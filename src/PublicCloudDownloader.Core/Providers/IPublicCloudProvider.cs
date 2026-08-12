using PublicCloudDownloader.Core.Models;

namespace PublicCloudDownloader.Core.Providers;

public interface IPublicCloudProvider
{
    ProviderKind Kind { get; }
    Task<PublicManifest> BuildManifestAsync(ParsedCloudLink link, IProgress<ManifestProgress>? progress, CancellationToken cancellationToken);
    Task<DownloadLease> OpenDownloadAsync(ManifestItem item, CancellationToken cancellationToken);
}

public sealed class DownloadLease(Stream content, long? length, string? contentType, IAsyncDisposable? owner = null) : IAsyncDisposable
{
    public Stream Content { get; } = content;
    public long? Length { get; } = length;
    public string? ContentType { get; } = contentType;

    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync();
        if (owner is not null) await owner.DisposeAsync();
    }
}

public class CloudProviderException(string code, string message, Exception? inner = null) : Exception(message, inner)
{
    public string Code { get; } = code;
}

public sealed class PrivateLinkException(string message) : CloudProviderException("not-public", message);
public sealed class UnsupportedCloudItemException(string message) : CloudProviderException("unsupported-item", message);
public sealed class ProviderResponseChangedException(string message) : CloudProviderException("response-changed", message);
public sealed class DownloadDisabledException(string message) : CloudProviderException("download-disabled", message);
public sealed class ProviderThrottledException(string message) : CloudProviderException("throttled", message);
