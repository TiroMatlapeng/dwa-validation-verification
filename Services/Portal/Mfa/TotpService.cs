using OtpNet;
using QRCoder;

namespace dwa_ver_val.Services.Portal.Mfa;

public class TotpService : ITotpService
{
    private const string Issuer = "DWA V&V System";

    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    public string GetQrCodeUri(string secret, string email)
    {
        var encodedEmail = Uri.EscapeDataString(email);
        var encodedIssuer = Uri.EscapeDataString(Issuer);
        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secret}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period=30";
    }

    public byte[] GetQrCodePng(string uri)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(qrData);
        return png.GetGraphic(20);
    }

    public TotpValidationResult Validate(string secret, string code, long? lastUsedTimestamp)
    {
        var key = Base32Encoding.ToBytes(secret);
        var totp = new Totp(key);
        var window = new VerificationWindow(previous: 1, future: 1);

        if (!totp.VerifyTotp(code, out long timeWindowUsed, window))
            return new TotpValidationResult(false, null);

        if (lastUsedTimestamp.HasValue && timeWindowUsed <= lastUsedTimestamp.Value)
            return new TotpValidationResult(false, null);

        return new TotpValidationResult(true, timeWindowUsed);
    }
}
