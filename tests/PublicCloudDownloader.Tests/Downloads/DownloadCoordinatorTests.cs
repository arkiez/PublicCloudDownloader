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

    private sealed class BytesProvider(string content) : IPublicCloudProvider
    {
        public int OpenCount { get; private set; }
        public ProviderKind Kind => ProviderKind.GoogleDrive;
        public Task<PublicManifest> BuildManifestAsync(ParsedCloudLink link, IProgress<ManifestProgress>? progress, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DownloadLease> OpenDownloadAsync(ManifestItem item, CancellationToken cancellationToken) { OpenCount++; return Task.FromResult(new DownloadLease(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)), content.Length, "text/plain")); }
    }
    private sealed class ImmediateDelay : IDelay { public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask; }
}
