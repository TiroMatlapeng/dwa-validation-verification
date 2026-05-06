using dwa_ver_val.Helpers;
using Xunit;

namespace dwa_ver_val.Tests.Helpers;

public class PortalPasswordPolicyTests
{
    [Fact]
    public void Validate_ReturnsEmpty_ForCompliantPassword()
    {
        var errors = PortalPasswordPolicy.Validate("My secure password 12 chars");
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("short1aa")]                                      // <12 chars
    [InlineData("alllowercaseonlynodigits")]                       // no digit
    [InlineData("123456789012345")]                                // no letter
    [InlineData("")]
    [InlineData(null)]
    public void Validate_ReturnsErrors_ForNonCompliantPasswords(string? password)
    {
        var errors = PortalPasswordPolicy.Validate(password);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Validate_ReportsMinLengthFailure()
    {
        var errors = PortalPasswordPolicy.Validate("aB1");
        Assert.Contains(errors, e => e.Contains("12", StringComparison.OrdinalIgnoreCase));
    }
}
