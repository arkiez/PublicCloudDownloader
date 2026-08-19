using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PublicCloudDownloader.App.Updates;

public sealed class GitHubUpdateClient : IUpdateClient
{
    private static readonly Uri LatestReleaseUri = new(
        "https://api.github.com/repos/arkiez/PublicCloudDownloader/releases/latest");
    private static readonly Regex DigestPattern = new(
        "^sha256:[0-9a-fA-F]{64}$",
        RegexOptions.CultureInvariant);
    private readonly HttpClient _httpClient;

    public GitHubUpdateClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            var version = typeof(GitHubUpdateClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PublicCloudDownloader", version));
        }
    }

    public async Task<UpdateRelease?> CheckAsync(Version currentVersion, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(LatestReleaseUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new UpdateCheckException($"GitHub returned HTTP {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            var tag = RequiredString(root, "tag_name");
            var draft = RequiredBoolean(root, "draft");
            var prerelease = RequiredBoolean(root, "prerelease");
            if (draft || prerelease || !UpdateVersion.IsNewer(tag, currentVersion)) return null;
            if (!UpdateVersion.TryParseTag(tag, out var version)) return null;

            var notes = root.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String
                ? body.GetString() ?? string.Empty
                : string.Empty;
            var expectedName = $"PublicCloudDownloader-v{version.ToString(3)}-win-x64.zip";
            if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            {
                throw new UpdateCheckException("Release assets are missing.");
            }

            foreach (var asset in assets.EnumerateArray())
            {
                if (!string.Equals(RequiredString(asset, "name"), expectedName, StringComparison.Ordinal)) continue;
                var digest = RequiredString(asset, "digest");
                if (!DigestPattern.IsMatch(digest))
                {
                    throw new UpdateCheckException("Release package SHA-256 digest is missing or malformed.");
                }

                var downloadUrl = RequiredString(asset, "browser_download_url");
                if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var downloadUri))
                {
                    throw new UpdateCheckException("Release package download URL is invalid.");
                }

                var size = asset.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var parsedSize)
                    ? parsedSize
                    : 0;
                return new UpdateRelease(version, tag, notes,
                    new UpdateAsset(expectedName, downloadUri, digest, size));
            }

            throw new UpdateCheckException($"Release package {expectedName} was not found.");
        }
        catch (UpdateCheckException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new UpdateCheckException("Could not read update information from GitHub.", ex);
        }
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new UpdateCheckException($"Release metadata is missing {propertyName}.");
        }
        return value.GetString() ?? string.Empty;
    }

    private static bool RequiredBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) ||
            (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False))
        {
            throw new UpdateCheckException($"Release metadata is missing {propertyName}.");
        }
        return value.GetBoolean();
    }
}
