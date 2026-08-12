namespace PublicCloudDownloader.Infrastructure.Runtime;

public static class AppPaths
{
    public static string Root => AppContext.BaseDirectory;
    public static string Data => Ensure("data");
    public static string Logs => Ensure("logs");
    private static string Ensure(string name) { var path = Path.Combine(Root, name); Directory.CreateDirectory(path); return path; }
}
