namespace PublicCloudDownloader.App.Notifications;

public interface IDesktopNotifier
{
    void ShowDownloadComplete(string summary, string destinationPath);
}

public static class DownloadCompletionNotificationPolicy
{
    public static bool ShouldNotify(string finalMessage)
        => string.Equals(finalMessage, "Download complete.", StringComparison.Ordinal);

    public static string BuildMessage(string summary, string destinationPath)
    {
        var message = $"{summary}\nSaved to: {destinationPath}";
        return message.Length <= 240 ? message : message[..237] + "...";
    }
}
public sealed class DownloadCompletionNotificationSession
{
    private readonly IDesktopNotifier _notifier;
    private readonly string _destinationPath;
    private int _notified;

    public DownloadCompletionNotificationSession(IDesktopNotifier notifier, string destinationPath)
    {
        _notifier = notifier;
        _destinationPath = destinationPath;
    }

    public bool NotifyCleanCompletion(string summary)
    {
        if (Interlocked.Exchange(ref _notified, 1) != 0) return false;
        _notifier.ShowDownloadComplete(summary, _destinationPath);
        return true;
    }
}
