namespace dwa_ver_val.Services.Portal.Mfa;

public interface ISmsGateway
{
    Task<bool> SendAsync(string to, string body, CancellationToken ct = default);
}
