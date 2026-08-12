using System.Net;
using System.Net.Http.Headers;
using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Providers;

namespace PublicCloudDownloader.Providers.GoogleDrive;

public sealed class GoogleDriveProvider : IPublicCloudProvider, IAsyncDisposable
{
    private readonly HttpClient _client;
    private readonly GoogleDriveOptions _options;
    public ProviderKind Kind => ProviderKind.GoogleDrive;

    public GoogleDriveProvider(HttpMessageHandler? handler = null, GoogleDriveOptions? options = null)
    {
        _options = options ?? new();
        handler ??= new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = true, AutomaticDecompression = DecompressionMethods.All };
        _client = new HttpClient(handler, true);
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        _client.Timeout = TimeSpan.FromMinutes(10);
    }

    public async Task<PublicManifest> BuildManifestAsync(ParsedCloudLink link, IProgress<ManifestProgress>? progress, CancellationToken cancellationToken)
    {
        EnsureLink(link);
        if (link.Hint == SourceHint.Folder)
        {
            var items = new List<ManifestItem>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var root = await EnumerateFolderAsync(link.ShareId, string.Empty, link.ResourceKey, items, visited, progress, cancellationToken);
            return new(Kind, SourceHint.Folder, root, items);
        }

        var variant = VariantFromUri(link.OriginalUri);
        var extension = variant switch { DownloadVariant.GoogleDocument => ".docx", DownloadVariant.GoogleSpreadsheet => ".xlsx", DownloadVariant.GooglePresentation => ".pptx", _ => string.Empty };
        var name = $"Google Drive file {link.ShareId}{extension}";
        long? size = null;
        if (variant == DownloadVariant.Binary)
        {
            using var response = await ResolveBinaryResponseAsync(link.ShareId, link.ResourceKey, cancellationToken);
            name = GetFileName(response.Content.Headers.ContentDisposition) ?? name;
            size = response.Content.Headers.ContentLength;
        }
        var item = new ManifestItem(link.ShareId, name, ManifestItemKind.File, size, variant, null, link.ResourceKey);
        return new(Kind, SourceHint.File, name, new[] { item });
    }

    public async Task<DownloadLease> OpenDownloadAsync(ManifestItem item, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        if (item.Variant is DownloadVariant.GoogleDocument or DownloadVariant.GoogleSpreadsheet or DownloadVariant.GooglePresentation)
        {
            var (path, format) = item.Variant switch
            {
                DownloadVariant.GoogleDocument => ($"document/d/{item.Id}/export", "docx"),
                DownloadVariant.GoogleSpreadsheet => ($"spreadsheets/d/{item.Id}/export", "xlsx"),
                _ => ($"presentation/d/{item.Id}/export", "pptx")
            };
            response = await SendAsync(new Uri(_options.DocsBaseUri, $"{path}?format={format}"), cancellationToken);
        }
        else response = await ResolveBinaryResponseAsync(item.Id, item.ResourceKey, cancellationToken);
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true)
        {
            var html = await response.Content.ReadAsStringAsync(cancellationToken); response.Dispose();
            if (GoogleDriveHtmlParser.LooksPrivate(html)) throw new PrivateLinkException("This Google Drive item is not public or is no longer available.");
            throw new ProviderResponseChangedException("Google Drive returned an unexpected download page.");
        }
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return new(stream, response.Content.Headers.ContentLength, mediaType, new ResponseOwner(response));
    }

    private async Task<string> EnumerateFolderAsync(string id, string prefix, string? resourceKey, List<ManifestItem> output, HashSet<string> visited, IProgress<ManifestProgress>? progress, CancellationToken cancellationToken)
    {
        if (!visited.Add(id)) return Path.GetFileName(prefix);
        var query = $"embeddedfolderview?id={Uri.EscapeDataString(id)}" + (string.IsNullOrEmpty(resourceKey) ? string.Empty : $"&resourcekey={Uri.EscapeDataString(resourceKey)}");
        using var response = await SendAsync(new Uri(_options.DriveBaseUri, query), cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var (title, entries) = await GoogleDriveHtmlParser.ParseFolderAsync(html, cancellationToken);
        foreach (var entry in entries)
        {
            var relative = string.IsNullOrEmpty(prefix) ? entry.Name : Path.Combine(prefix, entry.Name);
            if (entry.Kind == ManifestItemKind.Directory)
            {
                output.Add(new(entry.Id, relative, ManifestItemKind.Directory, null, null, null, resourceKey));
                await EnumerateFolderAsync(entry.Id, relative, resourceKey, output, visited, progress, cancellationToken);
            }
            else output.Add(new(entry.Id, relative, ManifestItemKind.File, null, entry.Variant, null, resourceKey));
            progress?.Report(new(output.Count, relative));
        }
        return title;
    }

    private async Task<HttpResponseMessage> ResolveBinaryResponseAsync(string id, string? resourceKey, CancellationToken cancellationToken)
    {
        var query = $"uc?export=download&id={Uri.EscapeDataString(id)}" + (string.IsNullOrEmpty(resourceKey) ? string.Empty : $"&resourcekey={Uri.EscapeDataString(resourceKey)}");
        var response = await SendAsync(new Uri(_options.DriveBaseUri, query), cancellationToken);
        if (response.Content.Headers.ContentType?.MediaType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) != true) return response;
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var confirmation = await GoogleDriveHtmlParser.ParseConfirmationAsync(html, _options.DriveBaseUri, cancellationToken);
        response.Dispose();
        if (confirmation is null)
        {
            if (GoogleDriveHtmlParser.LooksPrivate(html)) throw new PrivateLinkException("This Google Drive item is not public or is no longer available.");
            throw new ProviderResponseChangedException("Google Drive returned an unrecognized confirmation page.");
        }
        ValidateGoogleUri(confirmation);
        return await SendAsync(confirmation, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(Uri uri, CancellationToken cancellationToken)
    {
        ValidateGoogleUri(uri);
        var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound) { response.Dispose(); throw new PrivateLinkException("This Google Drive item is not public or is no longer available."); }
        if ((int)response.StatusCode == 429) { response.Dispose(); throw new ProviderThrottledException("Google Drive is temporarily limiting requests."); }
        response.EnsureSuccessStatusCode();
        return response;
    }

    private static void ValidateGoogleUri(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !(uri.IdnHost.Equals("drive.google.com", StringComparison.OrdinalIgnoreCase) || uri.IdnHost.Equals("docs.google.com", StringComparison.OrdinalIgnoreCase) || uri.IdnHost.EndsWith(".googleusercontent.com", StringComparison.OrdinalIgnoreCase)))
            throw new ProviderResponseChangedException("Google Drive attempted to use an unexpected download host.");
    }

    private static DownloadVariant VariantFromUri(Uri uri) => uri.AbsolutePath.Contains("/document/", StringComparison.OrdinalIgnoreCase) ? DownloadVariant.GoogleDocument
        : uri.AbsolutePath.Contains("/spreadsheets/", StringComparison.OrdinalIgnoreCase) ? DownloadVariant.GoogleSpreadsheet
        : uri.AbsolutePath.Contains("/presentation/", StringComparison.OrdinalIgnoreCase) ? DownloadVariant.GooglePresentation : DownloadVariant.Binary;
    private static string? GetFileName(ContentDispositionHeaderValue? header) => header?.FileNameStar?.Trim('"') ?? header?.FileName?.Trim('"');
    private static void EnsureLink(ParsedCloudLink link) { if (link.Provider != ProviderKind.GoogleDrive) throw new ArgumentException("Link does not belong to Google Drive.", nameof(link)); }
    public ValueTask DisposeAsync() { _client.Dispose(); return ValueTask.CompletedTask; }

    private sealed class ResponseOwner(HttpResponseMessage response) : IAsyncDisposable { public ValueTask DisposeAsync() { response.Dispose(); return ValueTask.CompletedTask; } }
}
