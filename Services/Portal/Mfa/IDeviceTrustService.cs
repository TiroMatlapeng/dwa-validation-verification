namespace dwa_ver_val.Services.Portal.Mfa;

public interface IDeviceTrustService
{
    Task<bool> IsTrustedAsync(Guid publicUserId, string rawToken, CancellationToken ct = default);
    Task<string> TrustAsync(Guid publicUserId, string? userAgent, CancellationToken ct = default);
    Task RevokeAllAsync(Guid publicUserId, CancellationToken ct = default);
}
