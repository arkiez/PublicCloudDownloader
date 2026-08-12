using System.Windows;
using PublicCloudDownloader.App.ViewModels;
using PublicCloudDownloader.Core.Downloads;
using PublicCloudDownloader.Core.Workflow;
using PublicCloudDownloader.Infrastructure.Files;
using PublicCloudDownloader.Infrastructure.Logging;
using PublicCloudDownloader.Infrastructure.Runtime;

namespace PublicCloudDownloader.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            try { _ = AppPaths.Data; _ = AppPaths.Logs; Environment.ExitCode = 0; }
            catch { Environment.ExitCode = 1; }
            Shutdown(Environment.ExitCode); return;
        }
        IDownloadWorkflow workflow = new DownloadWorkflow(new ProviderFactory(), new SafeFileWriter(), new SystemDelay(), new JobLog());
        new MainWindow(new MainViewModel(workflow)).Show();
    }
}
