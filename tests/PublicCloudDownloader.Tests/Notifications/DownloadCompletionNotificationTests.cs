using PublicCloudDownloader.App.Notifications;

namespace PublicCloudDownloader.Tests.Notifications;

public sealed class DownloadCompletionNotificationTests
{
    [Theory]
    [InlineData("Download complete.", true)]
    [InlineData("Download completed with errors.", false)]
    [InlineData("Download cancelled.", false)]
    [InlineData("Download could not start.", false)]
    public void Only_clean_completion_should_notify(string finalMessage, bool expected)
    {
        Assert.Equal(expected, DownloadCompletionNotificationPolicy.ShouldNotify(finalMessage));
    }

    [Fact]
    public void Notification_message_contains_summary_and_destination()
    {
        var message = DownloadCompletionNotificationPolicy.BuildMessage(
            "3 files downloaded, 0 skipped.", "C:\\Downloads");

        Assert.Contains("3 files downloaded", message);
        Assert.Contains("C:\\Downloads", message);
    }

    [Fact]
    public void Completion_session_notifies_immediately_and_only_once()
    {
        var notifier = new RecordingNotifier();
        var session = new DownloadCompletionNotificationSession(notifier, "C:\\Downloads");

        session.NotifyCleanCompletion("3 files downloaded, 0 skipped.");
        session.NotifyCleanCompletion("3 files downloaded, 0 skipped.");

        var call = Assert.Single(notifier.Calls);
        Assert.Equal("3 files downloaded, 0 skipped.", call.Summary);
        Assert.Equal("C:\\Downloads", call.Destination);
    }

    private sealed class RecordingNotifier : IDesktopNotifier
    {
        public List<(string Summary, string Destination)> Calls { get; } = [];

        public void ShowDownloadComplete(string summary, string destinationPath)
            => Calls.Add((summary, destinationPath));
    }
}
