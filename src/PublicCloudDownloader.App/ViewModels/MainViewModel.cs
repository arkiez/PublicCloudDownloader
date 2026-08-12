using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Reflection;
using PublicCloudDownloader.Core.Downloads;
using PublicCloudDownloader.Core.Links;
using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Workflow;

namespace PublicCloudDownloader.App.ViewModels;

public sealed class MainViewModel(IDownloadWorkflow workflow) : INotifyPropertyChanged
{
    private string _sourceLink = string.Empty;
    private string _destinationPath = DefaultDestination();
    private string _linkStatus = "Paste a public share link to begin.";
    private string _destinationStatus = "Ready to save here.";
    private bool _isBusy;

    public string SourceLink { get => _sourceLink; set { if (Set(ref _sourceLink, value)) Validate(); } }
    public string DestinationPath { get => _destinationPath; set { if (Set(ref _destinationPath, value)) Validate(); } }
    public string LinkStatus { get => _linkStatus; set => Set(ref _linkStatus, value); }
    public string DestinationStatus { get => _destinationStatus; private set => Set(ref _destinationStatus, value); }
    public bool IsBusy { get => _isBusy; set { if (Set(ref _isBusy, value)) OnPropertyChanged(nameof(CanDownload)); } }
    public bool CanDownload => !IsBusy && CloudLinkParser.TryParse(SourceLink, out _, out _) && IsWritableDestination(DestinationPath);
    public string VersionText => "Version " + (typeof(MainViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown").Split('+')[0];
    public event PropertyChangedEventHandler? PropertyChanged;

    public Task<PreparedDownload> PrepareAsync(IProgress<ManifestProgress>? progress, CancellationToken cancellationToken) => workflow.PrepareAsync(SourceLink, DestinationPath, progress, cancellationToken);
    public Task<DownloadResult> ExecuteAsync(PreparedDownload prepared, ExistingFilePolicy policy, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken) => workflow.ExecuteAsync(prepared, policy, progress, cancellationToken);

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(SourceLink)) LinkStatus = "Paste a public share link to begin.";
        else if (CloudLinkParser.TryParse(SourceLink, out var parsed, out var error)) LinkStatus = parsed!.Provider == ProviderKind.GoogleDrive ? "Google Drive link format recognized" : "OneDrive Personal link format recognized";
        else LinkStatus = error!.Message;
        DestinationStatus = !Directory.Exists(DestinationPath) ? "Choose an existing local folder."
            : !IsWritableDestination(DestinationPath) ? "This folder is not writable." : "Ready to save here.";
        OnPropertyChanged(nameof(CanDownload));
    }

    private static bool IsWritableDestination(string path)
    {
        if (!Directory.Exists(path)) return false;
        var probe = Path.Combine(path, $".pcd-ui-test-{Guid.NewGuid():N}.tmp");
        try { using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None); return true; }
        catch { return false; }
        finally { try { if (File.Exists(probe)) File.Delete(probe); } catch { } }
    }
    private static string DefaultDestination()
    {
        const string preferred = @"D:\Downloads";
        if (Directory.Exists(preferred)) return preferred;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; OnPropertyChanged(name); return true; }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
