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
}
