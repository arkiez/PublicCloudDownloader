namespace PublicCloudDownloader.Core.Models;

public enum ProviderKind { GoogleDrive, OneDrivePersonal }
public enum SourceHint { Unknown, File, Folder }
public enum ManifestItemKind { File, Directory }
public enum DownloadVariant { Binary, GoogleDocument, GoogleSpreadsheet, GooglePresentation, DirectUrl }
public enum ExistingFilePolicy { Cancel, Skip, Overwrite }

public sealed record ParsedCloudLink(
    ProviderKind Provider,
    Uri OriginalUri,
    string ShareId,
    SourceHint Hint,
    string? ResourceKey = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record LinkParseError(string Code, string Message);

public sealed record ManifestItem(
    string Id,
    string RelativePath,
    ManifestItemKind Kind,
    long? Size,
    DownloadVariant? Variant,
    string? DownloadUrl = null,
    string? ResourceKey = null);

public sealed record PublicManifest(
    ProviderKind Provider,
    SourceHint SourceKind,
    string RootName,
    IReadOnlyList<ManifestItem> Items);

public sealed record PlannedItem(ManifestItem Source, string FinalPath, string RelativeOutputPath);

public sealed record DownloadPlan(string Destination, string OutputRoot, IReadOnlyList<PlannedItem> Items);

public sealed record FileCollision(PlannedItem Item, long ExistingLength);

public sealed record ManifestProgress(int ItemsDiscovered, string? CurrentPath);

public sealed record DownloadProgress(
    int CompletedItems,
    int TotalItems,
    long CompletedBytes,
    long? TotalBytes,
    string? CurrentPath,
    string Status);
