using PublicCloudDownloader.Infrastructure.Runtime;

namespace PublicCloudDownloader.Tests.Runtime;

public sealed class AppSelfTestTests
{
    [Fact]
    public void Self_test_initializes_writable_portable_paths_without_opening_ui()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcd-self-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal(0, AppSelfTest.Run(root));
            Assert.True(Directory.Exists(Path.Combine(root, "data")));
            Assert.True(Directory.Exists(Path.Combine(root, "logs")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
