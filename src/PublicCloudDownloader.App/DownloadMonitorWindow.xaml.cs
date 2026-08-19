using System.IO;
using System.Windows;
using PublicCloudDownloader.App.Notifications;
using PublicCloudDownloader.App.ViewModels;
using PublicCloudDownloader.Core.Downloads;
using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Workflow;

namespace PublicCloudDownloader.App;

internal sealed record DownloadActivityEntry(string Text, string Kind);
internal sealed record ActiveDownloadDisplay(string FileName, string RelativePath, string StatusText);

public partial class DownloadMonitorWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly PreparedDownload _prepared;
    private readonly HashSet<string> _loggedActivities = new(StringComparer.OrdinalIgnoreCase);
    private readonly DownloadCompletionNotificationSession _notificationSession;
    private ExistingFilePolicy _policy;
    private CancellationTokenSource? _cancellation;

    public string FinalMessage { get; private set; } = "Download cancelled.";
    public string CompletionSummary { get; private set; } = string.Empty;

    public DownloadMonitorWindow(MainViewModel viewModel, PreparedDownload prepared, ExistingFilePolicy policy, DownloadCompletionNotificationSession notificationSession)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _prepared = prepared;
        _policy = policy;
        _notificationSession = notificationSession;
        Loaded += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        _cancellation?.Dispose();
        _cancellation = new();
        SetRunning(true);
        ActivityList.Items.Clear();
        _loggedActivities.Clear();
        var totalFiles = _prepared.Plan.Items.Count(x => x.Source.Kind == ManifestItemKind.File);
        ProgressBar.IsIndeterminate = false;
        ProgressBar.Value = 0;
        CountText.Text = $"0 of {totalFiles:N0} - 0%";

        var progress = new Progress<DownloadProgress>(UpdateProgress);
        try
        {
            var result = await _viewModel.ExecuteAsync(_prepared, _policy, progress, _cancellation.Token);
            ShowResult(result);
        }
        catch (Exception ex)
        {
            Heading.Text = "Download stopped";
            StatusText.Text = ex.Message;
            FinalMessage = "Download stopped with an error.";
            SetRunning(false);
        }
    }

    private void UpdateProgress(DownloadProgress progress)
    {
        ProgressBar.IsIndeterminate = false;
        ProgressBar.Value = progress.PercentComplete;
        StatusText.Text = progress.Status;
        CountText.Text = $"{progress.CompletedFiles:N0} of {progress.TotalFiles:N0} - {progress.PercentComplete:0}%";
        ActiveDownloadsList.ItemsSource = progress.ActiveDownloads.Select(ToDisplay).ToArray();
        if (progress.Status == "Downloaded")
            AppendActivity(progress.CurrentRelativePath, "Downloaded", "Downloaded");
        else if (progress.Status == "Skipped existing file")
            AppendActivity(progress.CurrentRelativePath, "Skipped", "Skipped");
    }

    private static ActiveDownloadDisplay ToDisplay(ActiveDownloadProgress item)
    {
        var fileName = Path.GetFileName(item.RelativePath);
        var status = item.PercentComplete.HasValue ? $"{item.Status} {item.PercentComplete.Value:0}%" : item.Status;
        return new(fileName, item.RelativePath, status);
    }

    private void AppendActivity(string? relativePath, string label, string kind)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;

        var eventKey = $"{relativePath}\0{kind}";
        if (!_loggedActivities.Add(eventKey)) return;

        var entry = new DownloadActivityEntry($"{relativePath} — {label}", kind);
        ActivityList.Items.Add(entry);
        ActivityList.ScrollIntoView(entry);
    }

    private void ShowResult(DownloadResult result)
    {
        SetRunning(false);
        if (result.Completion == DownloadCompletion.Cancelled)
        {
            Heading.Text = "Download cancelled";
            StatusText.Text = "No incomplete files were kept.";
            FinalMessage = "Download cancelled.";
            return;
        }

        if (result.Completion == DownloadCompletion.Failed)
        {
            var failure = result.Failures.FirstOrDefault();
            Heading.Text = "Download could not start";
            StatusText.Text = failure?.Message ?? "The output folder could not be created.";
            if (failure is not null) AppendActivity(failure.RelativePath, $"Failed: {failure.Message}", "Failed");
            FinalMessage = "Download could not start.";
            return;
        }

        if (result.Failures.Count > 0)
        {
            Heading.Text = "Completed with errors";
            StatusText.Text = $"{result.Downloaded:N0} downloaded, {result.Skipped:N0} skipped, {result.Failures.Count:N0} failed.";
            foreach (var failure in result.Failures)
                AppendActivity(failure.RelativePath, $"Failed: {failure.Message}", "Failed");
            RetryButton.Visibility = Visibility.Visible;
            FinalMessage = "Download completed with errors.";
        }
        else
        {
            Heading.Text = "Download complete";
            StatusText.Text = $"{result.Downloaded:N0} file{(result.Downloaded == 1 ? "" : "s")} downloaded, {result.Skipped:N0} skipped.";
            CompletionSummary = StatusText.Text;
            ProgressBar.Value = 100;
            FinalMessage = "Download complete.";
            _notificationSession.NotifyCleanCompletion(CompletionSummary);
        }
    }

    private void SetRunning(bool running)
    {
        CancelButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        CloseButton.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
        if (running) RetryButton.Visibility = Visibility.Collapsed;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cancellation?.Cancel();
    private async void Retry_Click(object sender, RoutedEventArgs e) => await RunAsync();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (CancelButton.Visibility == Visibility.Visible)
        {
            _cancellation?.Cancel();
            e.Cancel = true;
        }
        base.OnClosing(e);
    }
}
