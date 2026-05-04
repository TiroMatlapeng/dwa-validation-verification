using dwa_ver_val.Helpers;
using Xunit;

namespace dwa_ver_val.Tests.Helpers;

public class SaIdValidatorTests
{
    [Theory]
    [InlineData("8001015009087")]   // a known-valid example (Luhn-correct construction)
    [InlineData("9202204720082")]
    public void IsValid_ReturnsTrueForKnownValidIds(string id)
    {
        Assert.True(SaIdValidator.IsValid(id));
    }

    [Theory]
    [InlineData("8001015009088")]   // checksum bumped by one — invalid
    [InlineData("0000000000000")]   // all zeros — fails the checksum
    [InlineData("1234567890123")]
    public void IsValid_ReturnsFalseForBadChecksum(string id)
    {
        Assert.False(SaIdValidator.IsValid(id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("123")]            // too short
    [InlineData("80010150090877")] // too long
    [InlineData("80010A5009087")]  // non-digit
    public void IsValid_ReturnsFalseForMalformedInputs(string? id)
    {
        Assert.False(SaIdValidator.IsValid(id));
    }
}
