using PublicCloudDownloader.App.Updates;

namespace PublicCloudDownloader.Tests.Updates;

public sealed class UpdateUiCoordinatorTests
{
    [Fact]
    public async Task Network_failure_returns_failed_without_throwing()
    {
        var coordinator = new UpdateUiCoordinator(new ThrowingClient());
        var result = await coordinator.CheckAsync(false, CancellationToken.None);
        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
    }

    [Fact]
    public async Task No_newer_release_returns_no_update()
    {
        var coordinator = new UpdateUiCoordinator(new FixedClient(null));
        var result = await coordinator.CheckAsync(true, CancellationToken.None);
        Assert.Equal(UpdateCheckStatus.NoUpdate, result.Status);
        Assert.True(result.UserInitiated);
    }

    [Fact]
    public async Task Available_release_is_returned_for_ui()
    {
        var release = SampleRelease(new Version(9, 0, 0));
        var coordinator = new UpdateUiCoordinator(new FixedClient(release));
        var result = await coordinator.CheckAsync(false, CancellationToken.None);
        Assert.Equal(UpdateCheckStatus.Available, result.Status);
        Assert.Same(release, result.Release);
    }

    [Fact]
    public async Task Checks_are_serialized_and_use_executing_version()
    {
        var client = new BlockingClient();
        var coordinator = new UpdateUiCoordinator(client);
        var first = coordinator.CheckAsync(false, CancellationToken.None);
        await client.FirstEntered.Task;
        var second = coordinator.CheckAsync(false, CancellationToken.None);

        Assert.Equal(1, client.MaxConcurrent);
        client.Release.TrySetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, client.MaxConcurrent);
        Assert.All(client.SeenVersions, version =>
            Assert.Equal(typeof(UpdateUiCoordinator).Assembly.GetName().Version, version));
    }

    private static UpdateRelease SampleRelease(Version version)
        => new(version, $"v{version.ToString(3)}", "Notes",
            new UpdateAsset($"PublicCloudDownloader-v{version.ToString(3)}-win-x64.zip",
                new Uri("https://example.test/app.zip"),
                "sha256:" + new string('a', 64), 1));

    private sealed class FixedClient(UpdateRelease? release) : IUpdateClient
    {
        public Task<UpdateRelease?> CheckAsync(Version currentVersion, CancellationToken cancellationToken)
            => Task.FromResult(release);
    }

    private sealed class ThrowingClient : IUpdateClient
    {
        public Task<UpdateRelease?> CheckAsync(Version currentVersion, CancellationToken cancellationToken)
            => throw new UpdateCheckException("network");
    }

    private sealed class BlockingClient : IUpdateClient
    {
        private int _active;
        public int MaxConcurrent { get; private set; }
        public List<Version> SeenVersions { get; } = [];
        public TaskCompletionSource FirstEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<UpdateRelease?> CheckAsync(Version currentVersion, CancellationToken cancellationToken)
        {
            SeenVersions.Add(currentVersion);
            var active = Interlocked.Increment(ref _active);
            MaxConcurrent = Math.Max(MaxConcurrent, active);
            FirstEntered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref _active);
            return null;
        }
    }
}
