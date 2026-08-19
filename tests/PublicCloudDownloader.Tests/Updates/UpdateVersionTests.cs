using PublicCloudDownloader.App.Updates;

namespace PublicCloudDownloader.Tests.Updates;

public sealed class UpdateVersionTests
{
    [Theory]
    [InlineData("v1.2.1", "1.2.0", true)]
    [InlineData("v1.2.0", "1.2.0", false)]
    [InlineData("v1.1.9", "1.2.0", false)]
    public void IsNewer_compares_stable_versions(string tag, string current, bool expected)
    {
        Assert.Equal(expected, UpdateVersion.IsNewer(tag, Version.Parse(current)));
    }

    [Theory]
    [InlineData("1.2.1")]
    [InlineData("v1.2")]
    [InlineData("v1.2.1-beta.1")]
    public void IsNewer_rejects_non_release_tags(string tag)
    {
        Assert.False(UpdateVersion.IsNewer(tag, new Version(1, 2, 0)));
    }
}
