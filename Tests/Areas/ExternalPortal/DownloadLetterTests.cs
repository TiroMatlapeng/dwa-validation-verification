using dwa_ver_val.Services.Letters;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class DownloadLetterTests
{
    [Fact]
    public async Task ReadAsync_MissingFile_ThrowsException()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var store = new FileSystemBlobStore(tempRoot);
            // File.ReadAllBytesAsync throws either FileNotFoundException or DirectoryNotFoundException
            // when the file path does not exist. Both inherit from IOException and should be caught by controllers.
            var ex = await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
                store.ReadAsync("letters/does-not-exist.pdf"));
            Assert.NotNull(ex);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
