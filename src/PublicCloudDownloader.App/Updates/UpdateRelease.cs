namespace PublicCloudDownloader.App.Updates;

public sealed record UpdateAsset(string Name, Uri DownloadUri, string Digest, long Size);

public sealed record UpdateRelease(Version Version, string Tag, string Notes, UpdateAsset Package);
