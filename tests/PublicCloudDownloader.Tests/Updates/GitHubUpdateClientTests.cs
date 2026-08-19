using System.Net;
using System.Net.Http;
using PublicCloudDownloader.App.Updates;

namespace PublicCloudDownloader.Tests.Updates;

public sealed class GitHubUpdateClientTests
{
    private const string Digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task Newer_stable_release_returns_package_metadata()
    {
        var client = CreateClient(Json("v1.2.1"));
        var release = await client.CheckAsync(new Version(1, 2, 0), CancellationToken.None);

        Assert.NotNull(release);
        Assert.Equal(new Version(1, 2, 1), release.Version);
        Assert.Equal("PublicCloudDownloader-v1.2.1-win-x64.zip", release.Package.Name);
        Assert.Equal(Digest, release.Package.Digest);
    }

    [Theory]
    [InlineData("v1.2.0", false, false)]
    [InlineData("v1.2.1", true, false)]
    [InlineData("v1.2.1", false, true)]
    public async Task Ineligible_release_returns_null(string tag, bool draft, bool prerelease)
    {
        var client = CreateClient(Json(tag, draft, prerelease));
        Assert.Null(await client.CheckAsync(new Version(1, 2, 0), CancellationToken.None));
    }

    [Fact]
    public async Task Missing_expected_zip_throws()
    {
        var json = Json("v1.2.1").Replace("PublicCloudDownloader-v1.2.1-win-x64.zip", "other.zip");
        await Assert.ThrowsAsync<UpdateCheckException>(() =>
            CreateClient(json).CheckAsync(new Version(1, 2, 0), CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("sha256:abc")]
    [InlineData("md5:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Invalid_digest_throws(string digest)
    {
        var json = Json("v1.2.1").Replace(Digest, digest);
        await Assert.ThrowsAsync<UpdateCheckException>(() =>
            CreateClient(json).CheckAsync(new Version(1, 2, 0), CancellationToken.None));
    }

    [Fact]
    public async Task Non_success_response_throws_update_check_exception()
    {
        var client = CreateClient("{}", HttpStatusCode.Forbidden);
        await Assert.ThrowsAsync<UpdateCheckException>(() =>
            client.CheckAsync(new Version(1, 2, 0), CancellationToken.None));
    }

    private static GitHubUpdateClient CreateClient(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var http = new HttpClient(new StubHandler(status, json));
        return new GitHubUpdateClient(http);
    }

    private static string Json(string tag, bool draft = false, bool prerelease = false)
        => $$"""
        {"tag_name":"{{tag}}","draft":{{draft.ToString().ToLowerInvariant()}},"prerelease":{{prerelease.ToString().ToLowerInvariant()}},"body":"Bug fixes","assets":[{"name":"PublicCloudDownloader-{{tag}}-win-x64.zip","browser_download_url":"https://example.test/app.zip","digest":"{{Digest}}","size":123}]}
        """;

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://api.github.com/repos/arkiez/PublicCloudDownloader/releases/latest", request.RequestUri!.ToString());
            Assert.NotNull(request.Headers.UserAgent.FirstOrDefault());
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body)
            });
        }
    }
}
