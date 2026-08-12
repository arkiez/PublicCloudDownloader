using System.Collections.Concurrent;
using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Providers;

namespace PublicCloudDownloader.Core.Downloads;

public sealed class DownloadCoordinator(IFileWriter fileWriter, IDelay delay, int maxConcurrency = 3)
{
    public async Task<DownloadResult> RunAsync(IPublicCloudProvider provider, DownloadPlan plan, ExistingFilePolicy policy, Guid jobId, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        if (policy == ExistingFilePolicy.Cancel) return new(DownloadCompletion.Cancelled, 0, 0, []);
        if (File.Exists(plan.OutputRoot)) return new(DownloadCompletion.Failed, 0, 0, [new(plan.OutputRoot, "path-conflict", "A file already occupies the planned output folder path.")]);
        Directory.CreateDirectory(plan.OutputRoot);
        foreach (var directory in plan.Items.Where(x => x.Source.Kind == ManifestItemKind.Directory)) Directory.CreateDirectory(directory.FinalPath);

        var files = plan.Items.Where(x => x.Source.Kind == ManifestItemKind.File).ToArray();
        var failures = new ConcurrentBag<DownloadFailure>();
        var downloaded = 0; var skipped = 0; var transferred = 0L; var completed = 0; var failed = 0;
        long? total = files.All(x => x.Source.Size.HasValue) ? files.Sum(x => x.Source.Size!.Value) : null;
        try
        {
            await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = maxConcurrency, CancellationToken = cancellationToken }, async (item, token) =>
            {
                if (File.Exists(item.FinalPath) && policy == ExistingFilePolicy.Skip)
                {
                    Interlocked.Increment(ref skipped); var done = Interlocked.Increment(ref completed);
                    progress?.Report(new(done, files.Length, Interlocked.Read(ref transferred), total, item.RelativeOutputPath, Volatile.Read(ref failed), "Skipped existing file"));
                    return;
                }
                Exception? last = null;
                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        await using var lease = await provider.OpenDownloadAsync(item.Source, token);
                        var byteProgress = new Progress<long>(value => { Interlocked.Add(ref transferred, value); progress?.Report(new(Volatile.Read(ref completed), files.Length, Interlocked.Read(ref transferred), total, item.RelativeOutputPath, Volatile.Read(ref failed), "Downloading")); });
                        await fileWriter.WriteAsync(lease.Content, item.FinalPath, policy == ExistingFilePolicy.Overwrite, jobId, byteProgress, token);
                        Interlocked.Increment(ref downloaded); var done = Interlocked.Increment(ref completed);
                        progress?.Report(new(done, files.Length, Interlocked.Read(ref transferred), total, item.RelativeOutputPath, Volatile.Read(ref failed), "Downloaded"));
                        return;
                    }
                    catch (Exception ex) when (IsTransient(ex, token) && attempt < 3)
                    {
                        last = ex;
                        await delay.WaitAsync(attempt == 1 ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromMilliseconds(1500), token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                    catch (Exception ex) { last = ex; break; }
                }
                Interlocked.Increment(ref failed); Interlocked.Increment(ref completed);
                failures.Add(new(item.RelativeOutputPath, Category(last), SafeMessage(last)));
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(DownloadCompletion.Cancelled, downloaded, skipped, failures.ToArray());
        }
        var resultFailures = failures.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray();
        return new(resultFailures.Length == 0 ? DownloadCompletion.Completed : DownloadCompletion.CompletedWithErrors, downloaded, skipped, resultFailures);
    }

    private static bool IsTransient(Exception ex, CancellationToken token) => ex is ProviderThrottledException or HttpRequestException || ex is TaskCanceledException && !token.IsCancellationRequested;
    private static string Category(Exception? ex) => ex is CloudProviderException provider ? provider.Code : ex is IOException ? "local-io" : "download-error";
    private static string SafeMessage(Exception? ex) => ex switch
    {
        CloudProviderException provider => provider.Message,
        UnauthorizedAccessException => "The destination is not writable.",
        IOException => "The file could not be written to the destination.",
        _ => "The file could not be downloaded."
    };
}
