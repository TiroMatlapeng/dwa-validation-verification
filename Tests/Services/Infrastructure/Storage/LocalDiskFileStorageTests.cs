using dwa_ver_val.Services.Infrastructure.Storage;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace dwa_ver_val.Tests.Services.Infrastructure.Storage;

public class LocalDiskFileStorageTests : IDisposable
{
    private readonly string _root;
    private readonly LocalDiskFileStorage _storage;

    public LocalDiskFileStorageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dwa-portal-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _storage = new LocalDiskFileStorage(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task SaveAsync_RoundTrips_ContentAndComputesSha256()
    {
        var bytes = Encoding.UTF8.GetBytes("hello portal");
        var expectedSha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        using var input = new MemoryStream(bytes);
        var saved = await _storage.SaveAsync(input, "application/pdf", "doc.pdf", CancellationToken.None);

        Assert.Equal("application/pdf", saved.ContentType);
        Assert.Equal(bytes.Length, saved.SizeBytes);
        Assert.Equal(expectedSha, saved.Sha256Hex);

        using var read = await _storage.OpenReadAsync(saved.RelativePath, CancellationToken.None);
        using var ms = new MemoryStream();
        await read.CopyToAsync(ms);
        Assert.Equal(bytes, ms.ToArray());
    }

    [Fact]
    public async Task SaveAsync_PartitionsByYearAndMonth()
    {
        using var input = new MemoryStream(new byte[] { 1, 2, 3 });
        var saved = await _storage.SaveAsync(input, "application/pdf", "x.pdf", CancellationToken.None);

        // RelativePath should look like "2026/05/<guid>.pdf"
        var parts = saved.RelativePath.Split('/', '\\');
        Assert.Equal(3, parts.Length);
        Assert.True(int.TryParse(parts[0], out var year) && year >= 2026);
        Assert.True(int.TryParse(parts[1], out var month) && month >= 1 && month <= 12);
        Assert.EndsWith(".pdf", parts[2]);
    }

    [Fact]
    public async Task ExistsAsync_TrueAfterSave_FalseAfterDelete()
    {
        using var input = new MemoryStream(new byte[] { 9 });
        var saved = await _storage.SaveAsync(input, "application/pdf", "x.pdf", CancellationToken.None);

        Assert.True(await _storage.ExistsAsync(saved.RelativePath, CancellationToken.None));

        await _storage.DeleteAsync(saved.RelativePath, CancellationToken.None);

        Assert.False(await _storage.ExistsAsync(saved.RelativePath, CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_PreservesPdfExtensionFromOriginalName()
    {
        using var input = new MemoryStream(new byte[] { 9 });
        var saved = await _storage.SaveAsync(input, "application/pdf", "title-deed.PDF", CancellationToken.None);
        Assert.EndsWith(".pdf", saved.RelativePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenReadAsync_ThrowsForUnknownPath()
    {
        await Assert.ThrowsAnyAsync<Exception>(() =>
            _storage.OpenReadAsync("does/not/exist.pdf", CancellationToken.None));
    }
}
