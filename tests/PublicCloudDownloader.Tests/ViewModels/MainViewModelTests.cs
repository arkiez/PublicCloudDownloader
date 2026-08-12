using PublicCloudDownloader.App.ViewModels;
using PublicCloudDownloader.Core.Downloads;
using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Workflow;

namespace PublicCloudDownloader.Tests.ViewModels;

public sealed class MainViewModelTests
{
    [Fact]
    public void Download_is_enabled_only_for_a_complete_supported_link_and_writable_destination()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcd-vm-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var viewModel = new MainViewModel(new UnusedWorkflow()) { DestinationPath = root };
            viewModel.SourceLink = "https://drive.google.com/drive/folders/";
            Assert.False(viewModel.CanDownload);
            viewModel.SourceLink = "https://drive.google.com/drive/folders/1AbCdEfGhIjKlMnOpQrStUvWx";
            Assert.True(viewModel.CanDownload);
            Assert.Equal("Google Drive link format recognized", viewModel.LinkStatus);
            viewModel.SourceLink = "https://tenant.sharepoint.com/:f:/s/Test/Example";
            Assert.False(viewModel.CanDownload);
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class UnusedWorkflow : IDownloadWorkflow
    {
        public Task<PreparedDownload> PrepareAsync(string sourceLink, string destinationBase, IProgress<ManifestProgress>? progress, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DownloadResult> ExecuteAsync(PreparedDownload prepared, ExistingFilePolicy policy, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
