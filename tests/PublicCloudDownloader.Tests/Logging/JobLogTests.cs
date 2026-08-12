using PublicCloudDownloader.Core.Downloads;
using PublicCloudDownloader.Core.Links;
using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Infrastructure.Logging;
using PublicCloudDownloader.Infrastructure.Runtime;

namespace PublicCloudDownloader.Tests.Logging;

public sealed class JobLogTests
{
    [Fact]
    public async Task Log_never_contains_the_source_link_or_anonymous_secrets()
    {
        var jobId = Guid.NewGuid();
        CloudLinkParser.TryParse("https://drive.google.com/drive/folders/1AbCdEfGhIjKlMnOpQrStUvWx?resourcekey=top-secret", out var link, out _);
        var item = new ManifestItem("safe-id", "safe.txt", ManifestItemKind.File, 1, DownloadVariant.Binary, DownloadUrl: "https://example.invalid/?tempauth=secret");
        var plan = new DownloadPlan(@"C:\Downloads", @"C:\Downloads\Safe", new[] { new PlannedItem(item, @"C:\Downloads\Safe\safe.txt", "safe.txt") });
        await new JobLog().WriteAsync(jobId, link!, plan, new(DownloadCompletion.Completed, 1, 0, []), default);
        var file = Directory.GetFiles(AppPaths.Logs, $"*-{jobId:N}.log").Single();
        try
        {
            var text = await File.ReadAllTextAsync(file);
            Assert.DoesNotContain("top-secret", text, StringComparison.Ordinal);
            Assert.DoesNotContain("tempauth", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("https://", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("safe.txt", text, StringComparison.Ordinal);
        }
        finally { File.Delete(file); }
    }
}
