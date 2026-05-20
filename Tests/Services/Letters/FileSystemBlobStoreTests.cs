using dwa_ver_val.Services.Letters;
using Xunit;

namespace dwa_ver_val.Tests.Services.Letters;

public class FileSystemBlobStoreTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public FileSystemBlobStoreTests() => Directory.CreateDirectory(_tempRoot);
    public void Dispose() { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); }

    private FileSystemBlobStore Sut() => new(_tempRoot);

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("../../appsettings.json")]
    [InlineData("letters/../../../etc/passwd")]
    public async Task ReadAsync_PathTraversal_ThrowsArgumentException(string path)
    {
        var sut = Sut();
        await Assert.ThrowsAsync<ArgumentException>(() => sut.ReadAsync(path));
    }

    [Theory]
    [InlineData("../evil.pdf")]
    [InlineData("../../config.json")]
    public async Task WriteAsync_PathTraversal_ThrowsArgumentException(string path)
    {
        var sut = Sut();
        await Assert.ThrowsAsync<ArgumentException>(() => sut.WriteAsync(path, new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public async Task ReadAsync_RootedPath_ThrowsArgumentException()
    {
        var sut = Sut();
        await Assert.ThrowsAsync<ArgumentException>(() => sut.ReadAsync("/etc/passwd"));
    }

    [Fact]
    public async Task WriteAndRead_LegalPath_RoundTrips()
    {
        var sut = Sut();
        var data = new byte[] { 10, 20, 30, 40 };
        var storedPath = await sut.WriteAsync("letters/test.pdf", data);
        var read = await sut.ReadAsync(storedPath);
        Assert.Equal(data, read);
    }

    [Fact]
    public async Task ReadAsync_EmptyPath_ThrowsArgumentException()
    {
        var sut = Sut();
        await Assert.ThrowsAsync<ArgumentException>(() => sut.ReadAsync(""));
    }

    [Fact]
    public async Task WriteAsync_EmptyPath_ThrowsArgumentException()
    {
        var sut = Sut();
        await Assert.ThrowsAsync<ArgumentException>(() => sut.WriteAsync("", new byte[] { 1 }));
    }
}
