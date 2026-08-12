namespace PublicCloudDownloader.Core.Downloads;

public sealed record DownloadFailure(string RelativePath, string Category, string Message);
public enum DownloadCompletion { Completed, CompletedWithErrors, Cancelled, Failed }
public sealed record DownloadResult(DownloadCompletion Completion, int Downloaded, int Skipped, IReadOnlyList<DownloadFailure> Failures);

public interface IDelay
{
    Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class SystemDelay : IDelay
{
    public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.Delay(delay, cancellationToken);
}
