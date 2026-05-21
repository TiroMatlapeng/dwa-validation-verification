using dwa_ver_val.Services.Portal.Mfa;
using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace dwa_ver_val.Tests.Services.Portal.Mfa;

public class DeviceTrustServiceTests
{
    private static (DeviceTrustService sut, ApplicationDBContext db) CreateSut(
        Action<ApplicationDBContext>? seed = null)
    {
        var db = TestDbContextFactory.Create();
        seed?.Invoke(db);
        db.SaveChanges();
        return (new DeviceTrustService(db), db);
    }

    private static PublicUser AnyUser() => new PublicUser
    {
        PublicUserId = Guid.NewGuid(),
        EmailAddress = "x@x.com",
        PasswordHash = "h",
        FirstName = "X",
        LastName = "X",
        Status = "Active",
        EmailConfirmed = true,
        RegistrationDate = DateTime.UtcNow
    };

    [Fact]
    public async Task TrustAsync_InsertsHashedRow_ReturnsRawToken()
    {
        var user = AnyUser();
        var (sut, db) = CreateSut(d => d.PublicUsers.Add(user));

        var rawToken = await sut.TrustAsync(user.PublicUserId, "Mozilla/5.0");

        Assert.False(string.IsNullOrEmpty(rawToken));
        var row = await db.TrustedDevices.SingleAsync();
        Assert.Equal(user.PublicUserId, row.PublicUserId);
        Assert.Equal("Mozilla/5.0", row.UserAgent);
        Assert.True(row.ExpiresAt > DateTimeOffset.UtcNow.AddDays(6));
        // Stored token must be the hash, not the raw value
        Assert.NotEqual(rawToken, row.DeviceTokenHash);
        Assert.Equal(64, row.DeviceTokenHash.Length);
    }

    [Fact]
    public async Task IsTrustedAsync_ValidToken_ReturnsTrue()
    {
        var user = AnyUser();
        var (sut, _) = CreateSut(d => d.PublicUsers.Add(user));

        var rawToken = await sut.TrustAsync(user.PublicUserId, null);

        Assert.True(await sut.IsTrustedAsync(user.PublicUserId, rawToken));
    }

    [Fact]
    public async Task IsTrustedAsync_ExpiredToken_ReturnsFalse()
    {
        var user = AnyUser();
        var (sut, db) = CreateSut(d => d.PublicUsers.Add(user));

        var rawToken = await sut.TrustAsync(user.PublicUserId, null);

        // Manually expire the row
        var row = await db.TrustedDevices.SingleAsync();
        row.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        Assert.False(await sut.IsTrustedAsync(user.PublicUserId, rawToken));
    }

    [Fact]
    public async Task IsTrustedAsync_WrongUser_ReturnsFalse()
    {
        var user1 = AnyUser();
        var user2 = AnyUser();
        var (sut, db) = CreateSut(d => { d.PublicUsers.Add(user1); d.PublicUsers.Add(user2); });

        var rawToken = await sut.TrustAsync(user1.PublicUserId, null);

        Assert.False(await sut.IsTrustedAsync(user2.PublicUserId, rawToken));
    }

    [Fact]
    public async Task IsTrustedAsync_UnknownToken_ReturnsFalse()
    {
        var user = AnyUser();
        var (sut, _) = CreateSut(d => d.PublicUsers.Add(user));

        Assert.False(await sut.IsTrustedAsync(user.PublicUserId, "completely-unknown-token"));
    }

    [Fact]
    public async Task RevokeAllAsync_DeletesOnlyTargetUserRows()
    {
        var user1 = AnyUser();
        var user2 = AnyUser();
        var (sut, db) = CreateSut(d => { d.PublicUsers.Add(user1); d.PublicUsers.Add(user2); });

        await sut.TrustAsync(user1.PublicUserId, null);
        await sut.TrustAsync(user2.PublicUserId, null);

        await sut.RevokeAllAsync(user1.PublicUserId);

        var remaining = await db.TrustedDevices.ToListAsync();
        Assert.Single(remaining);
        Assert.Equal(user2.PublicUserId, remaining[0].PublicUserId);
    }
}
