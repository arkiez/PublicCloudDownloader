using System.Text.RegularExpressions;

namespace PublicCloudDownloader.App.Updates;

public static class UpdateVersion
{
    private static readonly Regex StableTag = new(
        "^v(?<v>\\d+\\.\\d+\\.\\d+)$",
        RegexOptions.CultureInvariant);

    public static bool IsNewer(string tag, Version current)
        => TryParseTag(tag, out var candidate) && candidate > current;

    public static bool TryParseTag(string tag, out Version version)
    {
        var match = StableTag.Match(tag ?? string.Empty);
        return Version.TryParse(
            match.Success ? match.Groups["v"].Value : string.Empty,
            out version!);
    }
}
