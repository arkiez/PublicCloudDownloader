using System.Text.RegularExpressions;
using PublicCloudDownloader.Core.Models;

namespace PublicCloudDownloader.Core.Links;

public static partial class CloudLinkParser
{
    public static bool TryParse(string? value, out ParsedCloudLink? parsed, out LinkParseError? error)
    {
        parsed = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value)) return Fail("empty-link", "Enter a public Google Drive or OneDrive Personal link.", out error);
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return Fail("invalid-link", "Enter a complete HTTPS share link.", out error);
        if (!string.IsNullOrEmpty(uri.UserInfo)) return Fail("invalid-link", "Links containing credentials are not supported.", out error);

        var host = uri.IdnHost.ToLowerInvariant();
        if (host.EndsWith(".sharepoint.com", StringComparison.Ordinal))
            return Fail("onedrive-business", "OneDrive Business and SharePoint links are not supported.", out error);

        if (host is "drive.google.com" or "docs.google.com") return TryGoogle(uri, out parsed, out error);
        if (host is "1drv.ms" or "onedrive.live.com") return TryOneDrive(uri, out parsed, out error);
        return Fail("unsupported-provider", "Only public Google Drive and OneDrive Personal links are supported.", out error);
    }

    private static bool TryGoogle(Uri uri, out ParsedCloudLink? parsed, out LinkParseError? error)
    {
        var match = GooglePath().Match(uri.AbsolutePath);
        var query = ParseQuery(uri.Query);
        var id = match.Success ? match.Groups["id"].Value : query.GetValueOrDefault("id", string.Empty);
        if (id.Length < 8) { parsed = null; return Fail("incomplete-link", "The Google Drive link is incomplete.", out error); }
        var hint = uri.AbsolutePath.Contains("/folders/", StringComparison.OrdinalIgnoreCase) ? SourceHint.Folder : SourceHint.File;
        parsed = new(ProviderKind.GoogleDrive, uri, id, hint, query.GetValueOrDefault("resourcekey"));
        error = null;
        return true;
    }

    private static bool TryOneDrive(Uri uri, out ParsedCloudLink? parsed, out LinkParseError? error)
    {
        var query = ParseQuery(uri.Query);
        string shareId;
        SourceHint hint;
        if (uri.IdnHost.Equals("1drv.ms", StringComparison.OrdinalIgnoreCase))
        {
            var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) { parsed = null; return Fail("incomplete-link", "The OneDrive link is incomplete.", out error); }
            shareId = uri.ToString();
            hint = parts[0].Equals("f", StringComparison.OrdinalIgnoreCase) ? SourceHint.Folder : SourceHint.Unknown;
        }
        else
        {
            shareId = query.GetValueOrDefault("resid") ?? query.GetValueOrDefault("id") ?? query.GetValueOrDefault("redeem") ?? string.Empty;
            if (shareId.Length < 3) { parsed = null; return Fail("incomplete-link", "The OneDrive link is incomplete.", out error); }
            hint = SourceHint.Unknown;
        }
        parsed = new(ProviderKind.OneDrivePersonal, uri, shareId, hint, null, query);
        error = null;
        return true;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = pair.Split('=', 2);
            result[Uri.UnescapeDataString(split[0])] = split.Length == 2 ? Uri.UnescapeDataString(split[1].Replace('+', ' ')) : string.Empty;
        }
        return result;
    }

    private static bool Fail(string code, string message, out LinkParseError? error) { error = new(code, message); return false; }

    [GeneratedRegex(@"/(?:drive/)?folders/(?<id>[A-Za-z0-9_-]+)|/(?:file/d|document/d|spreadsheets/d|presentation/d)/(?<id>[A-Za-z0-9_-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex GooglePath();
}
