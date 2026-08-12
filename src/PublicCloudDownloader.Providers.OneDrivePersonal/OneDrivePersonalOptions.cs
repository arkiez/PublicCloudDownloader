namespace PublicCloudDownloader.Providers.OneDrivePersonal;

public sealed class OneDrivePersonalOptions
{
    public Uri BadgerTokenUri { get; init; } = new("https://api-badgerp.svc.ms/v1.0/token");
    public Uri PersonalContentBaseUri { get; init; } = new("https://my.microsoftpersonalcontent.com/_api/v2.0/");
    public Uri LegacyApiBaseUri { get; init; } = new("https://api.onedrive.com/v1.0/");
    public string UserAgent { get; init; } = "PublicCloudDownloader/1.0 (+Windows)";
}
