using System.Net;
using System.Text;
using PublicCloudDownloader.Core.Links;
using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Providers;
using PublicCloudDownloader.Providers.OneDrivePersonal;

namespace PublicCloudDownloader.Tests.Providers;

public sealed class OneDrivePersonalProviderTests
{
    [Fact]
    public async Task Manifest_redeems_badger_share_and_recurses_children()
    {
        using var handler = new OneDriveHandler();
        await using var provider = new OneDrivePersonalProvider(handler);
        CloudLinkParser.TryParse("https://1drv.ms/f/c/bdbb59c1db4a58dd/ExampleToken", out var link, out _);
        var manifest = await provider.BuildManifestAsync(link!, null, default);
        Assert.Equal("Shared Photos", manifest.RootName);
        Assert.Contains(manifest.Items, x => x.RelativePath == "photo.jpg");
        Assert.Contains(manifest.Items, x => x.RelativePath == Path.Combine("docs", "readme.txt"));
        Assert.NotEmpty(handler.BadgerRequests);
        Assert.All(handler.BadgerRequests, value => Assert.Equal("Badger test-token", value));
    }

    [Fact]
    public async Task Download_refreshes_temporary_url_before_streaming()
    {
        using var handler = new OneDriveHandler();
        await using var provider = new OneDrivePersonalProvider(handler);
        CloudLinkParser.TryParse("https://1drv.ms/f/c/bdbb59c1db4a58dd/ExampleToken", out var link, out _);
        var manifest = await provider.BuildManifestAsync(link!, null, default);
        var item = manifest.Items.Single(x => x.RelativePath == "photo.jpg");
        await using var lease = await provider.OpenDownloadAsync(item, default);
        Assert.Equal("new-content", await new StreamReader(lease.Content).ReadToEndAsync());
        Assert.Contains(handler.Requests, x => x.Contains("/items/file1", StringComparison.Ordinal) && !x.Contains("children", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Download_rejects_a_final_redirect_to_an_untrusted_host()
    {
        using var handler = new OneDriveHandler { RedirectContentToEvilHost = true };
        await using var provider = new OneDrivePersonalProvider(handler);
        CloudLinkParser.TryParse("https://1drv.ms/f/c/bdbb59c1db4a58dd/ExampleToken", out var link, out _);
        var manifest = await provider.BuildManifestAsync(link!, null, default);
        var item = manifest.Items.Single(x => x.RelativePath == "photo.jpg");
        await Assert.ThrowsAsync<ProviderResponseChangedException>(() => provider.OpenDownloadAsync(item, default));
    }

    [Fact]
    public async Task Short_link_redirect_to_sharepoint_is_reported_as_unsupported_business_link()
    {
        using var handler = new OneDriveHandler { RedirectShortToSharePoint = true };
        await using var provider = new OneDrivePersonalProvider(handler);
        CloudLinkParser.TryParse("https://1drv.ms/f/c/bdbb59c1db4a58dd/ExampleToken", out var link, out _);
        await Assert.ThrowsAsync<UnsupportedCloudItemException>(() => provider.BuildManifestAsync(link!, null, default));
    }

    private sealed class OneDriveHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public List<string?> BadgerRequests { get; } = [];
        public bool RedirectContentToEvilHost { get; init; }
        public bool RedirectShortToSharePoint { get; init; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.ToString(); Requests.Add(uri);
            if (request.Headers.Authorization is not null) BadgerRequests.Add(request.Headers.Authorization.ToString());
            if (uri.StartsWith("https://1drv.ms/", StringComparison.Ordinal))
            {
                var target = RedirectShortToSharePoint ? new Uri("https://tenant.sharepoint.com/:f:/s/Test/Example") : new Uri("https://onedrive.live.com/?redeem=redeem-token");
                return Json("{}", request, target);
            }
            if (uri.Contains("api-badgerp", StringComparison.Ordinal)) return Json("{\"token\":\"test-token\"}", request);
            if (uri.Contains("/shares/u!redeem-token/driveitem", StringComparison.Ordinal))
                return Json("{\"id\":\"root\",\"name\":\"Shared Photos\",\"folder\":{\"childCount\":2},\"parentReference\":{\"driveId\":\"drive\"}}", request);
            if (uri.Contains("/items/root/children", StringComparison.Ordinal))
                return Json("{\"value\":[{\"id\":\"file1\",\"name\":\"photo.jpg\",\"size\":11,\"file\":{\"mimeType\":\"image/jpeg\"},\"parentReference\":{\"driveId\":\"drive\"}},{\"id\":\"folder2\",\"name\":\"docs\",\"folder\":{\"childCount\":1},\"parentReference\":{\"driveId\":\"drive\"}}]}", request);
            if (uri.Contains("/items/folder2/children", StringComparison.Ordinal))
                return Json("{\"value\":[{\"id\":\"file2\",\"name\":\"readme.txt\",\"size\":3,\"file\":{\"mimeType\":\"text/plain\"},\"parentReference\":{\"driveId\":\"drive\"}}]}", request);
            if (uri.Contains("/items/file1", StringComparison.Ordinal))
                return Json("{\"id\":\"file1\",\"@content.downloadUrl\":\"https://public.dm.files.1drv.com/content\"}", request);
            if (uri.StartsWith("https://public.dm.files.1drv.com", StringComparison.Ordinal))
            {
                if (RedirectContentToEvilHost) request.RequestUri = new Uri("https://evil.example/content");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("new-content", Encoding.UTF8, "application/octet-stream"), RequestMessage = request });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
        }
        private static Task<HttpResponseMessage> Json(string value, HttpRequestMessage request, Uri? finalUri = null)
        {
            if (finalUri is not null) request.RequestUri = finalUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json"), RequestMessage = request });
        }
    }
}
