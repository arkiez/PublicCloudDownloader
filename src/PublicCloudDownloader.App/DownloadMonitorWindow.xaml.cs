using System.Windows;
using PublicCloudDownloader.App.ViewModels;
using PublicCloudDownloader.Core.Downloads;
using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Workflow;

namespace PublicCloudDownloader.App;

public partial class DownloadMonitorWindow : Window
{
    private readonly MainViewModel _viewModel; private readonly PreparedDownload _prepared; private ExistingFilePolicy _policy; private CancellationTokenSource? _cancellation;
    public string FinalMessage { get; private set; } = "Download cancelled.";
    public DownloadMonitorWindow(MainViewModel viewModel, PreparedDownload prepared, ExistingFilePolicy policy) { InitializeComponent(); _viewModel = viewModel; _prepared = prepared; _policy = policy; Loaded += async (_, _) => await RunAsync(); }

    private async Task RunAsync()
    {
        _cancellation?.Dispose(); _cancellation = new();
        SetRunning(true); FailureList.Items.Clear(); FailureList.Visibility = Visibility.Collapsed;
        var progress = new Progress<DownloadProgress>(UpdateProgress);
        try
        {
            var result = await _viewModel.ExecuteAsync(_prepared, _policy, progress, _cancellation.Token);
            ShowResult(result);
        }
        catch (Exception ex) { Heading.Text = "Download stopped"; StatusText.Text = ex.Message; FinalMessage = "Download stopped with an error."; SetRunning(false); }
    }
    private void UpdateProgress(DownloadProgress p)
    {
        ProgressBar.IsIndeterminate = false; ProgressBar.Value = p.TotalFiles == 0 ? 100 : p.CompletedFiles * 100d / p.TotalFiles;
        StatusText.Text = p.Status; CurrentFileText.Text = p.CurrentRelativePath ?? string.Empty; CountText.Text = $"{p.CompletedFiles:N0} of {p.TotalFiles:N0}";
    }
    private void ShowResult(DownloadResult result)
    {
        SetRunning(false);
        if (result.Completion == DownloadCompletion.Cancelled) { Heading.Text = "Download cancelled"; StatusText.Text = "No incomplete files were kept."; FinalMessage = "Download cancelled."; return; }
        if (result.Failures.Count > 0)
        {
            Heading.Text = "Completed with errors"; StatusText.Text = $"{result.Downloaded:N0} downloaded, {result.Skipped:N0} skipped, {result.Failures.Count:N0} failed.";
            foreach (var failure in result.Failures) FailureList.Items.Add($"{failure.RelativePath} — {failure.Message}");
            FailureList.Visibility = Visibility.Visible; RetryButton.Visibility = Visibility.Visible; FinalMessage = "Download completed with errors.";
        }
        else { Heading.Text = "Download complete"; StatusText.Text = $"{result.Downloaded:N0} file{(result.Downloaded == 1 ? "" : "s")} downloaded, {result.Skipped:N0} skipped."; ProgressBar.Value = 100; FinalMessage = "Download complete."; }
    }
    private void SetRunning(bool running) { CancelButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed; CloseButton.Visibility = running ? Visibility.Collapsed : Visibility.Visible; if (running) RetryButton.Visibility = Visibility.Collapsed; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => _cancellation?.Cancel();
    private async void Retry_Click(object sender, RoutedEventArgs e) { _policy = ExistingFilePolicy.Overwrite; await RunAsync(); }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e) { if (CancelButton.Visibility == Visibility.Visible) { _cancellation?.Cancel(); e.Cancel = true; } base.OnClosing(e); }
}
