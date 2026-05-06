using dwa_ver_val.Services.Audit;
using dwa_ver_val.Services.Infrastructure.Email;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Tests.Helpers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Services.Portal.Auth;

public class PublicUserRegistrationServiceTests
{
    private static IDataProtectionProvider CreateDataProtection()
    {
        var services = new ServiceCollection().AddDataProtection().Services.BuildServiceProvider();
        return services.GetRequiredService<IDataProtectionProvider>();
    }

    private static PublicUserRegistrationService CreateSut(
        ApplicationDBContext db, Mock<IEmailSender>? email = null, TestAuditService? audit = null)
    {
        return new PublicUserRegistrationService(
            db,
            new PasswordHasher<PublicUser>(),
            CreateDataProtection(),
            (email ?? new Mock<IEmailSender>(MockBehavior.Loose)).Object,
            audit ?? new TestAuditService(),
            NullLogger<PublicUserRegistrationService>.Instance);
    }

    [Fact]
    public async Task RegisterAsync_HappyPath_CreatesPendingUser_AndDispatchesEmail()
    {
        using var db = TestDbContextFactory.Create();
        var email = new Mock<IEmailSender>();
        email.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var audit = new TestAuditService();
        var sut = CreateSut(db, email, audit);

        var result = await sut.RegisterAsync(new RegistrationRequest(
            EmailAddress: "alice@example.test",
            Password: "Aliceaaaa123!",
            FirstName: "Alice",
            LastName: "Smith",
            IdentityNumber: "8001015009087",
            PhoneNumber: "0820000000",
            IsHDI: false,
            HdiConsent: false,
            AcceptTerms: true), default);

        Assert.True(result.Success);
        Assert.Single(db.PublicUsers);
        var saved = db.PublicUsers.AsNoTracking().Single();
        Assert.Equal("Pending", saved.Status);
        Assert.False(saved.EmailConfirmed);
        Assert.NotEqual("Aliceaaaa123!", saved.PasswordHash); // hashed
        email.Verify(e => e.SendAsync(It.Is<EmailMessage>(m => m.To == "alice@example.test"), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(audit.Events, e => e.Action == "PublicUserRegistered");
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_FailsWithErrorAndDoesNotEmail()
    {
        using var db = TestDbContextFactory.Create();
        db.PublicUsers.Add(PublicUserBuilder.Pending("dup@example.test"));
        await db.SaveChangesAsync();

        var email = new Mock<IEmailSender>();
        var sut = CreateSut(db, email);

        var result = await sut.RegisterAsync(new RegistrationRequest(
            EmailAddress: "dup@example.test",
            Password: "Aliceaaaa123!",
            FirstName: "X", LastName: "Y",
            IdentityNumber: "8001015009087",
            PhoneNumber: null,
            IsHDI: false, HdiConsent: false, AcceptTerms: true), default);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("email", StringComparison.OrdinalIgnoreCase));
        email.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_BadPassword_FailsAndDoesNotPersist()
    {
        using var db = TestDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.RegisterAsync(new RegistrationRequest(
            EmailAddress: "x@example.test",
            Password: "short",
            FirstName: "X", LastName: "Y",
            IdentityNumber: "8001015009087",
            PhoneNumber: null,
            IsHDI: false, HdiConsent: false, AcceptTerms: true), default);

        Assert.False(result.Success);
        Assert.Empty(db.PublicUsers);
    }

    [Fact]
    public async Task RegisterAsync_BadIdentityNumber_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.RegisterAsync(new RegistrationRequest(
            EmailAddress: "x@example.test",
            Password: "Aliceaaaa123!",
            FirstName: "X", LastName: "Y",
            IdentityNumber: "1234567890123",
            PhoneNumber: null,
            IsHDI: false, HdiConsent: false, AcceptTerms: true), default);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RegisterAsync_HdiTrueWithoutConsent_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.RegisterAsync(new RegistrationRequest(
            EmailAddress: "x@example.test",
            Password: "Aliceaaaa123!",
            FirstName: "X", LastName: "Y",
            IdentityNumber: "8001015009087",
            PhoneNumber: null,
            IsHDI: true, HdiConsent: false, AcceptTerms: true), default);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("hdi", StringComparison.OrdinalIgnoreCase) || e.Contains("consent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RegisterAsync_HdiTrueWithConsent_PersistsConsentDate()
    {
        using var db = TestDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.RegisterAsync(new RegistrationRequest(
            EmailAddress: "h@example.test",
            Password: "Aliceaaaa123!",
            FirstName: "H", LastName: "Y",
            IdentityNumber: "8001015009087",
            PhoneNumber: null,
            IsHDI: true, HdiConsent: true, AcceptTerms: true), default);

        Assert.True(result.Success);
        var saved = db.PublicUsers.AsNoTracking().Single();
        Assert.True(saved.IsHDI);
        Assert.NotNull(saved.HdiConsentGivenDate);
    }

    [Fact]
    public async Task ConfirmEmailAsync_ValidToken_ActivatesUser_AndAudits()
    {
        using var db = TestDbContextFactory.Create();
        var audit = new TestAuditService();
        var sut = CreateSut(db, audit: audit);

        var registerResult = await sut.RegisterAsync(new RegistrationRequest(
            EmailAddress: "c@example.test",
            Password: "Aliceaaaa123!",
            FirstName: "C", LastName: "D",
            IdentityNumber: "8001015009087",
            PhoneNumber: null,
            IsHDI: false, HdiConsent: false, AcceptTerms: true), default);
        Assert.True(registerResult.Success);
        var token = registerResult.ConfirmationToken!;

        var confirmResult = await sut.ConfirmEmailAsync(token, default);

        Assert.True(confirmResult.Success);
        var saved = db.PublicUsers.AsNoTracking().Single();
        Assert.True(saved.EmailConfirmed);
        Assert.Equal("Active", saved.Status);
        Assert.Contains(audit.Events, e => e.Action == "PublicUserEmailConfirmed");
    }

    [Fact]
    public async Task ConfirmEmailAsync_TamperedToken_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var sut = CreateSut(db);

        var confirmResult = await sut.ConfirmEmailAsync("totally-not-a-real-token", default);

        Assert.False(confirmResult.Success);
    }
}
