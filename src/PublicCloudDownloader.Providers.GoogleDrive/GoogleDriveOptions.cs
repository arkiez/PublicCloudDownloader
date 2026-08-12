namespace PublicCloudDownloader.Providers.GoogleDrive;

public sealed class GoogleDriveOptions
{
    public Uri DriveBaseUri { get; init; } = new("https://drive.google.com/");
    public Uri DocsBaseUri { get; init; } = new("https://docs.google.com/");
    public string UserAgent { get; init; } = "PublicCloudDownloader/1.0 (+Windows)";
}
