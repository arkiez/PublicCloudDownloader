using PublicCloudDownloader.App.Updates;

namespace PublicCloudDownloader.Tests.Updates;

public sealed class SelfUpdateRunnerTests
{
    private static readonly string[] PayloadFiles =
    [
        "PublicCloudDownloader.exe",
        "PublicCloudDownloader.ico",
        "README.txt",
        "THIRD-PARTY-NOTICES.md"
    ];

    [Fact]
    public async Task Apply_replaces_payload_preserves_runtime_and_restarts()
    {
        var root = TempRoot();
        var staging = Path.Combine(root, "staging");
        var install = Path.Combine(root, "install");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(install);
        SeedPayload(staging, "new");
        SeedPayload(install, "old");
        Directory.CreateDirectory(Path.Combine(install, "data"));
        Directory.CreateDirectory(Path.Combine(install, "logs"));
        File.WriteAllText(Path.Combine(install, "data", "user.db"), "userdata");
        File.WriteAllText(Path.Combine(install, "logs", "history.log"), "logdata");
        var process = new FakeProcessController();
        var runner = new SelfUpdateRunner(process);
        var restart = Path.Combine(install, "PublicCloudDownloader.exe");

        try
        {
            var exitCode = await runner.ApplyAsync(staging, install, 1234, restart, CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Equal(1234, process.WaitedProcessId);
            Assert.Equal(["wait", "start"], process.Events);
            Assert.Equal(restart, process.StartedExecutable);
            foreach (var name in PayloadFiles)
                Assert.Equal($"new-{name}", File.ReadAllText(Path.Combine(install, name)));
            Assert.Equal("userdata", File.ReadAllText(Path.Combine(install, "data", "user.db")));
            Assert.Equal("logdata", File.ReadAllText(Path.Combine(install, "logs", "history.log")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Missing_staged_payload_fails_without_replacing_or_restarting()
    {
        var root = TempRoot();
        var staging = Path.Combine(root, "staging");
        var install = Path.Combine(root, "install");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(install);
        SeedPayload(staging, "new");
        File.Delete(Path.Combine(staging, "README.txt"));
        SeedPayload(install, "old");
        Directory.CreateDirectory(Path.Combine(install, "data"));
        File.WriteAllText(Path.Combine(install, "data", "user.db"), "userdata");
        var process = new FakeProcessController();
        var runner = new SelfUpdateRunner(process);

        try
        {
            var exitCode = await runner.ApplyAsync(
                staging, install, 55, Path.Combine(install, "PublicCloudDownloader.exe"), CancellationToken.None);

            Assert.Equal(1, exitCode);
            Assert.Null(process.WaitedProcessId);
            Assert.Null(process.StartedExecutable);
            foreach (var name in PayloadFiles)
                Assert.Equal($"old-{name}", File.ReadAllText(Path.Combine(install, name)));
            Assert.Equal("userdata", File.ReadAllText(Path.Combine(install, "data", "user.db")));
        }
        finally { Directory.Delete(root, true); }
    }

    private static void SeedPayload(string root, string prefix)
    {
        foreach (var name in PayloadFiles)
            File.WriteAllText(Path.Combine(root, name), $"{prefix}-{name}");
    }

    private static string TempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "pcd-update-runner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeProcessController : IProcessController
    {
        public int? WaitedProcessId { get; private set; }
        public string? StartedExecutable { get; private set; }
        public List<string> Events { get; } = [];

        public Task WaitForExitAsync(int processId, CancellationToken cancellationToken)
        {
            WaitedProcessId = processId;
            Events.Add("wait");
            return Task.CompletedTask;
        }

        public void Start(string executablePath, string? arguments = null)
        {
            StartedExecutable = executablePath;
            Events.Add("start");
        }
    }
}
