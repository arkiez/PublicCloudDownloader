namespace PublicCloudDownloader.App.Updates;

public interface IUpdateClient
{
    Task<UpdateRelease?> CheckAsync(Version currentVersion, CancellationToken cancellationToken);
}

public sealed class UpdateCheckException : Exception
{
    public UpdateCheckException(string message, Exception? inner = null) : base(message, inner) { }
}
