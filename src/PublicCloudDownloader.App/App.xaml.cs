using System.Windows;
using System.IO;
using PublicCloudDownloader.App.ViewModels;
using PublicCloudDownloader.App.Updates;
using PublicCloudDownloader.App.Notifications;
using PublicCloudDownloader.Core.Downloads;
using PublicCloudDownloader.Core.Workflow;
using PublicCloudDownloader.Infrastructure.Files;
using PublicCloudDownloader.Infrastructure.Logging;
using PublicCloudDownloader.Infrastructure.Runtime;

namespace PublicCloudDownloader.App;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length > 0 && e.Args[0].Equals("--apply-update", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = RunApplyUpdate(e.Args);
            Shutdown(Environment.ExitCode); return;
        }
        if (e.Args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            Environment.ExitCode = AppSelfTest.Run(AppContext.BaseDirectory);
            Shutdown(Environment.ExitCode); return;
        }
        var headlessIndex = Array.FindIndex(e.Args, value => value.Equals("--headless-download", StringComparison.OrdinalIgnoreCase));
        if (headlessIndex >= 0)
        {
            Environment.ExitCode = headlessIndex + 2 < e.Args.Length ? RunHeadlessDownload(e.Args[headlessIndex + 1], e.Args[headlessIndex + 2]) : 2;
            Shutdown(Environment.ExitCode); return;
        }
        IDownloadWorkflow workflow = new DownloadWorkflow(new ProviderFactory(), new SafeFileWriter(), new SystemDelay(), new JobLog());
        var updateHttpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        var updateCoordinator = new UpdateUiCoordinator(new GitHubUpdateClient(updateHttpClient));
        var updatePackageService = new UpdatePackageService(updateHttpClient);
        new MainWindow(new MainViewModel(workflow), updateCoordinator, updatePackageService, new WindowsDesktopNotifier()).Show();
    }

    private static int RunApplyUpdate(string[] args)
    {
        if (args.Length != 5 || !int.TryParse(args[3], out var oldProcessId) || oldProcessId <= 0) return 2;
        return new SelfUpdateRunner(new SystemProcessController())
            .ApplyAsync(args[1], args[2], oldProcessId, args[4], CancellationToken.None)
            .GetAwaiter().GetResult();
    }
    private static int RunHeadlessDownload(string source, string destination)
    {
        try
        {
            Directory.CreateDirectory(destination);
            IDownloadWorkflow workflow = new DownloadWorkflow(new ProviderFactory(), new SafeFileWriter(), new SystemDelay(), new JobLog());
            return Task.Run(async () =>
            {
                await using var prepared = await workflow.PrepareAsync(source, destination, null, CancellationToken.None);
                var result = await workflow.ExecuteAsync(prepared, PublicCloudDownloader.Core.Models.ExistingFilePolicy.Overwrite, null, CancellationToken.None);
                return result.Completion == DownloadCompletion.Completed && result.Downloaded > 0 ? 0 : 1;
            }).GetAwaiter().GetResult();
        }
        catch { return 1; }
    }
}
