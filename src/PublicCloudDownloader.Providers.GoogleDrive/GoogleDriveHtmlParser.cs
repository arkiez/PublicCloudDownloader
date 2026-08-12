using AngleSharp.Html.Parser;
using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Providers;

namespace PublicCloudDownloader.Providers.GoogleDrive;

internal sealed record GoogleEntry(string Id, string Name, ManifestItemKind Kind, DownloadVariant? Variant);

internal static class GoogleDriveHtmlParser
{
    public static async Task<(string Title, IReadOnlyList<GoogleEntry> Entries)> ParseFolderAsync(string html, CancellationToken cancellationToken)
    {
        var document = await new HtmlParser().ParseDocumentAsync(html, cancellationToken);
        var title = document.Title?.Trim();
        var entries = new List<GoogleEntry>();
        foreach (var anchor in document.QuerySelectorAll("a[href]"))
        {
            if (!Uri.TryCreate(anchor.GetAttribute("href"), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) continue;
            if (uri.IdnHost is not ("drive.google.com" or "docs.google.com")) continue;
            var classified = Classify(uri);
            if (classified is null) continue;
            var name = anchor.TextContent.Trim();
            if (string.IsNullOrWhiteSpace(name)) name = classified.Value.Id;
            if (classified.Value.Variant is DownloadVariant.GoogleDocument && !name.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)) name += ".docx";
            if (classified.Value.Variant is DownloadVariant.GoogleSpreadsheet && !name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) name += ".xlsx";
            if (classified.Value.Variant is DownloadVariant.GooglePresentation && !name.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase)) name += ".pptx";
            entries.Add(new(classified.Value.Id, name, classified.Value.Kind, classified.Value.Variant));
        }
        if (string.IsNullOrWhiteSpace(title)) throw new ProviderResponseChangedException("Google Drive returned a folder page without a title.");
        if (entries.Count == 0 && LooksPrivate(html)) throw new PrivateLinkException("This Google Drive item is not public or is no longer available.");
        return (title, entries);
    }

    public static async Task<Uri?> ParseConfirmationAsync(string html, Uri baseUri, CancellationToken cancellationToken)
    {
        var document = await new HtmlParser().ParseDocumentAsync(html, cancellationToken);
        var form = document.QuerySelector("form#download-form");
        if (form is null || !Uri.TryCreate(baseUri, form.GetAttribute("action"), out var action) || action.Scheme != Uri.UriSchemeHttps) return null;
        var builder = new UriBuilder(action);
        var values = form.QuerySelectorAll("input[name]").Select(x => $"{Uri.EscapeDataString(x.GetAttribute("name")!)}={Uri.EscapeDataString(x.GetAttribute("value") ?? string.Empty)}");
        var query = string.Join("&", values);
        builder.Query = string.IsNullOrEmpty(builder.Query) ? query : builder.Query.TrimStart('?') + "&" + query;
        return builder.Uri;
    }

    public static bool LooksPrivate(string html) => html.Contains("sign in", StringComparison.OrdinalIgnoreCase)
        || html.Contains("request access", StringComparison.OrdinalIgnoreCase)
        || html.Contains("you need access", StringComparison.OrdinalIgnoreCase)
        || html.Contains("access denied", StringComparison.OrdinalIgnoreCase);

    private static (string Id, ManifestItemKind Kind, DownloadVariant? Variant)? Classify(Uri uri)
    {
        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var marker = Array.FindIndex(parts, x => x.Equals("folders", StringComparison.OrdinalIgnoreCase));
        if (marker >= 0 && marker + 1 < parts.Length) return (parts[marker + 1], ManifestItemKind.Directory, null);
        marker = Array.FindIndex(parts, x => x.Equals("d", StringComparison.OrdinalIgnoreCase));
        if (marker < 0 || marker + 1 >= parts.Length) return null;
        var variant = parts.Contains("document", StringComparer.OrdinalIgnoreCase) ? DownloadVariant.GoogleDocument
            : parts.Contains("spreadsheets", StringComparer.OrdinalIgnoreCase) ? DownloadVariant.GoogleSpreadsheet
            : parts.Contains("presentation", StringComparer.OrdinalIgnoreCase) ? DownloadVariant.GooglePresentation
            : DownloadVariant.Binary;
        return (parts[marker + 1], ManifestItemKind.File, variant);
    }
}
