using System.Security.Claims;
using dwa_ver_val.Services.Audit;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Tests.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Services.Portal.Auth;

public class PublicUserSignInServiceTests
{
    private static (PublicUserSignInService sut, Mock<IAuthenticationService> auth, TestAuditService audit, ApplicationDBContext db) CreateSut(
        Action<ApplicationDBContext>? seed = null)
    {
        var db = TestDbContextFactory.Create();
        seed?.Invoke(db);
        db.SaveChanges();

        var hasher = new PasswordHasher<PublicUser>();
        var audit = new TestAuditService();
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        var ctx = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddSingleton(auth.Object);
        ctx.RequestServices = services.BuildServiceProvider();
        var ctxAccessor = new HttpContextAccessor { HttpContext = ctx };

        var sut = new PublicUserSignInService(
            db, hasher, ctxAccessor, audit, NullLogger<PublicUserSignInService>.Instance);
        return (sut, auth, audit, db);
    }

    private static PublicUser HashedActive(string email, string plainPassword)
    {
        var hasher = new PasswordHasher<PublicUser>();
        var user = PublicUserBuilder.Active(email);
        user.PasswordHash = hasher.HashPassword(user, plainPassword);
        return user;
    }

    [Fact]
    public async Task SignInAsync_HappyPath_SignsIn_AndAudits()
    {
        var (sut, auth, audit, _) = CreateSut(db => db.PublicUsers.Add(HashedActive("u@e.test", "Goodpassword12!")));

        var result = await sut.SignInAsync("u@e.test", "Goodpassword12!", default);

        Assert.True(result.Success);
        auth.Verify(a => a.SignInAsync(It.IsAny<HttpContext>(),
            "PublicPortalScheme",
            It.Is<ClaimsPrincipal>(p => p.HasClaim(c => c.Type == "EmailConfirmed" && c.Value == "true")),
            It.IsAny<AuthenticationProperties>()), Times.Once);
        Assert.Contains(audit.Events, e => e.Action == "PublicUserSignedIn");
    }

    [Fact]
    public async Task SignInAsync_WrongPassword_ReturnsGenericError_AndAudits()
    {
        var (sut, auth, audit, db) = CreateSut(db => db.PublicUsers.Add(HashedActive("u@e.test", "Goodpassword12!")));

        var result = await sut.SignInAsync("u@e.test", "wrongpassword12!", default);

        Assert.False(result.Success);
        Assert.Equal("Login failed.", result.Error);
        auth.Verify(a => a.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()), Times.Never);
        Assert.Contains(audit.Events, e => e.Action == "PublicUserSignInFailed");
    }

    [Fact]
    public async Task SignInAsync_UnknownEmail_ReturnsSameGenericError()
    {
        var (sut, _, _, _) = CreateSut();

        var result = await sut.SignInAsync("nope@e.test", "anything", default);

        Assert.False(result.Success);
        Assert.Equal("Login failed.", result.Error);
    }

    [Fact]
    public async Task SignInAsync_UnconfirmedEmail_BlocksWithSpecificMessage()
    {
        var (sut, auth, audit, _) = CreateSut(db =>
        {
            var pending = PublicUserBuilder.Pending("p@e.test");
            pending.PasswordHash = new PasswordHasher<PublicUser>().HashPassword(pending, "Goodpassword12!");
            db.PublicUsers.Add(pending);
        });

        var result = await sut.SignInAsync("p@e.test", "Goodpassword12!", default);

        Assert.False(result.Success);
        Assert.Equal("Please confirm your email before logging in.", result.Error);
        auth.Verify(a => a.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()), Times.Never);
    }

    [Fact]
    public async Task SignInAsync_SuspendedUser_ReturnsGenericError_DoesNotIssueCookie()
    {
        var (sut, auth, audit, _) = CreateSut(db =>
        {
            var user = PublicUserBuilder.Suspended("s@e.test");
            user.PasswordHash = new PasswordHasher<PublicUser>().HashPassword(user, "Goodpassword12!");
            db.PublicUsers.Add(user);
        });

        var result = await sut.SignInAsync("s@e.test", "Goodpassword12!", default);

        Assert.False(result.Success);
        Assert.Equal("Login failed.", result.Error);
        auth.Verify(a => a.SignInAsync(
            It.IsAny<HttpContext>(), It.IsAny<string>(),
            It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()), Times.Never);
        Assert.Contains(audit.Events, e => e.Action == "PublicUserSignInFailed" && e.Reason == "AccountSuspended");
    }

    [Fact]
    public async Task SignInAsync_DeactivatedUser_ReturnsGenericError_DoesNotIssueCookie()
    {
        var hasher = new PasswordHasher<PublicUser>();
        var (sut, auth, audit, _) = CreateSut(db =>
        {
            var user = new PublicUser
            {
                EmailAddress = "d@e.test",
                PasswordHash = "",
                FirstName = "D",
                LastName = "U",
                Status = "Deactivated",
                EmailConfirmed = true,
                RegistrationDate = DateTime.UtcNow
            };
            user.PasswordHash = hasher.HashPassword(user, "Goodpassword12!");
            db.PublicUsers.Add(user);
        });

        var result = await sut.SignInAsync("d@e.test", "Goodpassword12!", default);

        Assert.False(result.Success);
        Assert.Equal("Login failed.", result.Error);
        auth.Verify(a => a.SignInAsync(
            It.IsAny<HttpContext>(), It.IsAny<string>(),
            It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()), Times.Never);
        Assert.Contains(audit.Events, e => e.Action == "PublicUserSignInFailed" && e.Reason == "AccountSuspended");
    }

    [Fact]
    public async Task SignInAsync_LockedOutUser_BlocksWithoutCheckingPassword()
    {
        var (sut, auth, audit, _) = CreateSut(db =>
        {
            var user = HashedActive("l@e.test", "Goodpassword12!");
            user.FailedLoginAttempts = 5;
            user.LockoutUntil = DateTime.UtcNow.AddMinutes(10);
            db.PublicUsers.Add(user);
        });

        var result = await sut.SignInAsync("l@e.test", "Goodpassword12!", default);

        Assert.False(result.Success);
        Assert.Equal("Login failed.", result.Error);
        auth.Verify(a => a.SignInAsync(
            It.IsAny<HttpContext>(), It.IsAny<string>(),
            It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()), Times.Never);
        Assert.Contains(audit.Events, e => e.Action == "PublicUserSignInFailed" && e.Reason == "AccountLocked");
    }

    [Fact]
    public async Task SignInAsync_WrongPassword_IncrementsFailedAttempts()
    {
        var (sut, _, _, db) = CreateSut(db =>
        {
            var user = HashedActive("f@e.test", "Goodpassword12!");
            user.FailedLoginAttempts = 2;
            db.PublicUsers.Add(user);
        });

        await sut.SignInAsync("f@e.test", "wrongpassword!", default);

        var stored = db.PublicUsers.Single(u => u.EmailAddress == "f@e.test");
        Assert.Equal(3, stored.FailedLoginAttempts);
        Assert.Null(stored.LockoutUntil);
    }

    [Fact]
    public async Task SignInAsync_FifthWrongPassword_SetsLockoutUntil()
    {
        var (sut, _, _, db) = CreateSut(db =>
        {
            var user = HashedActive("ff@e.test", "Goodpassword12!");
            user.FailedLoginAttempts = 4;
            db.PublicUsers.Add(user);
        });

        await sut.SignInAsync("ff@e.test", "wrongpassword!", default);

        var stored = db.PublicUsers.Single(u => u.EmailAddress == "ff@e.test");
        Assert.Equal(5, stored.FailedLoginAttempts);
        Assert.NotNull(stored.LockoutUntil);
        Assert.True(stored.LockoutUntil > DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task SignInAsync_SuccessAfterPreviousFailures_ResetsLockout()
    {
        var (sut, _, _, db) = CreateSut(db =>
        {
            var user = HashedActive("r@e.test", "Goodpassword12!");
            user.FailedLoginAttempts = 3;
            db.PublicUsers.Add(user);
        });

        var result = await sut.SignInAsync("r@e.test", "Goodpassword12!", default);

        Assert.True(result.Success);
        var stored = db.PublicUsers.Single(u => u.EmailAddress == "r@e.test");
        Assert.Equal(0, stored.FailedLoginAttempts);
        Assert.Null(stored.LockoutUntil);
    }

    [Fact]
    public async Task SignOutAsync_CallsSignOutWithPortalScheme()
    {
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.SignOutAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);
        var ctx = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddSingleton(auth.Object);
        ctx.RequestServices = services.BuildServiceProvider();
        var sut2 = new PublicUserSignInService(
            TestDbContextFactory.Create(),
            new PasswordHasher<PublicUser>(),
            new HttpContextAccessor { HttpContext = ctx },
            new TestAuditService(),
            NullLogger<PublicUserSignInService>.Instance);

        await sut2.SignOutAsync(default);

        auth.Verify(a => a.SignOutAsync(It.IsAny<HttpContext>(), "PublicPortalScheme", It.IsAny<AuthenticationProperties>()), Times.Once);
    }
}
