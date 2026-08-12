using PublicCloudDownloader.Core.Links;
using PublicCloudDownloader.Core.Models;

namespace PublicCloudDownloader.Tests.Links;

public sealed class CloudLinkParserTests
{
    [Theory]
    [InlineData("https://drive.google.com/drive/folders/1AbCdEfGhIjKlMnOpQrStUvWx", ProviderKind.GoogleDrive, SourceHint.Folder)]
    [InlineData("https://drive.google.com/file/d/1AbCdEfGhIjKlMnOpQrStUvWx/view", ProviderKind.GoogleDrive, SourceHint.File)]
    [InlineData("https://docs.google.com/document/d/1AbCdEfGhIjKlMnOpQrStUvWx/edit", ProviderKind.GoogleDrive, SourceHint.File)]
    [InlineData("https://1drv.ms/f/c/bdbb59c1db4a58dd/ExampleToken", ProviderKind.OneDrivePersonal, SourceHint.Folder)]
    [InlineData("https://onedrive.live.com/?cid=ABC123&id=ABC123!456", ProviderKind.OneDrivePersonal, SourceHint.Unknown)]
    public void TryParse_accepts_complete_supported_links(string value, ProviderKind provider, SourceHint hint)
    {
        Assert.True(CloudLinkParser.TryParse(value, out var parsed, out var error));
        Assert.Null(error);
        Assert.Equal(provider, parsed!.Provider);
        Assert.Equal(hint, parsed.Hint);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://drive.google.com/drive/folders/")]
    [InlineData("http://drive.google.com/drive/folders/1AbCdEfGhIjKlMnOpQrStUvWx")]
    [InlineData("https://tenant.sharepoint.com/:f:/s/Test/Example")]
    [InlineData("C:\\Downloads")]
    public void TryParse_rejects_incomplete_unsafe_or_unsupported_links(string value)
    {
        Assert.False(CloudLinkParser.TryParse(value, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Google_resource_key_is_preserved_for_anonymous_requests()
    {
        CloudLinkParser.TryParse("https://drive.google.com/drive/folders/1AbCdEfGhIjKlMnOpQrStUvWx?resourcekey=abc123", out var parsed, out _);
        Assert.Equal("abc123", parsed!.ResourceKey);
    }
}
