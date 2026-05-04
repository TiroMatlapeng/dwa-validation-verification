namespace dwa_ver_val.Services.Infrastructure.Storage;

public interface IFileStorage
{
    /// <summary>
    /// Persists the stream to storage and returns the relative path
    /// (relative to the storage root, NEVER an absolute filesystem path).
    /// Computes SHA-256 of the bytes during the write.
    /// </summary>
    Task<StoredFileResult> SaveAsync(
        Stream content, string contentType, string originalFileName, CancellationToken ct);

    /// <summary>Opens a read stream for a previously saved file. Caller disposes.</summary>
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct);

    /// <summary>Idempotent: returns true if the file is now gone.</summary>
    Task<bool> DeleteAsync(string relativePath, CancellationToken ct);

    Task<bool> ExistsAsync(string relativePath, CancellationToken ct);
}
