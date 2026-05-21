namespace dwa_ver_val.Services.Portal.Mfa;

public record TotpValidationResult(bool Valid, long? NewTimestamp);

public interface ITotpService
{
    string GenerateSecret();
    string GetQrCodeUri(string secret, string email);
    byte[] GetQrCodePng(string uri);
    TotpValidationResult Validate(string secret, string code, long? lastUsedTimestamp);
}
