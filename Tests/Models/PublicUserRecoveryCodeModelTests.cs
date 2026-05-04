using Xunit;

namespace dwa_ver_val.Tests.Models;

public class PublicUserRecoveryCodeModelTests
{
    [Fact]
    public void NewRecoveryCode_HasExpectedDefaults()
    {
        var code = new PublicUserRecoveryCode
        {
            PublicUserId = Guid.NewGuid(),
            CodeHash = "hashed-bytes",
            CreatedDate = new DateTime(2026, 5, 4)
        };

        Assert.False(code.Used);
        Assert.Null(code.UsedDate);
        Assert.Null(code.ExpiresDate);
        Assert.Equal("hashed-bytes", code.CodeHash);
    }

    [Fact]
    public void Redemption_FlipsUsedAndUsedDate()
    {
        var code = new PublicUserRecoveryCode
        {
            PublicUserId = Guid.NewGuid(),
            CodeHash = "hashed",
            CreatedDate = new DateTime(2026, 5, 4)
        };

        code.Used = true;
        code.UsedDate = new DateTime(2026, 5, 4, 10, 0, 0);

        Assert.True(code.Used);
        Assert.Equal(new DateTime(2026, 5, 4, 10, 0, 0), code.UsedDate);
    }
}
