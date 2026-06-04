namespace dwa_ver_val.Services.Documents;

/// <summary>
/// Validates that a file's leading magic bytes match its claimed extension.
/// Prevents content-type spoofing where, e.g., an executable is uploaded with a .pdf name.
///
/// DOC-02: real virus scanning is deferred (no IVirusScanner wired).
/// Until an IVirusScanner sets "Clean"/"Infected", only the magic-byte check runs at upload time.
/// </summary>
public static class FileSignatureValidator
{
    // PDF: %PDF
    private static readonly byte[] PdfSig = { 0x25, 0x50, 0x44, 0x46 };

    // PNG: 89 50 4E 47 0D 0A 1A 0A
    private static readonly byte[] PngSig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    // JPEG/JPG: FF D8 FF
    private static readonly byte[] JpgSig = { 0xFF, 0xD8, 0xFF };

    // TIFF little-endian: 49 49 2A 00
    private static readonly byte[] TiffLeSig = { 0x49, 0x49, 0x2A, 0x00 };

    // TIFF big-endian: 4D 4D 00 2A
    private static readonly byte[] TiffBeSig = { 0x4D, 0x4D, 0x00, 0x2A };

    /// <summary>
    /// Returns <see langword="true"/> if the leading bytes of <paramref name="content"/>
    /// match the magic signature expected for <paramref name="extension"/>.
    /// Always resets <paramref name="content"/>.Position to 0 before returning,
    /// so the caller's subsequent read/save still sees the full stream.
    /// Unknown or empty extensions always return <see langword="false"/>.
    /// </summary>
    public static bool MatchesExtension(Stream content, string extension)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(extension))
                return false;

            var ext = extension.ToLowerInvariant();

            byte[]? expected = ext switch
            {
                ".pdf"  => PdfSig,
                ".png"  => PngSig,
                ".jpg"  => JpgSig,
                ".jpeg" => JpgSig,
                ".tiff" => null, // two valid signatures — handled below
                _       => null
            };

            // Unknown extension
            if (ext != ".tiff" && expected is null)
                return false;

            // Read enough bytes for the largest signature (PNG = 8 bytes)
            const int MaxHeaderLen = 8;
            var header = new byte[MaxHeaderLen];
            int read = content.Read(header, 0, MaxHeaderLen);

            if (ext == ".tiff")
                return StartsWith(header, read, TiffLeSig) || StartsWith(header, read, TiffBeSig);

            return StartsWith(header, read, expected!);
        }
        finally
        {
            // Always reset so the subsequent SaveAsync sees the full stream.
            if (content.CanSeek)
                content.Seek(0, SeekOrigin.Begin);
        }
    }

    private static bool StartsWith(byte[] buffer, int bufferLen, byte[] signature)
    {
        if (bufferLen < signature.Length)
            return false;
        for (int i = 0; i < signature.Length; i++)
            if (buffer[i] != signature[i])
                return false;
        return true;
    }
}
