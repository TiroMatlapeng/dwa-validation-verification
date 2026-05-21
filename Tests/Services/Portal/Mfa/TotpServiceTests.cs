using dwa_ver_val.Services.Portal.Mfa;
using OtpNet;
using Xunit;

namespace dwa_ver_val.Tests.Services.Portal.Mfa;

public class TotpServiceTests
{
    private static TotpService CreateSut() => new();

    [Fact]
    public void GenerateSecret_ReturnsNonEmptyBase32String()
    {
        var sut = CreateSut();
        var secret = sut.GenerateSecret();
        Assert.False(string.IsNullOrWhiteSpace(secret));
        // Base32 uses only A-Z and 2-7
        Assert.Matches("^[A-Z2-7]+=*$", secret);
    }

    [Fact]
    public void GetQrCodeUri_ContainsSecretEmailAndIssuer()
    {
        var sut = CreateSut();
        var secret = sut.GenerateSecret();
        var uri = sut.GetQrCodeUri(secret, "user@example.com");
        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains(secret, uri);
        Assert.Contains("user%40example.com", uri);
        Assert.Contains("issuer=DWA", uri);
    }

    [Fact]
    public void GetQrCodePng_ReturnsNonEmptyByteArray()
    {
        var sut = CreateSut();
        var secret = sut.GenerateSecret();
        var uri = sut.GetQrCodeUri(secret, "user@example.com");
        var png = sut.GetQrCodePng(uri);
        Assert.NotEmpty(png);
        // PNG magic bytes
        Assert.Equal(0x89, png[0]);
        Assert.Equal(0x50, png[1]); // 'P'
        Assert.Equal(0x4E, png[2]); // 'N'
        Assert.Equal(0x47, png[3]); // 'G'
    }

    [Fact]
    public void Validate_WithCurrentCode_ReturnsTrue()
    {
        var sut = CreateSut();
        var secret = sut.GenerateSecret();
        // Generate the current valid code using OtpNet directly
        var key = Base32Encoding.ToBytes(secret);
        var totp = new Totp(key);
        var currentCode = totp.ComputeTotp();

        var result = sut.Validate(secret, currentCode, lastUsedTimestamp: null);

        Assert.True(result.Valid);
        Assert.NotNull(result.NewTimestamp);
    }

    [Fact]
    public void Validate_WithWrongCode_ReturnsFalse()
    {
        var sut = CreateSut();
        var secret = sut.GenerateSecret();

        var result = sut.Validate(secret, "000000", lastUsedTimestamp: null);

        Assert.False(result.Valid);
        Assert.Null(result.NewTimestamp);
    }

    [Fact]
    public void Validate_ReplayAttack_SameTimestampRejected()
    {
        var sut = CreateSut();
        var secret = sut.GenerateSecret();
        var key = Base32Encoding.ToBytes(secret);
        var totp = new Totp(key);
        var code = totp.ComputeTotp();

        var first = sut.Validate(secret, code, lastUsedTimestamp: null);
        Assert.True(first.Valid);

        // Second call with the same code and the timestamp from the first call
        var second = sut.Validate(secret, code, lastUsedTimestamp: first.NewTimestamp);
        Assert.False(second.Valid);
    }
}
