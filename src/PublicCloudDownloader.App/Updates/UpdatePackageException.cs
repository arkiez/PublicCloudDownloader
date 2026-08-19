namespace PublicCloudDownloader.App.Updates;

public sealed class UpdatePackageException : Exception
{
    public UpdatePackageException(string message, Exception? inner = null) : base(message, inner) { }
}

public sealed record StagedUpdate(string RootPath, string ExecutablePath, Version TargetVersion);
