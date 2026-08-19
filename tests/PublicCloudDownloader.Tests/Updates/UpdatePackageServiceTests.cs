using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using PublicCloudDownloader.App.Updates;

namespace PublicCloudDownloader.Tests.Updates;

public sealed class UpdatePackageServiceTests
{
    [Fact]
    public async Task Valid_package_is_verified_staged_and_reports_completion()
    {
        var zip = BuildZip();
        var release = Release(zip, CurrentVersion);
        var progress = new CaptureProgress();
        var service = new UpdatePackageService(new HttpClient(new BytesHandler(zip)));

        var staged = await service.DownloadAndStageAsync(
            release, progress, CancellationToken.None);

        try
        {
            Assert.True(File.Exists(staged.ExecutablePath));
            Assert.Equal(CurrentVersion, staged.TargetVersion);
            Assert.Contains(progress.Values, value => value >= 100);
        }
        finally { Directory.Delete(staged.RootPath, true); }
    }

    [Fact]
    public async Task Wrong_digest_prevents_staging()
    {
        var zip = BuildZip();
        var release = Release(zip, CurrentVersion) with
        {
            Package = Release(zip, CurrentVersion).Package with
            {
                Digest = "sha256:" + new string('0', 64)
            }
        };
        var service = new UpdatePackageService(new HttpClient(new BytesHandler(zip)));

        await Assert.ThrowsAsync<UpdatePackageException>(() =>
            service.DownloadAndStageAsync(release, null, CancellationToken.None));
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("C:/escape.txt")]
    public async Task Unsafe_zip_paths_are_rejected(string entryName)
    {
        var zip = BuildZip(entryName);
        var service = new UpdatePackageService(new HttpClient(new BytesHandler(zip)));
        await Assert.ThrowsAsync<UpdatePackageException>(() =>
            service.DownloadAndStageAsync(Release(zip, CurrentVersion), null, CancellationToken.None));
    }

    [Fact]
    public async Task Unexpected_top_level_file_is_rejected()
    {
        var zip = BuildZip("extra.dll");
        var service = new UpdatePackageService(new HttpClient(new BytesHandler(zip)));
        await Assert.ThrowsAsync<UpdatePackageException>(() =>
            service.DownloadAndStageAsync(Release(zip, CurrentVersion), null, CancellationToken.None));
    }

    [Fact]
    public async Task Missing_executable_is_rejected()
    {
        var zip = BuildZip(includeExecutable: false);
        var service = new UpdatePackageService(new HttpClient(new BytesHandler(zip)));
        await Assert.ThrowsAsync<UpdatePackageException>(() =>
            service.DownloadAndStageAsync(Release(zip, CurrentVersion), null, CancellationToken.None));
    }

    [Fact]
    public async Task Executable_version_must_match_release()
    {
        var zip = BuildZip();
        var service = new UpdatePackageService(new HttpClient(new BytesHandler(zip)));
        await Assert.ThrowsAsync<UpdatePackageException>(() =>
            service.DownloadAndStageAsync(Release(zip, MismatchedVersion), null, CancellationToken.None));
    }

    private static Version CurrentVersion =>
        new(typeof(PublicCloudDownloader.App.App).Assembly.GetName().Version!.Major,
            typeof(PublicCloudDownloader.App.App).Assembly.GetName().Version!.Minor,
            typeof(PublicCloudDownloader.App.App).Assembly.GetName().Version!.Build);

    private static Version MismatchedVersion =>
        new(CurrentVersion.Major, CurrentVersion.Minor, CurrentVersion.Build + 1);

    private static UpdateRelease Release(byte[] zip, Version version)
    {
        var digest = Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant();
        var name = $"PublicCloudDownloader-v{version.ToString(3)}-win-x64.zip";
        return new UpdateRelease(version, $"v{version.ToString(3)}", string.Empty,
            new UpdateAsset(name, new Uri("https://example.test/update.zip"), $"sha256:{digest}", zip.Length));
    }

    private static byte[] BuildZip(string? extraEntry = null, bool includeExecutable = true)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            if (includeExecutable)
            {
                var exeEntry = archive.CreateEntry("PublicCloudDownloader.exe");
                using var target = exeEntry.Open();
                using var source = File.OpenRead(typeof(PublicCloudDownloader.App.App).Assembly.Location);
                source.CopyTo(target);
            }
            AddText(archive, "PublicCloudDownloader.ico", "icon");
            AddText(archive, "README.txt", "readme");
            AddText(archive, "THIRD-PARTY-NOTICES.md", "notices");
            archive.CreateEntry("data/");
            archive.CreateEntry("logs/");
            if (extraEntry is not null) AddText(archive, extraEntry, "bad");
        }
        return memory.ToArray();
    }

    private static void AddText(ZipArchive archive, string name, string value)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(value);
    }

    private sealed class CaptureProgress : IProgress<double>
    {
        public List<double> Values { get; } = [];
        public void Report(double value) => Values.Add(value);
    }
    private sealed class BytesHandler(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new ByteArrayContent(body);
            content.Headers.ContentLength = body.Length;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            });
        }
    }
}
