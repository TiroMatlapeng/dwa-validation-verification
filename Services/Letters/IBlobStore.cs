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
        _root = Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root)));
        Directory.CreateDirectory(_root);
    }

    public async Task<string> WriteAsync(string logicalPath, byte[] bytes)
    {
        GuardPath(logicalPath, nameof(logicalPath));
        var full = Path.GetFullPath(Path.Combine(_root, logicalPath.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !full.Equals(_root, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Path escapes storage root.", nameof(logicalPath));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, bytes);
        return logicalPath;  // stored path is the logical path; resolver composes root at read time
    }

    public async Task<byte[]> ReadAsync(string storagePath)
    {
        GuardPath(storagePath, nameof(storagePath));
        var full = Path.GetFullPath(Path.Combine(_root, storagePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !full.Equals(_root, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Path escapes storage root.", nameof(storagePath));
        return await File.ReadAllBytesAsync(full);
    }

    private static void GuardPath(string path, string paramName)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Path must not be empty.", paramName);
        if (path.Contains(".."))
            throw new ArgumentException("Path must not contain '..'.", paramName);
        if (Path.IsPathRooted(path))
            throw new ArgumentException("Path must be relative.", paramName);
    }
}
