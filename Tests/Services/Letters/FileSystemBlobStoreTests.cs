using dwa_ver_val.Services.Letters;
using Xunit;

namespace dwa_ver_val.Tests.Services.Letters;

public class FileSystemBlobStoreTests
{
    [Fact]
    public async Task WriteThenRead_RoundtripsBytes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "dws-blob-" + Guid.NewGuid());
        try
        {
            var sut = new FileSystemBlobStore(tempRoot);
            var path = await sut.WriteAsync("letters/test-A.pdf", new byte[] { 1, 2, 3, 4 });
            Assert.Equal("letters/test-A.pdf", path);
            var bytes = await sut.ReadAsync(path);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, bytes);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }
}
