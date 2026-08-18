using PublicCloudDownloader.Core.Downloads;
using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Providers;
using PublicCloudDownloader.Infrastructure.Files;

namespace PublicCloudDownloader.Tests.Downloads;

public sealed class DownloadCoordinatorTests
{
    [Fact]
    public async Task Overwrite_policy_does_not_replace_a_new_unconfirmed_conflict()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcd-coordinator-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var target = Path.Combine(root, "file.txt"); await File.WriteAllTextAsync(target, "created-after-confirmation");
            var source = new ManifestItem("1", "file.txt", ManifestItemKind.File, 3, DownloadVariant.Binary);
            var plan = new DownloadPlan(root, root, new[] { new PlannedItem(source, target, "file.txt") });
            var provider = new BytesProvider("new");
            var result = await new DownloadCoordinator(new SafeFileWriter(), new ImmediateDelay()).RunAsync(provider, plan, ExistingFilePolicy.Overwrite, Guid.NewGuid(), [], null, default);
            Assert.Equal("created-after-confirmation", await File.ReadAllTextAsync(target));
            Assert.Equal(1, result.Skipped);
            Assert.Equal(0, provider.OpenCount);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Confirmed_overwrite_replaces_the_existing_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcd-coordinator-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var target = Path.Combine(root, "file.txt"); await File.WriteAllTextAsync(target, "old");
            var source = new ManifestItem("1", "file.txt", ManifestItemKind.File, 3, DownloadVariant.Binary);
            var plan = new DownloadPlan(root, root, new[] { new PlannedItem(source, target, "file.txt") });
            var provider = new BytesProvider("new");
            var result = await new DownloadCoordinator(new SafeFileWriter(), new ImmediateDelay()).RunAsync(provider, plan, ExistingFilePolicy.Overwrite, Guid.NewGuid(), [target], null, default);
            Assert.Equal("new", await File.ReadAllTextAsync(target));
            Assert.Equal(1, result.Downloaded);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Progress_reports_intermediate_percentage_while_a_file_is_streaming()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcd-progress-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var target = Path.Combine(root, "file.bin");
            var source = new ManifestItem("1", "file.bin", ManifestItemKind.File, 10, DownloadVariant.Binary);
            var plan = new DownloadPlan(root, root, new[] { new PlannedItem(source, target, "file.bin") });
            var reports = new List<DownloadProgress>();
            var progress = new InlineProgress<DownloadProgress>(reports.Add);
            await new DownloadCoordinator(new SafeFileWriter(), new ImmediateDelay()).RunAsync(new ChunkedProvider("0123456789", 5), plan, ExistingFilePolicy.Skip, Guid.NewGuid(), [], progress, default);
            Assert.Contains(reports, x => x.PercentComplete > 0 && x.PercentComplete < 100);
            Assert.Equal(100, reports.Last().PercentComplete);
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class ChunkedProvider(string content, int chunkSize) : IPublicCloudProvider
    {
        public ProviderKind Kind => ProviderKind.GoogleDrive;
        public Task<PublicManifest> BuildManifestAsync(ParsedCloudLink link, IProgress<ManifestProgress>? progress, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DownloadLease> OpenDownloadAsync(ManifestItem item, CancellationToken cancellationToken)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            return Task.FromResult(new DownloadLease(new ChunkedStream(bytes, chunkSize), bytes.Length, "application/octet-stream"));
        }
    }

    private sealed class ChunkedStream(byte[] bytes, int chunkSize) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(buffer.Length, chunkSize)], cancellationToken);
    }

    [Fact]
    public async Task Progress_reports_three_active_downloads_with_individual_status()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcd-active-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var items = Enumerable.Range(1, 4).Select(i =>
            {
                var name = $"file{i}.bin";
                var source = new ManifestItem(i.ToString(), name, ManifestItemKind.File, 10, DownloadVariant.Binary);
                return new PlannedItem(source, Path.Combine(root, name), name);
            }).ToArray();
            var plan = new DownloadPlan(root, root, items);
            var reports = new System.Collections.Concurrent.ConcurrentQueue<DownloadProgress>();
            var provider = new GatedProvider("0123456789");
            var run = new DownloadCoordinator(new SafeFileWriter(), new ImmediateDelay()).RunAsync(
                provider, plan, ExistingFilePolicy.Skip, Guid.NewGuid(), [], new InlineProgress<DownloadProgress>(reports.Enqueue), default);

            await provider.ThreeStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Contains(reports, x => x.ActiveDownloads.Count == 3 && x.ActiveDownloads.All(y => y.Status == "Downloading"));
            Assert.All(reports, x => Assert.InRange(x.ActiveDownloads.Count, 0, 3));

            provider.Release();
            await run;
            Assert.Empty(reports.Last().ActiveDownloads);
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class GatedProvider(string content) : IPublicCloudProvider
    {
        private int _started;
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ThreeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ProviderKind Kind => ProviderKind.GoogleDrive;
        public Task<PublicManifest> BuildManifestAsync(ParsedCloudLink link, IProgress<ManifestProgress>? progress, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DownloadLease> OpenDownloadAsync(ManifestItem item, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _started) == 3) ThreeStarted.TrySetResult();
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            return Task.FromResult(new DownloadLease(new GatedStream(bytes, _release.Task), bytes.Length, "application/octet-stream"));
        }
        public void Release() => _release.TrySetResult();
    }

    private sealed class GatedStream(byte[] bytes, Task release) : MemoryStream(bytes)
    {
        private bool _released;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_released) { await release.WaitAsync(cancellationToken); _released = true; }
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }
    private sealed class BytesProvider(string content) : IPublicCloudProvider
    {
        public int OpenCount { get; private set; }
        public ProviderKind Kind => ProviderKind.GoogleDrive;
        public Task<PublicManifest> BuildManifestAsync(ParsedCloudLink link, IProgress<ManifestProgress>? progress, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DownloadLease> OpenDownloadAsync(ManifestItem item, CancellationToken cancellationToken) { OpenCount++; return Task.FromResult(new DownloadLease(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)), content.Length, "text/plain")); }
    }
    private sealed class ImmediateDelay : IDelay { public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask; }
}
