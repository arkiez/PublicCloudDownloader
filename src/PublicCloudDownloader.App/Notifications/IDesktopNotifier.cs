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
