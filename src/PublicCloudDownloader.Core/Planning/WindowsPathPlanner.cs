using PublicCloudDownloader.Core.Models;

namespace PublicCloudDownloader.Core.Planning;

public sealed class ManifestPathException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public static class WindowsPathPlanner
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

    public static DownloadPlan CreatePlan(PublicManifest manifest, string destination)
    {
        if (string.IsNullOrWhiteSpace(destination) || !Path.IsPathFullyQualified(destination))
            throw new ArgumentException("Destination must be an absolute path.", nameof(destination));
        var destinationFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        var outputRoot = manifest.SourceKind == SourceHint.Folder
            ? Path.Combine(destinationFull, SanitizeSegment(manifest.RootName))
            : destinationFull;
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var planned = new List<PlannedItem>(manifest.Items.Count);
        var resolvedDirectories = new Dictionary<string, string>(StringComparer.Ordinal);
        var ordered = manifest.Items.Select((item, index) => (item, index))
            .OrderBy(x => PathDepth(x.item.RelativePath)).ThenBy(x => x.item.Kind == ManifestItemKind.Directory ? 0 : 1).ThenBy(x => x.index);
        foreach (var entry in ordered)
        {
            var item = entry.item;
            string relative;
            if (!string.IsNullOrEmpty(item.ParentId) && resolvedDirectories.TryGetValue(item.ParentId, out var resolvedParent))
                relative = Path.Combine(resolvedParent, SanitizeSegment(Path.GetFileName(item.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
            else relative = SanitizeRelativePath(item.RelativePath);
            relative = MakeUnique(relative, used);
            var finalPath = Path.GetFullPath(Path.Combine(outputRoot, relative));
            var rootWithSeparator = Path.GetFullPath(outputRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!finalPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                throw new ManifestPathException("path-escape", "A cloud item attempted to escape the selected destination.");
            planned.Add(new(item, finalPath, relative));
            if (item.Kind == ManifestItemKind.Directory) resolvedDirectories[item.Id] = relative;
        }
        return new(destinationFull, outputRoot, planned);
    }

    public static string SanitizeSegment(string value)
    {
        var chars = value.Select(c => c < 32 || "<>:\"/\\|?*".Contains(c) ? '_' : c).ToArray();
        var result = new string(chars).TrimEnd(' ', '.');
        if (string.IsNullOrWhiteSpace(result)) result = "_";
        var firstExtension = result.IndexOf('.');
        var deviceStem = firstExtension < 0 ? result : result[..firstExtension];
        if (Reserved.Contains(deviceStem)) result = deviceStem + "_" + result[deviceStem.Length..];
        return result;
    }

    private static string SanitizeRelativePath(string path)
    {
        if (Path.IsPathFullyQualified(path) || path.Contains(':')) ThrowEscape();
        var segments = path.Replace('\\', '/').Split('/');
        if (segments.Any(x => x is "." or "..")) ThrowEscape();
        return Path.Combine(segments.Select(SanitizeSegment).ToArray());
    }

    private static string MakeUnique(string relative, HashSet<string> used)
    {
        if (used.Add(relative)) return relative;
        var directory = Path.GetDirectoryName(relative);
        var stem = Path.GetFileNameWithoutExtension(relative);
        var extension = Path.GetExtension(relative);
        for (var i = 2; ; i++)
        {
            var candidateName = $"{stem} ({i}){extension}";
            var candidate = string.IsNullOrEmpty(directory) ? candidateName : Path.Combine(directory, candidateName);
            if (used.Add(candidate)) return candidate;
        }
    }

    private static void ThrowEscape() => throw new ManifestPathException("path-escape", "A cloud item contained an unsafe path.");
    private static int PathDepth(string path) => path.Count(c => c is '/' or '\\');
}
