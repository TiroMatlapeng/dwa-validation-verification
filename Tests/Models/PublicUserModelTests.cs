using Xunit;
using dwa_ver_val.Tests.Helpers;

namespace dwa_ver_val.Tests.Models;

public class PublicUserModelTests
{
    [Fact]
    public void NewAuthColumns_AreNullableOrZeroByDefault()
    {
        var user = PublicUserBuilder.Active();

        Assert.Null(user.MfaSecret);
        Assert.Null(user.MfaEnrolledDate);
        Assert.Null(user.LastLoginDate);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockoutUntil);
        Assert.Null(user.LastUsedOtpTimestamp);
        Assert.Null(user.HdiConsentGivenDate);
    }

    [Fact]
    public void NewAuthColumns_AreSettable()
    {
        var user = PublicUserBuilder.Active();
        user.MfaSecret = "secret";
        user.MfaEnrolledDate = new DateTime(2026, 5, 4);
        user.LastLoginDate = new DateTime(2026, 5, 4);
        user.FailedLoginAttempts = 3;
        user.LockoutUntil = new DateTime(2026, 5, 4, 10, 0, 0);
        user.LastUsedOtpTimestamp = 1714824000L;
        user.HdiConsentGivenDate = new DateTime(2026, 5, 4);

        Assert.Equal("secret", user.MfaSecret);
        Assert.Equal(3, user.FailedLoginAttempts);
        Assert.Equal(1714824000L, user.LastUsedOtpTimestamp);
    }
}
