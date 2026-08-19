using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.Win32;
using PublicCloudDownloader.App.Notifications;
using PublicCloudDownloader.App.Updates;
using PublicCloudDownloader.App.ViewModels;
using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Providers;
using PublicCloudDownloader.Core.Workflow;

namespace PublicCloudDownloader.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly UpdateUiCoordinator _updateCoordinator;
    private readonly UpdatePackageService _updatePackageService;
    private readonly IDesktopNotifier _desktopNotifier;
    private UpdateRelease? _availableRelease;
    private bool _startupUpdateCheckStarted;

    public MainWindow(
        MainViewModel viewModel,
        UpdateUiCoordinator updateCoordinator,
        UpdatePackageService updatePackageService,
        IDesktopNotifier desktopNotifier)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _updateCoordinator = updateCoordinator;
        _updatePackageService = updatePackageService;
        _desktopNotifier = desktopNotifier;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_startupUpdateCheckStarted) return;
        _startupUpdateCheckStarted = true;
        await CheckForUpdatesAsync(userInitiated: false);
    }

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Clipboard.ContainsText()) _viewModel.SourceLink = System.Windows.Clipboard.GetText().Trim();
        SourceLinkBox.Focus();
        SourceLinkBox.CaretIndex = SourceLinkBox.Text.Length;
    }

    private void ClearSourceLink_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SourceLink = string.Empty;
        SourceLinkBox.Focus();
    }

    private void ClearDestinationPath_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.DestinationPath = string.Empty;
        DestinationPathBox.Focus();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose where downloaded files will be saved",
            Multiselect = false,
            InitialDirectory = Directory.Exists(_viewModel.DestinationPath) ? _viewModel.DestinationPath : null
        };
        if (dialog.ShowDialog(this) == true) _viewModel.DestinationPath = dialog.FolderName;
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanDownload) return;
        _viewModel.IsBusy = true;
        using var cancellation = new CancellationTokenSource();
        try
        {
            var discovery = new Progress<ManifestProgress>(p =>
                _viewModel.LinkStatus = $"Checking public access… {p.ItemsDiscovered} items found");
            await using var prepared = await _viewModel.PrepareAsync(discovery, cancellation.Token);
            var policy = ExistingFilePolicy.Skip;
            if (prepared.Conflicts.Count > 0)
            {
                var conflict = new ConflictDialog(prepared.Conflicts) { Owner = this };
                if (conflict.ShowDialog() != true || conflict.Policy == ExistingFilePolicy.Cancel)
                {
                    _viewModel.LinkStatus = "Download cancelled.";
                    return;
                }
                policy = conflict.Policy;
            }

            var notificationSession = new DownloadCompletionNotificationSession(_desktopNotifier, _viewModel.DestinationPath);
            var monitor = new DownloadMonitorWindow(_viewModel, prepared, policy, notificationSession) { Owner = this };
            monitor.ShowDialog();
            _viewModel.LinkStatus = monitor.FinalMessage;
        }
        catch (PrivateLinkException)
        {
            System.Windows.MessageBox.Show(this,
                "This folder or file is not public.\n\nIn the cloud service, set General access to ‘Anyone with the link’, then try again.",
                "Public Link Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            _viewModel.LinkStatus = "Public access could not be confirmed.";
        }
        catch (UnsupportedCloudItemException ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Unsupported Link", MessageBoxButton.OK, MessageBoxImage.Information);
            _viewModel.LinkStatus = ex.Message;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, FriendlyError(ex), "Download Could Not Start", MessageBoxButton.OK, MessageBoxImage.Error);
            _viewModel.LinkStatus = FriendlyError(ex);
        }
        finally { _viewModel.IsBusy = false; }
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        => await CheckForUpdatesAsync(userInitiated: true);

    private void UpdateAvailable_Click(object sender, RoutedEventArgs e)
    {
        if (_availableRelease is not null) ShowUpdatePrompt(_availableRelease);
    }

    private async Task CheckForUpdatesAsync(bool userInitiated)
    {
        CheckForUpdatesButton.IsEnabled = false;
        try
        {
            var result = await _updateCoordinator.CheckAsync(userInitiated, CancellationToken.None);
            if (result.Status == UpdateCheckStatus.Available && result.Release is not null)
            {
                SetAvailableRelease(result.Release);
                ShowUpdatePrompt(result.Release);
                return;
            }

            ClearAvailableRelease();
            if (!userInitiated) return;
            if (result.Status == UpdateCheckStatus.NoUpdate)
            {
                System.Windows.MessageBox.Show(this, "You're up to date.", "Check for updates",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show(this, "Could not check for updates. Check your internet connection and try again.",
                    "Check for updates", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (OperationCanceledException) { }
        finally { CheckForUpdatesButton.IsEnabled = true; }
    }

    private void SetAvailableRelease(UpdateRelease release)
    {
        _availableRelease = release;
        UpdateAvailableText.Text = $"Update v{release.Version.ToString(3)}";
        UpdateAvailableButton.Visibility = Visibility.Visible;
    }

    private void ClearAvailableRelease()
    {
        _availableRelease = null;
        UpdateAvailableButton.Visibility = Visibility.Collapsed;
    }

    private void ShowUpdatePrompt(UpdateRelease release)
    {
        var prompt = new UpdatePromptWindow(release, _updatePackageService) { Owner = this };
        prompt.ShowDialog();
    }

    private static string FriendlyError(Exception ex) => ex switch
    {
        ProviderResponseChangedException => "The cloud service returned a response this version does not recognize. An application update may be required.",
        CloudProviderException provider => provider.Message,
        UnauthorizedAccessException => "The selected destination folder is not writable.",
        DirectoryNotFoundException => "Choose an existing local destination folder.",
        HttpRequestException => "The cloud service could not be reached. Check your internet connection and try again.",
        _ => "The download could not be started. Check the link and destination, then try again."
    };
}
