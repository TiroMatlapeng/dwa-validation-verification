namespace dwa_ver_val.Services.Portal.Mfa;

public interface ISmsOtpService
{
    Task SendAsync(Guid publicUserId, CancellationToken ct = default);
    Task<bool> ValidateAsync(Guid publicUserId, string code, CancellationToken ct = default);
}
