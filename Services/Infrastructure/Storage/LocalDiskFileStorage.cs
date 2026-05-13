using System.Security.Cryptography;

namespace dwa_ver_val.Services.Infrastructure.Storage;

/// <summary>
/// Disk-backed IFileStorage. Root directory MUST be outside wwwroot so static
/// file middleware never serves uploads directly. Files are partitioned
/// year/month/{guid}{ext} for easy archival; relative paths use forward slashes
/// for cross-platform consistency.
/// </summary>
public class LocalDiskFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalDiskFileStorage(string rootDirectory)
    {
        _root = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFileResult> SaveAsync(
        Stream content, string contentType, string originalFileName, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var year = now.ToString("yyyy");
        var month = now.ToString("MM");
        var ext = Path.GetExtension(originalFileName);
        if (string.IsNullOrEmpty(ext)) ext = "";
        var fileName = Guid.NewGuid().ToString("N") + ext.ToLowerInvariant();
        var relativePath = $"{year}/{month}/{fileName}";
        var absolutePath = Path.Combine(_root, year, month, fileName);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        using var sha = SHA256.Create();
        long size = 0;
        await using (var fs = File.Create(absolutePath))
        await using (var crypto = new CryptoStream(fs, sha, CryptoStreamMode.Write))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await content.ReadAsync(buffer, ct)) > 0)
            {
                await crypto.WriteAsync(buffer.AsMemory(0, read), ct);
                size += read;
            }
        }

        var hashHex = Convert.ToHexString(sha.Hash!).ToLowerInvariant();

        return new StoredFileResult
        {
            RelativePath = relativePath,
            ContentType = contentType,
            SizeBytes = size,
            Sha256Hex = hashHex
        };
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct)
    {
        var absolutePath = ResolveAbsolute(relativePath);
        Stream stream = File.OpenRead(absolutePath);
        return Task.FromResult(stream);
    }

    public Task<bool> DeleteAsync(string relativePath, CancellationToken ct)
    {
        var absolutePath = ResolveAbsolute(relativePath);
        if (File.Exists(absolutePath)) File.Delete(absolutePath);
        return Task.FromResult(!File.Exists(absolutePath));
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken ct)
    {
        var absolutePath = ResolveAbsolute(relativePath);
        return Task.FromResult(File.Exists(absolutePath));
    }

    private string ResolveAbsolute(string relativePath)
    {
        // Reject path traversal attempts.
        if (relativePath.Contains("..") || Path.IsPathRooted(relativePath))
            throw new ArgumentException("Invalid relative path.", nameof(relativePath));

        var normalised = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_root, normalised);
    }
}
