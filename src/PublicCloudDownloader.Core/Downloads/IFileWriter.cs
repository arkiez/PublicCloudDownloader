namespace PublicCloudDownloader.Core.Downloads;

public interface IFileWriter
{
    Task WriteAsync(Stream source, string finalPath, bool overwrite, Guid jobId, IProgress<long>? progress, CancellationToken cancellationToken);
}
