using PublicCloudDownloader.Core.Links;
using PublicCloudDownloader.Core.Downloads;
using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Providers;
using PublicCloudDownloader.Core.Workflow;
using PublicCloudDownloader.Infrastructure.Files;

namespace PublicCloudDownloader.Tests.Workflow;

public sealed class DownloadWorkflowTests
{
    [Fact]
    public async Task Prepare_builds_plan_without_creating_output_root()
    {
        var destination = Path.Combine(Path.GetTempPath(), "pcd-flow-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(destination);
        try
        {
            var manifest = new PublicManifest(ProviderKind.GoogleDrive, SourceHint.Folder, "Shared", new[] { new ManifestItem("1", "a.txt", ManifestItemKind.File, 1, DownloadVariant.Binary) });
            var provider = new FakeProvider(manifest);
            var workflow = new DownloadWorkflow(new FakeFactory(provider), new SafeFileWriter(), new ImmediateDelay(), null);
            await using var prepared = await workflow.PrepareAsync("https://drive.google.com/drive/folders/12345678", destination, null, default);
            Assert.Equal(Path.Combine(destination, "Shared"), prepared.Plan.OutputRoot);
            Assert.False(Directory.Exists(prepared.Plan.OutputRoot));
        }
        finally { Directory.Delete(destination, true); }
    }

    private sealed class FakeFactory(IPublicCloudProvider provider) : IProviderFactory { public IPublicCloudProvider Create(ProviderKind kind) => provider; }
    private sealed class FakeProvider(PublicManifest manifest) : IPublicCloudProvider, IAsyncDisposable
    {
        public ProviderKind Kind => ProviderKind.GoogleDrive;
        public Task<PublicManifest> BuildManifestAsync(ParsedCloudLink link, IProgress<ManifestProgress>? progress, CancellationToken cancellationToken) => Task.FromResult(manifest);
        public Task<DownloadLease> OpenDownloadAsync(ManifestItem item, CancellationToken cancellationToken) => Task.FromResult(new DownloadLease(new MemoryStream([1]), 1, null));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class ImmediateDelay : IDelay { public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask; }
}
