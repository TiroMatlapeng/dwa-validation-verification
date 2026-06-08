namespace dwa_ver_val.Services.Documents;

/// <summary>
/// Outcome of an antivirus scan over an uploaded document's content.
/// <list type="bullet">
///   <item><see cref="Clean"/> — no signature matched; safe to persist and serve.</item>
///   <item><see cref="Infected"/> — a malware signature matched; reject the upload.</item>
///   <item><see cref="Error"/> — the scan could not complete (scanner unreachable, I/O error).
///   Callers MUST fail closed: treat <see cref="Error"/> as not-clean and never serve.</item>
/// </list>
/// </summary>
public enum VirusScanResult
{
    Clean,
    Infected,
    Error
}

/// <summary>
/// Scans uploaded document content for malware before it is persisted and before it can
/// be downloaded. The default implementation is <see cref="EicarVirusScanner"/> (detects the
/// industry-standard EICAR test signature). A production ClamAV/Microsoft Defender scanner
/// plugs in behind this interface via DI, selected by configuration — see the note in
/// <see cref="EicarVirusScanner"/>.
/// </summary>
public interface IVirusScanner
{
    /// <summary>
    /// Scans <paramref name="content"/> from its current position to the end. Implementations
    /// MUST reset <paramref name="content"/>.Position to 0 before returning (when seekable) so the
    /// caller's subsequent read/save sees the full stream. Returns <see cref="VirusScanResult.Error"/>
    /// rather than throwing when the scan cannot complete, so callers can fail closed.
    /// </summary>
    Task<VirusScanResult> ScanAsync(Stream content, CancellationToken ct);
}
