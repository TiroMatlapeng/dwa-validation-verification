using dwa_ver_val.Services.Portal.Mfa;
using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Services.Portal.Mfa;

public class SmsOtpServiceTests
{
    private static (SmsOtpService sut, Mock<ISmsGateway> gateway, ApplicationDBContext db) CreateSut(
        Action<ApplicationDBContext>? seed = null)
    {
        var db = TestDbContextFactory.Create();
        seed?.Invoke(db);
        db.SaveChanges();
        var gateway = new Mock<ISmsGateway>();
        gateway.Setup(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(true);
        var sut = new SmsOtpService(db, gateway.Object);
        return (sut, gateway, db);
    }

    private static PublicUser ActiveUserWithPhone() =>
        new PublicUser
        {
            PublicUserId = Guid.NewGuid(),
            EmailAddress = "user@test.com",
            PasswordHash = "hash",
            FirstName = "A",
            LastName = "B",
            Status = "Active",
            EmailConfirmed = true,
            PhoneNumber = "+27821234567",
            RegistrationDate = DateTime.UtcNow
        };

    [Fact]
    public async Task SendAsync_CallsGateway_AndWritesHashedRow()
    {
        var user = ActiveUserWithPhone();
        var (sut, gateway, db) = CreateSut(d => d.PublicUsers.Add(user));

        await sut.SendAsync(user.PublicUserId);

        gateway.Verify(g => g.SendAsync("+27821234567", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        var row = await db.SmsOtps.SingleAsync();
        Assert.Equal(user.PublicUserId, row.PublicUserId);
        Assert.False(row.Used);
        Assert.True(row.ExpiresAt > DateTimeOffset.UtcNow);
        // CodeHash must not be the raw code (it's hashed)
        Assert.Equal(64, row.CodeHash.Length); // SHA-256 hex = 64 chars
    }

    [Fact]
    public async Task SendAsync_PrunesPriorRowsForSameUser()
    {
        var user = ActiveUserWithPhone();
        var (sut, _, db) = CreateSut(d =>
        {
            d.PublicUsers.Add(user);
            d.SmsOtps.Add(new SmsOtp
            {
                SmsOtpId = Guid.NewGuid(),
                PublicUserId = user.PublicUserId,
                CodeHash = "oldhash",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-15),
                Used = false
            });
        });

        await sut.SendAsync(user.PublicUserId);

        // Only the new row should remain
        var rows = await db.SmsOtps.ToListAsync();
        Assert.Single(rows);
        Assert.NotEqual("oldhash", rows[0].CodeHash);
    }

    [Fact]
    public async Task ValidateAsync_CorrectCode_ReturnsTrue_MarksUsed()
    {
        var user = ActiveUserWithPhone();
        var (sut, _, db) = CreateSut(d => d.PublicUsers.Add(user));

        var knownCode = "123456";
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(knownCode));
        var hexHash = Convert.ToHexString(hash).ToLowerInvariant();

        db.SmsOtps.Add(new SmsOtp
        {
            SmsOtpId = Guid.NewGuid(),
            PublicUserId = user.PublicUserId,
            CodeHash = hexHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            CreatedAt = DateTimeOffset.UtcNow,
            Used = false
        });
        await db.SaveChangesAsync();

        var valid = await sut.ValidateAsync(user.PublicUserId, knownCode);

        Assert.True(valid);
        var row = await db.SmsOtps.SingleAsync();
        Assert.True(row.Used);
    }

    [Fact]
    public async Task ValidateAsync_WrongCode_ReturnsFalse()
    {
        var user = ActiveUserWithPhone();
        var (sut, _, db) = CreateSut(d => d.PublicUsers.Add(user));

        var knownCode = "123456";
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(knownCode))).ToLowerInvariant();
        db.SmsOtps.Add(new SmsOtp
        {
            SmsOtpId = Guid.NewGuid(),
            PublicUserId = user.PublicUserId,
            CodeHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            CreatedAt = DateTimeOffset.UtcNow,
            Used = false
        });
        db.SaveChanges();

        var valid = await sut.ValidateAsync(user.PublicUserId, "999999");

        Assert.False(valid);
    }

    [Fact]
    public async Task ValidateAsync_ExpiredCode_ReturnsFalse()
    {
        var user = ActiveUserWithPhone();
        var (sut, _, db) = CreateSut(d => d.PublicUsers.Add(user));

        var code = "123456";
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
        db.SmsOtps.Add(new SmsOtp
        {
            SmsOtpId = Guid.NewGuid(),
            PublicUserId = user.PublicUserId,
            CodeHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1), // expired
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-6),
            Used = false
        });
        db.SaveChanges();

        Assert.False(await sut.ValidateAsync(user.PublicUserId, code));
    }

    [Fact]
    public async Task ValidateAsync_AlreadyUsedCode_ReturnsFalse()
    {
        var user = ActiveUserWithPhone();
        var (sut, _, db) = CreateSut(d => d.PublicUsers.Add(user));

        var code = "123456";
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
        db.SmsOtps.Add(new SmsOtp
        {
            SmsOtpId = Guid.NewGuid(),
            PublicUserId = user.PublicUserId,
            CodeHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            CreatedAt = DateTimeOffset.UtcNow,
            Used = true // already consumed
        });
        db.SaveChanges();

        Assert.False(await sut.ValidateAsync(user.PublicUserId, code));
    }
}
