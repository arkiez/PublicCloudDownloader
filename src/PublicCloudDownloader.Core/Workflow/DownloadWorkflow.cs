using PublicCloudDownloader.Core.Downloads;
using PublicCloudDownloader.Core.Links;
using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Planning;
using PublicCloudDownloader.Core.Providers;

namespace PublicCloudDownloader.Core.Workflow;

public interface IProviderFactory { IPublicCloudProvider Create(ProviderKind kind); }
public interface IJobLogger { Task WriteAsync(Guid jobId, ParsedCloudLink link, DownloadPlan plan, DownloadResult result, CancellationToken cancellationToken); }
public interface IDownloadWorkflow
{
    Task<PreparedDownload> PrepareAsync(string sourceLink, string destinationBase, IProgress<ManifestProgress>? progress, CancellationToken cancellationToken);
    Task<DownloadResult> ExecuteAsync(PreparedDownload prepared, ExistingFilePolicy policy, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken);
}

public sealed class DownloadWorkflow(IProviderFactory providerFactory, IFileWriter fileWriter, IDelay delay, IJobLogger? logger) : IDownloadWorkflow
{
    public async Task<PreparedDownload> PrepareAsync(string sourceLink, string destinationBase, IProgress<ManifestProgress>? progress, CancellationToken cancellationToken)
    {
        if (!CloudLinkParser.TryParse(sourceLink, out var link, out var error)) throw new CloudProviderException(error!.Code, error.Message);
        ValidateDestination(destinationBase);
        var provider = providerFactory.Create(link!.Provider);
        try
        {
            var manifest = await provider.BuildManifestAsync(link, progress, cancellationToken);
            var plan = WindowsPathPlanner.CreatePlan(manifest, destinationBase);
            return new(Guid.NewGuid(), link, manifest, plan, CollisionAnalyzer.Find(plan), provider);
        }
        catch { if (provider is IAsyncDisposable disposable) await disposable.DisposeAsync(); else if (provider is IDisposable sync) sync.Dispose(); throw; }
    }

    public async Task<DownloadResult> ExecuteAsync(PreparedDownload prepared, ExistingFilePolicy policy, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        var result = await new DownloadCoordinator(fileWriter, delay).RunAsync(prepared.Provider, prepared.Plan, policy, prepared.JobId, progress, cancellationToken);
        if (logger is not null) await logger.WriteAsync(prepared.JobId, prepared.ParsedLink, prepared.Plan, result, CancellationToken.None);
        return result;
    }

    private static void ValidateDestination(string destination)
    {
        if (!Directory.Exists(destination)) throw new DirectoryNotFoundException("Choose an existing local destination folder.");
        var probe = Path.Combine(destination, $".pcd-write-test-{Guid.NewGuid():N}.tmp");
        try { using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { throw new UnauthorizedAccessException("The selected destination folder is not writable.", ex); }
        finally { if (File.Exists(probe)) File.Delete(probe); }
    }
}

public sealed class PreparedDownload : IAsyncDisposable
{
    internal IPublicCloudProvider Provider { get; }
    public Guid JobId { get; }
    public ParsedCloudLink ParsedLink { get; }
    public PublicManifest Manifest { get; }
    public DownloadPlan Plan { get; }
    public IReadOnlyList<FileCollision> Conflicts { get; }
    internal PreparedDownload(Guid jobId, ParsedCloudLink parsedLink, PublicManifest manifest, DownloadPlan plan, IReadOnlyList<FileCollision> conflicts, IPublicCloudProvider provider)
        => (JobId, ParsedLink, Manifest, Plan, Conflicts, Provider) = (jobId, parsedLink, manifest, plan, conflicts, provider);
    public async ValueTask DisposeAsync() { if (Provider is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync(); else if (Provider is IDisposable disposable) disposable.Dispose(); }
}
