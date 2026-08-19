using System.IO;
using System.Net.Http;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace PublicCloudDownloader.App.Updates;

public sealed class UpdatePackageService
{
    private static readonly HashSet<string> AllowedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "PublicCloudDownloader.exe",
        "PublicCloudDownloader.ico",
        "README.txt",
        "THIRD-PARTY-NOTICES.md"
    };

    private readonly HttpClient _httpClient;

    public UpdatePackageService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<StagedUpdate> DownloadAndStageAsync(
        UpdateRelease release,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var root = Path.Combine(Path.GetTempPath(), "PublicCloudDownloader", "updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var packagePath = Path.Combine(root, "package.zip");
            await DownloadVerifiedAsync(release, packagePath, progress, cancellationToken);
            ExtractValidatedPayload(packagePath, root);

            var executablePath = Path.Combine(root, "PublicCloudDownloader.exe");
            ValidateExecutableVersion(executablePath, release.Version);
            return new StagedUpdate(root, executablePath, release.Version);
        }
        catch (OperationCanceledException)
        {
            TryDelete(root);
            throw;
        }
        catch (UpdatePackageException)
        {
            TryDelete(root);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(root);
            throw new UpdatePackageException("The update package could not be prepared.", ex);
        }
    }

    private async Task DownloadVerifiedAsync(
        UpdateRelease release,
        string packagePath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            release.Package.DownloadUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new UpdatePackageException($"Update download returned HTTP {(int)response.StatusCode}.");
        }

        var expected = ParseExpectedDigest(release.Package.Digest);
        var total = response.Content.Headers.ContentLength ?? (release.Package.Size > 0 ? release.Package.Size : null);
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(packagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long received = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            hash.AppendData(buffer, 0, read);
            received += read;
            if (total is > 0) progress?.Report(Math.Min(100d, received * 100d / total.Value));
        }

        var actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new UpdatePackageException("Downloaded update failed SHA-256 verification.");
        }
        progress?.Report(100d);
    }

    private static void ExtractValidatedPayload(string packagePath, string root)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var rootPrefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            var normalized = entry.FullName.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalized))
                throw new UpdatePackageException("Update ZIP contains an invalid entry.");
            if (Path.IsPathRooted(entry.FullName) || normalized.Contains(':'))
                throw new UpdatePackageException("Update ZIP contains an unsafe path.");

            var target = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new UpdatePackageException("Update ZIP contains a path outside staging.");

            if (string.IsNullOrEmpty(entry.Name))
            {
                if (normalized is not ("data/" or "logs/"))
                    throw new UpdatePackageException($"Unexpected update directory: {normalized}");
                Directory.CreateDirectory(target);
                continue;
            }

            if (normalized.Contains('/') || !AllowedFiles.Contains(normalized))
                throw new UpdatePackageException($"Unexpected update file: {normalized}");

            using var source = entry.Open();
            using var destination = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            source.CopyTo(destination);
        }

        foreach (var name in AllowedFiles)
        {
            if (!File.Exists(Path.Combine(root, name)))
                throw new UpdatePackageException($"Update package is missing {name}.");
        }
    }

    private static void ValidateExecutableVersion(string executablePath, Version targetVersion)
    {
        var fileVersion = FileVersionInfo.GetVersionInfo(executablePath).FileVersion;
        if (!Version.TryParse(fileVersion, out var parsed) ||
            parsed.Major != targetVersion.Major ||
            parsed.Minor != targetVersion.Minor ||
            parsed.Build != targetVersion.Build)
        {
            throw new UpdatePackageException("Staged executable version does not match the selected release.");
        }
    }

    private static string ParseExpectedDigest(string digest)
    {
        if (digest is null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) || digest.Length != 71)
            throw new UpdatePackageException("Release SHA-256 digest is missing or malformed.");
        var hex = digest[7..];
        if (hex.Any(ch => !Uri.IsHexDigit(ch)))
            throw new UpdatePackageException("Release SHA-256 digest is missing or malformed.");
        return hex.ToLowerInvariant();
    }

    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch { }
    }
}
