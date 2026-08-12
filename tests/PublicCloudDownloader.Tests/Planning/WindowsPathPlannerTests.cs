using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Planning;

namespace PublicCloudDownloader.Tests.Planning;

public sealed class WindowsPathPlannerTests
{
    [Fact]
    public void Folder_manifest_is_rooted_below_destination_and_keeps_hierarchy()
    {
        var manifest = Manifest(SourceHint.Folder, "Project Assets", File("docs/brief.pdf"), Directory("images"), File("images/logo.png"));
        var plan = WindowsPathPlanner.CreatePlan(manifest, @"C:\Downloads");
        Assert.Equal(@"C:\Downloads\Project Assets", plan.OutputRoot);
        Assert.Contains(plan.Items, x => x.FinalPath == @"C:\Downloads\Project Assets\docs\brief.pdf");
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData("folder/../../escape.txt")]
    public void Planner_never_allows_manifest_paths_to_escape_destination(string relativePath)
    {
        var ex = Assert.Throws<ManifestPathException>(() => WindowsPathPlanner.CreatePlan(Manifest(SourceHint.Folder, "Safe", File(relativePath)), @"C:\Downloads"));
        Assert.Equal("path-escape", ex.Code);
    }

    [Fact]
    public void Sanitized_case_insensitive_collisions_receive_deterministic_suffixes()
    {
        var plan = WindowsPathPlanner.CreatePlan(Manifest(SourceHint.Folder, "Root", File("A?.txt"), File("a*.txt")), @"C:\Downloads");
        Assert.Equal(new[] { "A_.txt", "a_ (2).txt" }, plan.Items.Select(x => Path.GetFileName(x.FinalPath)).ToArray());
    }

    [Fact]
    public void Single_file_is_written_directly_below_destination()
    {
        var plan = WindowsPathPlanner.CreatePlan(Manifest(SourceHint.File, "report.pdf", File("report.pdf")), @"C:\Downloads");
        Assert.Equal(@"C:\Downloads", plan.OutputRoot);
        Assert.Equal(@"C:\Downloads\report.pdf", Assert.Single(plan.Items).FinalPath);
    }

    [Theory]
    [InlineData("CON", "CON_")]
    [InlineData("CON.tar.gz", "CON_.tar.gz")]
    [InlineData("bad. ", "bad")]
    [InlineData("", "_")]
    public void Unsafe_windows_names_are_sanitized(string input, string expected)
    {
        var plan = WindowsPathPlanner.CreatePlan(Manifest(SourceHint.Folder, "Root", File(input)), @"C:\Downloads");
        Assert.Equal(expected, Path.GetFileName(Assert.Single(plan.Items).FinalPath));
    }

    [Fact]
    public void Drive_root_destination_is_preserved_as_a_rooted_path()
    {
        var driveRoot = Path.GetPathRoot(Environment.SystemDirectory)!;
        var plan = WindowsPathPlanner.CreatePlan(Manifest(SourceHint.File, "report.pdf", File("report.pdf")), driveRoot);
        Assert.Equal(Path.Combine(driveRoot, "report.pdf"), Assert.Single(plan.Items).FinalPath);
    }

    [Fact]
    public void Renamed_directory_collision_keeps_each_child_with_its_parent()
    {
        var firstFolder = new ManifestItem("folder-1", "A?", ManifestItemKind.Directory, null, null);
        var secondFolder = new ManifestItem("folder-2", "A*", ManifestItemKind.Directory, null, null);
        var firstChild = new ManifestItem("file-1", "A?/first.txt", ManifestItemKind.File, 1, DownloadVariant.Binary, ParentId: "folder-1");
        var secondChild = new ManifestItem("file-2", "A*/second.txt", ManifestItemKind.File, 1, DownloadVariant.Binary, ParentId: "folder-2");
        var plan = WindowsPathPlanner.CreatePlan(Manifest(SourceHint.Folder, "Root", firstFolder, secondFolder, firstChild, secondChild), @"C:\Downloads");
        Assert.Contains(plan.Items, x => x.RelativeOutputPath == Path.Combine("A_", "first.txt"));
        Assert.Contains(plan.Items, x => x.RelativeOutputPath == Path.Combine("A_ (2)", "second.txt"));
    }

    private static PublicManifest Manifest(SourceHint hint, string root, params ManifestItem[] items) => new(ProviderKind.GoogleDrive, hint, root, items);
    private static ManifestItem File(string path) => new(Guid.NewGuid().ToString("N"), path, ManifestItemKind.File, null, null);
    private static ManifestItem Directory(string path) => new(Guid.NewGuid().ToString("N"), path, ManifestItemKind.Directory, null, null);
}
