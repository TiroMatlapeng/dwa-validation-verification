namespace dwa_ver_val.Services.Infrastructure.Storage;

public class StoredFileResult
{
    public required string RelativePath { get; set; }
    public required string ContentType { get; set; }
    public required long SizeBytes { get; set; }
    public required string Sha256Hex { get; set; }
}
