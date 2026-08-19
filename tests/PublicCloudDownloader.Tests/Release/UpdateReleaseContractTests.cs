namespace PublicCloudDownloader.Tests.Release;

public sealed class UpdateReleaseContractTests
{
    [Fact]
    public void Repository_contains_guarded_public_release_workflow()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "scripts", "security-scan-public.ps1")));
        Assert.True(File.Exists(Path.Combine(root, "scripts", "publish-github-release.ps1")));

        var readme = File.ReadAllText(Path.Combine(root, "docs", "PublicCloudDownloader-README.txt"));
        Assert.Contains("Check for updates", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GitHub Releases", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SHA-256", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data and logs", readme, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Release_publisher_checks_existing_releases_without_failing_on_missing_tag()
    {
        var root = FindRepoRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "publish-github-release.ps1"));

        Assert.Contains("gh release list", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gh release view", script, StringComparison.OrdinalIgnoreCase);
    }
    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Version.props")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
