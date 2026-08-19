using System.Diagnostics;
using System.IO;

namespace PublicCloudDownloader.App.Updates;

public interface IProcessController
{
    Task WaitForExitAsync(int processId, CancellationToken cancellationToken);
    void Start(string executablePath, string? arguments = null);
}

public sealed class SystemProcessController : IProcessController
{
    public async Task WaitForExitAsync(int processId, CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (ArgumentException)
        {
            // The old process already exited.
        }
    }

    public void Start(string executablePath, string? arguments = null)
    {
        Process.Start(new ProcessStartInfo(executablePath, arguments ?? string.Empty)
        {
            UseShellExecute = true
        });
    }
}

public sealed class SelfUpdateRunner
{
    private static readonly string[] PayloadFiles =
    [
        "PublicCloudDownloader.exe",
        "PublicCloudDownloader.ico",
        "README.txt",
        "THIRD-PARTY-NOTICES.md"
    ];

    private readonly IProcessController _processController;

    public SelfUpdateRunner(IProcessController processController)
        => _processController = processController;

    public async Task<int> ApplyAsync(
        string stagingRoot,
        string installDirectory,
        int oldProcessId,
        string restartExecutable,
        CancellationToken cancellationToken)
    {
        try
        {
            var staging = Path.GetFullPath(stagingRoot);
            var install = Path.GetFullPath(installDirectory);
            if (!Directory.Exists(staging) || oldProcessId <= 0) return 1;
            foreach (var name in PayloadFiles)
                if (!File.Exists(Path.Combine(staging, name))) return 1;

            Directory.CreateDirectory(install);
            await _processController.WaitForExitAsync(oldProcessId, cancellationToken);
            return ReplacePayloadAndRestart(staging, install, restartExecutable);
        }
        catch (OperationCanceledException) { return 1; }
        catch { return 1; }
    }

    private int ReplacePayloadAndRestart(string staging, string install, string restartExecutable)
    {
        var replaced = new List<string>();
        try
        {
            foreach (var name in PayloadFiles)
            {
                var source = Path.Combine(staging, name);
                var temp = Path.Combine(install, $".{name}.update-new");
                var backup = Path.Combine(install, $".{name}.update-old");
                if (File.Exists(temp)) File.Delete(temp);
                if (File.Exists(backup)) File.Delete(backup);
                File.Copy(source, temp, true);

                var destination = Path.Combine(install, name);
                if (File.Exists(destination)) File.Copy(destination, backup, true);
            }

            foreach (var name in PayloadFiles)
            {
                var destination = Path.Combine(install, name);
                var temp = Path.Combine(install, $".{name}.update-new");
                File.Move(temp, destination, true);
                replaced.Add(name);
            }

            CleanupSidecars(install);
            _processController.Start(Path.GetFullPath(restartExecutable));
            return 0;
        }
        catch
        {
            RollBack(install, replaced);
            CleanupSidecars(install);
            return 1;
        }
    }

    private static void RollBack(string install, IEnumerable<string> replaced)
    {
        foreach (var name in replaced.Reverse())
        {
            var destination = Path.Combine(install, name);
            var backup = Path.Combine(install, $".{name}.update-old");
            try
            {
                if (File.Exists(backup)) File.Move(backup, destination, true);
                else if (File.Exists(destination)) File.Delete(destination);
            }
            catch { }
        }
    }

    private static void CleanupSidecars(string install)
    {
        foreach (var name in PayloadFiles)
        {
            foreach (var suffix in new[] { "update-new", "update-old" })
            {
                var path = Path.Combine(install, $".{name}.{suffix}");
                try { if (File.Exists(path)) File.Delete(path); }
                catch { }
            }
        }
    }
}
