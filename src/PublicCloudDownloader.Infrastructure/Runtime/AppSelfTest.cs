namespace PublicCloudDownloader.Infrastructure.Runtime;

public static class AppSelfTest
{
    public static int Run(string root)
    {
        try
        {
            foreach (var name in new[] { "data", "logs" })
            {
                var directory = Path.Combine(root, name); Directory.CreateDirectory(directory);
                var probe = Path.Combine(directory, $".self-test-{Guid.NewGuid():N}.tmp");
                File.WriteAllText(probe, "ok"); File.Delete(probe);
            }
            using var googleHandler = new HttpClientHandler();
            using var oneDriveHandler = new HttpClientHandler();
            return 0;
        }
        catch { return 1; }
    }
}
