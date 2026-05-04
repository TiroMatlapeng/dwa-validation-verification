namespace dwa_ver_val.Helpers;

/// <summary>
/// Thrown by services when a requested entity is not found OR is not visible
/// to the current caller. Mapped to HTTP 404 by PortalExceptionHandler so we
/// don't leak record existence (no 403/200 timing differences).
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
