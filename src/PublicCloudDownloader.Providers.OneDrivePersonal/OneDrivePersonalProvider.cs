using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Providers;

namespace PublicCloudDownloader.Providers.OneDrivePersonal;

public sealed class OneDrivePersonalProvider : IPublicCloudProvider, IAsyncDisposable
{
    private readonly HttpClient _client;
    private readonly OneDrivePersonalOptions _options;
    private readonly Dictionary<string, string> _driveByItem = new(StringComparer.Ordinal);
    private string? _badgerToken;
    private string? _legacyAuthKey;
    private string? _legacyCid;
    public ProviderKind Kind => ProviderKind.OneDrivePersonal;

    public OneDrivePersonalProvider(HttpMessageHandler? handler = null, OneDrivePersonalOptions? options = null)
    {
        _options = options ?? new();
        handler ??= new HttpClientHandler { AllowAutoRedirect = true, AutomaticDecompression = DecompressionMethods.All };
        _client = new HttpClient(handler, true);
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        _client.Timeout = TimeSpan.FromMinutes(10);
    }

    public async Task<PublicManifest> BuildManifestAsync(ParsedCloudLink link, IProgress<ManifestProgress>? progress, CancellationToken cancellationToken)
    {
        if (link.Provider != Kind) throw new ArgumentException("Link does not belong to OneDrive Personal.", nameof(link));
        _driveByItem.Clear();
        var resolved = await ResolveShareAsync(link, cancellationToken);
        using var root = await GetJsonAsync(resolved.RootUri, cancellationToken);
        var rootItem = ParseItem(root.RootElement);
        if (rootItem.DriveId is null) throw new ProviderResponseChangedException("OneDrive did not return a personal drive identifier.");
        _driveByItem[rootItem.Id] = rootItem.DriveId;
        if (!rootItem.IsFolder)
        {
            var file = new ManifestItem(rootItem.Id, rootItem.Name, ManifestItemKind.File, rootItem.Size, DownloadVariant.Binary);
            return new(Kind, SourceHint.File, rootItem.Name, new[] { file });
        }
        var output = new List<ManifestItem>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        await EnumerateAsync(rootItem.DriveId, rootItem.Id, string.Empty, output, visited, progress, cancellationToken);
        return new(Kind, SourceHint.Folder, rootItem.Name, output);
    }

    public async Task<DownloadLease> OpenDownloadAsync(ManifestItem item, CancellationToken cancellationToken)
    {
        if (!_driveByItem.TryGetValue(item.Id, out var driveId)) throw new CloudProviderException("session-expired", "Prepare this OneDrive link again before downloading.");
        var uri = ItemUri(driveId, item.Id);
        using var json = await GetJsonAsync(uri, cancellationToken);
        if (!json.RootElement.TryGetProperty("@content.downloadUrl", out var urlProperty) || !Uri.TryCreate(urlProperty.GetString(), UriKind.Absolute, out var contentUri))
            throw new DownloadDisabledException("Downloading this OneDrive item is disabled by its owner.");
        ValidateContentUri(contentUri);
        var response = await _client.GetAsync(contentUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        MapStatus(response);
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return new(stream, response.Content.Headers.ContentLength ?? item.Size, response.Content.Headers.ContentType?.MediaType, new ResponseOwner(response));
    }

    private async Task EnumerateAsync(string driveId, string itemId, string prefix, List<ManifestItem> output, HashSet<string> visited, IProgress<ManifestProgress>? progress, CancellationToken cancellationToken)
    {
        if (!visited.Add($"{driveId}|{itemId}")) return;
        Uri? page = ChildrenUri(driveId, itemId);
        while (page is not null)
        {
            ValidateApiUri(page);
            using var json = await GetJsonAsync(page, cancellationToken);
            if (!json.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
                throw new ProviderResponseChangedException("OneDrive returned an unexpected folder response.");
            foreach (var element in value.EnumerateArray())
            {
                var child = ParseItem(element);
                var childDrive = child.DriveId ?? driveId;
                var relative = string.IsNullOrEmpty(prefix) ? child.Name : Path.Combine(prefix, child.Name);
                _driveByItem[child.Id] = childDrive;
                if (child.IsFolder)
                {
                    output.Add(new(child.Id, relative, ManifestItemKind.Directory, null, null));
                    await EnumerateAsync(childDrive, child.Id, relative, output, visited, progress, cancellationToken);
                }
                else output.Add(new(child.Id, relative, ManifestItemKind.File, child.Size, DownloadVariant.Binary));
                progress?.Report(new(output.Count, relative));
            }
            page = null;
            if (json.RootElement.TryGetProperty("@odata.nextLink", out var next) && !string.IsNullOrWhiteSpace(next.GetString()))
            {
                if (!Uri.TryCreate(next.GetString(), UriKind.Absolute, out page)) throw new ProviderResponseChangedException("OneDrive returned an invalid continuation link.");
            }
        }
    }

    private async Task<(Uri RootUri, string Mode)> ResolveShareAsync(ParsedCloudLink link, CancellationToken cancellationToken)
    {
        var finalUri = link.OriginalUri;
        if (finalUri.IdnHost.Equals("1drv.ms", StringComparison.OrdinalIgnoreCase))
        {
            using var response = await _client.GetAsync(finalUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            MapStatus(response);
            finalUri = response.RequestMessage?.RequestUri ?? finalUri;
            if (finalUri.IdnHost.EndsWith(".sharepoint.com", StringComparison.OrdinalIgnoreCase)) throw new UnsupportedCloudItemException("OneDrive Business and SharePoint links are not supported.");
        }
        var query = ParseQuery(finalUri.Query);
        if (query.TryGetValue("redeem", out var redeem) && !string.IsNullOrWhiteSpace(redeem))
        {
            _badgerToken = await GetBadgerTokenAsync(cancellationToken);
            return (new Uri(_options.PersonalContentBaseUri, $"shares/u!{Uri.EscapeDataString(redeem)}/driveitem"), "badger");
        }
        var cid = query.GetValueOrDefault("cid");
        var resid = query.GetValueOrDefault("resid") ?? query.GetValueOrDefault("id");
        var authkey = query.GetValueOrDefault("authkey");
        if (!string.IsNullOrWhiteSpace(cid) && !string.IsNullOrWhiteSpace(resid) && !string.IsNullOrWhiteSpace(authkey))
        {
            _legacyCid = cid; _legacyAuthKey = authkey;
            return (new Uri(_options.LegacyApiBaseUri, $"drives/{Uri.EscapeDataString(cid)}/items/{Uri.EscapeDataString(resid)}?authkey={Uri.EscapeDataString(authkey)}"), "legacy");
        }
        throw new PrivateLinkException("This OneDrive Personal link is not public or has expired.");
    }

    private async Task<string> GetBadgerTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.BadgerTokenUri) { Content = new StringContent("{\"appId\":\"5cbed6ac-a083-4e14-b191-b4ba07653de2\"}", Encoding.UTF8, "application/json") };
        request.Headers.TryAddWithoutValidation("AppId", "1141147648");
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        MapStatus(response);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (json.RootElement.ValueKind == JsonValueKind.String) return json.RootElement.GetString()!;
        foreach (var name in new[] { "token", "accessToken" }) if (json.RootElement.TryGetProperty(name, out var property) && !string.IsNullOrWhiteSpace(property.GetString())) return property.GetString()!;
        throw new ProviderResponseChangedException("OneDrive did not return an anonymous session token.");
    }

    private async Task<JsonDocument> GetJsonAsync(Uri uri, CancellationToken cancellationToken)
    {
        ValidateApiUri(uri);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        AddSessionHeaders(request);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        MapStatus(response);
        try { return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken)); }
        catch (JsonException ex) { throw new ProviderResponseChangedException($"OneDrive returned incompatible data: {ex.Message}"); }
    }

    private void AddSessionHeaders(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_badgerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Badger", _badgerToken);
            request.Headers.TryAddWithoutValidation("Prefer", "autoredeem");
        }
    }

    private Uri ItemUri(string driveId, string itemId)
    {
        if (!string.IsNullOrEmpty(_badgerToken)) return new(_options.PersonalContentBaseUri, $"drives/{Uri.EscapeDataString(driveId)}/items/{Uri.EscapeDataString(itemId)}");
        return new(_options.LegacyApiBaseUri, $"drives/{Uri.EscapeDataString(_legacyCid ?? driveId)}/items/{Uri.EscapeDataString(itemId)}?authkey={Uri.EscapeDataString(_legacyAuthKey ?? string.Empty)}");
    }
    private Uri ChildrenUri(string driveId, string itemId)
    {
        var item = ItemUri(driveId, itemId);
        var builder = new UriBuilder(item) { Path = item.AbsolutePath.TrimEnd('/') + "/children" };
        return builder.Uri;
    }

    private static DriveItem ParseItem(JsonElement element)
    {
        if (!element.TryGetProperty("id", out var id) || !element.TryGetProperty("name", out var name)) throw new ProviderResponseChangedException("OneDrive returned an incomplete item.");
        var source = element.TryGetProperty("remoteItem", out var remote) ? remote : element;
        var driveId = source.TryGetProperty("parentReference", out var parent) && parent.TryGetProperty("driveId", out var drive) ? drive.GetString() : null;
        var actualId = source.TryGetProperty("id", out var remoteId) ? remoteId.GetString()! : id.GetString()!;
        var actualName = name.GetString() ?? actualId;
        var size = source.TryGetProperty("size", out var sizeProperty) && sizeProperty.TryGetInt64(out var parsedSize) ? parsedSize : (long?)null;
        return new(actualId, actualName, driveId, source.TryGetProperty("folder", out _), size);
    }

    private static void MapStatus(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound) { response.Dispose(); throw new PrivateLinkException("This OneDrive Personal link is not public or has expired."); }
        if ((int)response.StatusCode == 429) { response.Dispose(); throw new ProviderThrottledException("OneDrive is temporarily limiting requests."); }
        response.EnsureSuccessStatusCode();
    }
    private void ValidateApiUri(Uri uri)
    {
        var allowed = uri.IdnHost.Equals("my.microsoftpersonalcontent.com", StringComparison.OrdinalIgnoreCase)
            || uri.IdnHost.Equals("api.onedrive.com", StringComparison.OrdinalIgnoreCase)
            || uri == _options.BadgerTokenUri;
        if (uri.Scheme != Uri.UriSchemeHttps || !allowed) throw new ProviderResponseChangedException("OneDrive attempted to use an unexpected service host.");
    }
    private static void ValidateContentUri(Uri uri)
    {
        var host = uri.IdnHost;
        if (uri.Scheme != Uri.UriSchemeHttps || host.EndsWith(".sharepoint.com", StringComparison.OrdinalIgnoreCase)
            || !(host.EndsWith(".1drv.com", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".onedrive.com", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".livefilestore.com", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".microsoftusercontent.com", StringComparison.OrdinalIgnoreCase)))
            throw new ProviderResponseChangedException("OneDrive returned an unexpected content host.");
    }
    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)) { var split = pair.Split('=', 2); result[Uri.UnescapeDataString(split[0])] = split.Length == 2 ? Uri.UnescapeDataString(split[1].Replace('+', ' ')) : string.Empty; }
        return result;
    }
    public ValueTask DisposeAsync() { _client.Dispose(); return ValueTask.CompletedTask; }
    private sealed record DriveItem(string Id, string Name, string? DriveId, bool IsFolder, long? Size);
    private sealed class ResponseOwner(HttpResponseMessage response) : IAsyncDisposable { public ValueTask DisposeAsync() { response.Dispose(); return ValueTask.CompletedTask; } }
}
