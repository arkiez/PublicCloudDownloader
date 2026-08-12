using PublicCloudDownloader.Core.Downloads;

namespace PublicCloudDownloader.Infrastructure.Files;

public sealed class SafeFileWriter : IFileWriter
{
    public async Task WriteAsync(Stream source, string finalPath, bool overwrite, Guid jobId, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        var parent = Path.GetDirectoryName(finalPath) ?? throw new IOException("The destination path has no parent folder.");
        Directory.CreateDirectory(parent);
        var partial = Path.Combine(parent, $".{Path.GetFileName(finalPath)}.partial.{jobId:N}");
        var promoted = false;
        try
        {
            await using (var target = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[131072];
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    progress?.Report(read);
                }
                await target.FlushAsync(cancellationToken);
            }
            File.Move(partial, finalPath, overwrite);
            promoted = true;
        }
        finally { if (!promoted && File.Exists(partial)) File.Delete(partial); }
    }
}
