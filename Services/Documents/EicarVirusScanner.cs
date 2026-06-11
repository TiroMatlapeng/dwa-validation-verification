using System.Text;

namespace dwa_ver_val.Services.Documents;

/// <summary>
/// Default <see cref="IVirusScanner"/>. Flags content containing the standard EICAR
/// antivirus test signature as <see cref="VirusScanResult.Infected"/>, otherwise
/// <see cref="VirusScanResult.Clean"/>. EICAR is the harmless, industry-standard test
/// string every real AV engine is required to detect, so this gives us a deterministic,
/// infrastructure-free way to prove the upload/download gate end-to-end.
///
/// PRODUCTION SEAM: a real scanner (ClamAV via clamd, or Microsoft Defender) plugs in
/// behind <see cref="IVirusScanner"/> by registering a different implementation in
/// Program.cs, selected by configuration (e.g. a "VirusScanner:Provider" setting). That
/// implementation would stream the content to the scan engine and map its verdict onto
/// <see cref="VirusScanResult"/> (engine error → <see cref="VirusScanResult.Error"/> so the
/// caller fails closed). It is intentionally NOT built here — no clamd is available in this
/// environment to test it against.
/// </summary>
public sealed class EicarVirusScanner : IVirusScanner
{
    // The standard 68-byte EICAR test signature (ASCII). Any file containing this exact
    // byte sequence is treated as a (harmless) "virus" by every conformant AV product.
    private static readonly byte[] EicarSignature = Encoding.ASCII.GetBytes(
        "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");

    public async Task<VirusScanResult> ScanAsync(Stream content, CancellationToken ct)
    {
        if (content is null)
            return VirusScanResult.Error;

        try
        {
            byte[] bytes;
            using (var buffer = new MemoryStream())
            {
                await content.CopyToAsync(buffer, ct);
                bytes = buffer.ToArray();
            }

            return ContainsSignature(bytes, EicarSignature)
                ? VirusScanResult.Infected
                : VirusScanResult.Clean;
        }
        catch (OperationCanceledException)
        {
            // Honour cancellation rather than masking it as a scan error.
            throw;
        }
        catch
        {
            // Any I/O failure → fail closed: not clean.
            return VirusScanResult.Error;
        }
        finally
        {
            // Reset so the caller's subsequent read/save sees the full stream.
            if (content is not null && content.CanSeek)
                content.Seek(0, SeekOrigin.Begin);
        }
    }

    private static bool ContainsSignature(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
            return false;

        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            int j = 0;
            while (j < needle.Length && haystack[i + j] == needle[j])
                j++;
            if (j == needle.Length)
                return true;
        }
        return false;
    }
}
