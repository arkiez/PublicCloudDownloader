using System.Net;
using System.Text;
using PublicCloudDownloader.Core.Links;
using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Providers;
using PublicCloudDownloader.Providers.GoogleDrive;

namespace PublicCloudDownloader.Tests.Providers;

public sealed class GoogleDriveProviderTests
{
    [Fact]
    public async Task Manifest_recurses_folders_and_maps_native_exports()
    {
        using var handler = new GoogleHandler();
        await using var provider = new GoogleDriveProvider(handler);
        CloudLinkParser.TryParse("https://drive.google.com/drive/folders/1RootFolderIdentifier1234567?resourcekey=safe", out var link, out _);
        var manifest = await provider.BuildManifestAsync(link!, null, default);
        Assert.Equal("Project Assets", manifest.RootName);
        Assert.Contains(manifest.Items, x => x.RelativePath == "logo.png" && x.Variant == DownloadVariant.Binary);
        Assert.Contains(manifest.Items, x => x.RelativePath == "brief.docx" && x.Variant == DownloadVariant.GoogleDocument);
        Assert.Contains(manifest.Items, x => x.RelativePath == Path.Combine("docs", "guide.pdf"));
        Assert.Contains(handler.Requests, x => x.Contains("resourcekey=safe", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Download_follows_confirmation_form_in_same_session()
    {
        using var handler = new GoogleHandler();
        await using var provider = new GoogleDriveProvider(handler);
        var item = new ManifestItem("1BinaryFileIdentifier123456", "logo.png", ManifestItemKind.File, null, DownloadVariant.Binary);
        await using var lease = await provider.OpenDownloadAsync(item, default);
        Assert.Equal("payload", await new StreamReader(lease.Content).ReadToEndAsync());
        Assert.Contains(handler.Requests, x => x.Contains("confirm=yes", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Provider_rejects_a_redirect_to_an_untrusted_host()
    {
        using var handler = new EvilRedirectHandler();
        await using var provider = new GoogleDriveProvider(handler);
        CloudLinkParser.TryParse("https://drive.google.com/drive/folders/1RootFolderIdentifier1234567", out var link, out _);
        await Assert.ThrowsAsync<ProviderResponseChangedException>(() => provider.BuildManifestAsync(link!, null, default));
    }

    [Fact]
    public async Task Public_folder_shortcut_cycle_is_ignored_safely()
    {
        using var handler = new CycleHandler();
        await using var provider = new GoogleDriveProvider(handler);
        CloudLinkParser.TryParse("https://drive.google.com/drive/folders/1RootFolderIdentifier1234567", out var link, out _);
        var manifest = await provider.BuildManifestAsync(link!, null, default);
        Assert.Single(manifest.Items);
        Assert.Equal("docs", manifest.Items[0].RelativePath);
        Assert.Equal(2, handler.RequestCount);
    }

    private sealed class GoogleHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.ToString(); Requests.Add(uri);
            if (uri.Contains("embeddedfolderview", StringComparison.Ordinal) && uri.Contains("1RootFolder", StringComparison.Ordinal))
                return Html("<html><head><title>Project Assets</title></head><body><a href='https://drive.google.com/file/d/1BinaryFileIdentifier123456/view'>logo.png</a><a href='https://docs.google.com/document/d/1GoogleDocIdentifier12345678/edit'>brief</a><a href='https://drive.google.com/drive/folders/1NestedFolderIdentifier12345'>docs</a></body></html>");
            if (uri.Contains("embeddedfolderview", StringComparison.Ordinal))
                return Html("<html><head><title>docs</title></head><body><a href='https://drive.google.com/file/d/1GuideIdentifier123456789/view'>guide.pdf</a></body></html>");
            if (uri.Contains("confirm=yes", StringComparison.Ordinal)) return Bytes("payload", "application/octet-stream");
            if (uri.Contains("/uc?", StringComparison.Ordinal))
                return Html("<html><form id='download-form' action='https://drive.google.com/download'><input name='confirm' value='yes'><input name='id' value='1BinaryFileIdentifier123456'></form></html>");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
        private static Task<HttpResponseMessage> Html(string html) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") });
        private static Task<HttpResponseMessage> Bytes(string text, string type) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, type) });
    }

    private sealed class EvilRedirectHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.RequestUri = new Uri("https://evil.example/folder");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new StringContent("<html><title>Wrong host</title></html>") });
        }
    }
    private sealed class CycleHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var isRoot = request.RequestUri!.Query.Contains("1RootFolder", StringComparison.Ordinal);
            var html = isRoot
                ? "<html><title>Root</title><a href='https://drive.google.com/drive/folders/1NestedFolderIdentifier12345'>docs</a></html>"
                : "<html><title>docs</title><a href='https://drive.google.com/drive/folders/1RootFolderIdentifier1234567'>root shortcut</a></html>";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new StringContent(html) });
        }
    }
}
