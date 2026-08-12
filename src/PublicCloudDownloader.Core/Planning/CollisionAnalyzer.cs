using PublicCloudDownloader.Core.Models;

namespace PublicCloudDownloader.Core.Planning;

public static class CollisionAnalyzer
{
    public static IReadOnlyList<FileCollision> Find(DownloadPlan plan) => plan.Items
        .Where(x => x.Source.Kind == ManifestItemKind.File && File.Exists(x.FinalPath))
        .Select(x => new FileCollision(x, new FileInfo(x.FinalPath).Length))
        .ToArray();
}
