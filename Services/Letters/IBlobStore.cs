namespace dwa_ver_val.Services.Letters;

/// <summary>
/// Abstracts letter-PDF storage so we can move from filesystem (dev) to Azure Blob (prod)
/// without touching LetterService or the templates.
/// </summary>
public interface IBlobStore
{
    /// <summary>Writes bytes to a logical path (e.g. "letters/VV-LIM-2026-0001-L1.pdf") and returns the storage path/URL.</summary>
    Task<string> WriteAsync(string logicalPath, byte[] bytes);

    /// <summary>Reads previously-written bytes by the path returned from <see cref="WriteAsync"/>.</summary>
    Task<byte[]> ReadAsync(string storagePath);
}

/// <summary>Filesystem-backed IBlobStore. Dev + test default.</summary>
public class FileSystemBlobStore : IBlobStore
{
    private readonly string _root;
    public FileSystemBlobStore(string root)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        Directory.CreateDirectory(_root);
    }

    public async Task<string> WriteAsync(string logicalPath, byte[] bytes)
    {
        var full = Path.Combine(_root, logicalPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, bytes);
        return logicalPath;  // stored path is the logical path; resolver composes root at read time
    }

    public Task<byte[]> ReadAsync(string storagePath)
    {
        var full = Path.Combine(_root, storagePath);
        return File.ReadAllBytesAsync(full);
    }
}
