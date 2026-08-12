using System.Text;
using PublicCloudDownloader.Infrastructure.Files;

namespace PublicCloudDownloader.Tests.Downloads;

public sealed class SafeFileWriterTests
{
    [Fact]
    public async Task Failed_overwrite_keeps_old_file_and_removes_partial()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcd-write-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var target = Path.Combine(root, "file.txt"); await File.WriteAllTextAsync(target, "old");
            await Assert.ThrowsAsync<IOException>(() => new SafeFileWriter().WriteAsync(new BrokenStream(), target, true, Guid.NewGuid(), null, default));
            Assert.Equal("old", await File.ReadAllTextAsync(target));
            Assert.Empty(Directory.GetFiles(root, "*.partial.*"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Successful_write_promotes_partial_and_replaces_old_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcd-write-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var target = Path.Combine(root, "file.txt"); await File.WriteAllTextAsync(target, "old");
            await new SafeFileWriter().WriteAsync(new MemoryStream("new"u8.ToArray()), target, true, Guid.NewGuid(), null, default);
            Assert.Equal("new", await File.ReadAllTextAsync(target));
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class BrokenStream : MemoryStream
    {
        public BrokenStream() : base(Encoding.UTF8.GetBytes("new-but-broken")) { }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position >= 3) throw new IOException("broken");
            var limited = buffer[..Math.Min(buffer.Length, 3 - (int)Position)];
            return await base.ReadAsync(limited, cancellationToken);
        }
    }
}
