using System.Diagnostics;
using System.IO;
using System.Windows;
using PublicCloudDownloader.App.Updates;

namespace PublicCloudDownloader.App;

public partial class UpdatePromptWindow : Window
{
    private readonly UpdateRelease _release;
    private readonly UpdatePackageService _packageService;
    private bool _updating;

    public UpdatePromptWindow(UpdateRelease release, UpdatePackageService packageService)
    {
        InitializeComponent();
        _release = release;
        _packageService = packageService;
        CurrentVersionText.Text = typeof(UpdatePromptWindow).Assembly.GetName().Version?.ToString(3) ?? "Unknown";
        LatestVersionText.Text = release.Version.ToString(3);
        NotesText.Text = FormatNotes(release.Notes);
    }

    private static string FormatNotes(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return "A newer stable version is available.";
        var trimmed = notes.Trim();
        return trimmed.Length <= 1200 ? trimmed : trimmed[..1197] + "...";
    }

    private void Later_Click(object sender, RoutedEventArgs e) => Close();

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (_updating) return;
        _updating = true;
        UpdateButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;

        try
        {
            var progress = new Progress<double>(value =>
            {
                UpdateProgressBar.Value = value;
                ProgressText.Text = $"Downloading update… {value:0}%";
            });
            var staged = await _packageService.DownloadAndStageAsync(_release, progress, CancellationToken.None);
            ProgressText.Text = "Installing update…";

            var installDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var restartExecutable = Path.Combine(installDirectory, "PublicCloudDownloader.exe");
            var startInfo = new ProcessStartInfo(staged.ExecutablePath) { UseShellExecute = false };
            startInfo.ArgumentList.Add("--apply-update");
            startInfo.ArgumentList.Add(staged.RootPath);
            startInfo.ArgumentList.Add(installDirectory);
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
            startInfo.ArgumentList.Add(restartExecutable);
            if (Process.Start(startInfo) is null)
                throw new InvalidOperationException("The update process could not be started.");

            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this,
                $"The update could not be installed.\n\n{ex.Message}",
                "Update failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            ProgressText.Text = "Update failed.";
            UpdateButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
            _updating = false;
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_updating) e.Cancel = true;
        base.OnClosing(e);
    }
}
