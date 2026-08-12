using PublicCloudDownloader.Core.Downloads;
using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Workflow;
using PublicCloudDownloader.Infrastructure.Runtime;
using System.Reflection;

namespace PublicCloudDownloader.Infrastructure.Logging;

public sealed class JobLog : IJobLogger
{
    public async Task WriteAsync(Guid jobId, ParsedCloudLink link, DownloadPlan plan, DownloadResult result, CancellationToken cancellationToken)
    {
        var file = Path.Combine(AppPaths.Logs, $"{DateTime.Now:yyyyMMdd-HHmmss}-{jobId:N}.log");
        var lines = new List<string>
        {
            $"Public Cloud Downloader {ProductVersion()}", $"Provider: {link.Provider}", $"Status: {result.Completion}",
            $"Downloaded: {result.Downloaded}", $"Skipped: {result.Skipped}", $"Errors: {result.Failures.Count}", "Files:"
        };
        lines.AddRange(plan.Items.Where(x => x.Source.Kind == ManifestItemKind.File).Select(x => "  " + x.RelativeOutputPath));
        lines.AddRange(result.Failures.Select(x => $"  ERROR [{x.Category}] {x.RelativePath}: {x.Message}"));
        await File.WriteAllLinesAsync(file, lines, cancellationToken);
    }

    private static string ProductVersion() => (typeof(JobLog).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown").Split('+')[0];
}
