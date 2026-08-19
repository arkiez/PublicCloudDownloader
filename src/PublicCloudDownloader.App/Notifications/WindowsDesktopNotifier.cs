using System.Drawing;
using System.Windows.Forms;

namespace PublicCloudDownloader.App.Notifications;

public sealed class WindowsDesktopNotifier : IDesktopNotifier
{
    private readonly object _gate = new();
    private readonly HashSet<NotifyIcon> _active = [];

    public void ShowDownloadComplete(string summary, string destinationPath)
    {
        var notification = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Visible = true,
            BalloonTipTitle = "Download complete",
            BalloonTipText = DownloadCompletionNotificationPolicy.BuildMessage(summary, destinationPath),
            BalloonTipIcon = ToolTipIcon.Info
        };

        lock (_gate) _active.Add(notification);
        notification.ShowBalloonTip(6000);
        _ = DisposeLaterAsync(notification);
    }

    private async Task DisposeLaterAsync(NotifyIcon notification)
    {
        await Task.Delay(7500);
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            await dispatcher.InvokeAsync(() => Dispose(notification));
        }
        else
        {
            Dispose(notification);
        }
    }

    private void Dispose(NotifyIcon notification)
    {
        lock (_gate) _active.Remove(notification);
        notification.Visible = false;
        notification.Dispose();
    }
}
