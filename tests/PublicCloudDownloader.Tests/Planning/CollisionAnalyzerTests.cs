using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Planning;

namespace PublicCloudDownloader.Tests.Planning;

public sealed class CollisionAnalyzerTests
{
    [Fact]
    public void Find_returns_only_existing_files_and_never_directories()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcd-collision-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "exists.txt"), "old");
            var items = new[]
            {
                new PlannedItem(new("1", "exists.txt", ManifestItemKind.File, null, null), Path.Combine(root, "exists.txt"), "exists.txt"),
                new PlannedItem(new("2", "folder", ManifestItemKind.Directory, null, null), Path.Combine(root, "folder"), "folder")
            };
            var conflicts = CollisionAnalyzer.Find(new(root, root, items));
            Assert.Equal("exists.txt", Assert.Single(conflicts).Item.RelativeOutputPath);
        }
        finally { Directory.Delete(root, true); }
    }
}
