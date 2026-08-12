using System.Windows;
using System.IO;
using System.Net.Http;
using Microsoft.Win32;
using PublicCloudDownloader.App.ViewModels;
using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Providers;
using PublicCloudDownloader.Core.Workflow;

namespace PublicCloudDownloader.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    public MainWindow(MainViewModel viewModel) { InitializeComponent(); DataContext = _viewModel = viewModel; }

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        if (Clipboard.ContainsText()) _viewModel.SourceLink = Clipboard.GetText().Trim();
        SourceLinkBox.Focus(); SourceLinkBox.CaretIndex = SourceLinkBox.Text.Length;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose where downloaded files will be saved", Multiselect = false, InitialDirectory = Directory.Exists(_viewModel.DestinationPath) ? _viewModel.DestinationPath : null };
        if (dialog.ShowDialog(this) == true) _viewModel.DestinationPath = dialog.FolderName;
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanDownload) return;
        _viewModel.IsBusy = true;
        using var cancellation = new CancellationTokenSource();
        try
        {
            var discovery = new Progress<ManifestProgress>(p => _viewModel.LinkStatus = $"Checking public access… {p.ItemsDiscovered} items found");
            await using var prepared = await _viewModel.PrepareAsync(discovery, cancellation.Token);
            var policy = ExistingFilePolicy.Overwrite;
            if (prepared.Conflicts.Count > 0)
            {
                var conflict = new ConflictDialog(prepared.Conflicts.Count) { Owner = this };
                if (conflict.ShowDialog() != true || conflict.Policy == ExistingFilePolicy.Cancel) { _viewModel.LinkStatus = "Download cancelled."; return; }
                policy = conflict.Policy;
            }
            var monitor = new DownloadMonitorWindow(_viewModel, prepared, policy) { Owner = this };
            monitor.ShowDialog();
            _viewModel.LinkStatus = monitor.FinalMessage;
        }
        catch (PrivateLinkException)
        {
            MessageBox.Show(this, "This folder or file is not public.\n\nIn the cloud service, set General access to ‘Anyone with the link’, then try again.", "Public Link Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            _viewModel.LinkStatus = "Public access could not be confirmed.";
        }
        catch (UnsupportedCloudItemException ex) { MessageBox.Show(this, ex.Message, "Unsupported Link", MessageBoxButton.OK, MessageBoxImage.Information); _viewModel.LinkStatus = ex.Message; }
        catch (Exception ex) { MessageBox.Show(this, FriendlyError(ex), "Download Could Not Start", MessageBoxButton.OK, MessageBoxImage.Error); _viewModel.LinkStatus = FriendlyError(ex); }
        finally { _viewModel.IsBusy = false; }
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
