using PublicCloudDownloader.Core.Providers;

namespace PublicCloudDownloader.Tests.Providers;

public sealed class ProviderContractTests
{
    [Fact]
    public async Task DownloadLease_disposes_the_owned_stream()
    {
        var stream = new TrackingStream();
        await using (var lease = new DownloadLease(stream, 10, "text/plain")) { }
        Assert.True(stream.WasDisposed);
    }

    private sealed class TrackingStream : MemoryStream
    {
        public bool WasDisposed { get; private set; }
        protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
    }
}
