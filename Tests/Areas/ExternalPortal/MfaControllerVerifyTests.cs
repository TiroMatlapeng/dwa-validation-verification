using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.Controllers;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Services.Portal.Mfa;
using dwa_ver_val.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class MfaControllerVerifyTests
{
    private static (MfaController sut, ApplicationDBContext db) BuildController(
        Guid userId,
        ITotpService? totp = null,
        ISmsOtpService? smsOtp = null,
        IDeviceTrustService? deviceTrust = null,
        IPublicUserSignInService? signIn = null,
        Action<ApplicationDBContext>? seed = null)
    {
        var db = TestDbContextFactory.Create();
        seed?.Invoke(db);
        db.SaveChanges();

        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        identity.AddClaim(new Claim("MfaPending", "true"));

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        var controller = new MfaController(
            db,
            totp ?? Mock.Of<ITotpService>(),
            smsOtp ?? Mock.Of<ISmsOtpService>(),
            deviceTrust ?? Mock.Of<IDeviceTrustService>(),
            signIn ?? Mock.Of<IPublicUserSignInService>())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = tempData
        };
        return (controller, db);
    }

    private static PublicUser TotpUser(Guid userId) => new PublicUser
    {
        PublicUserId = userId,
        EmailAddress = "u@test.com",
        PasswordHash = "h",
        FirstName = "U",
        LastName = "U",
        Status = "Active",
        EmailConfirmed = true,
        MfaEnabled = true,
        MfaMethod = "TOTP",
        MfaSecret = "TESTSECRET",
        RegistrationDate = DateTime.UtcNow
    };

    private static PublicUser SmsUser(Guid userId) => new PublicUser
    {
        PublicUserId = userId,
        EmailAddress = "u@test.com",
        PasswordHash = "h",
        FirstName = "U",
        LastName = "U",
        Status = "Active",
        EmailConfirmed = true,
        MfaEnabled = true,
        MfaMethod = "SMS",
        PhoneNumber = "+27821234567",
        RegistrationDate = DateTime.UtcNow
    };

    [Fact]
    public async Task Verify_Post_Totp_ValidCode_NoTrust_IssuesFullSession_RedirectsToDashboard()
    {
        var userId = Guid.NewGuid();
        var totpMock = new Mock<ITotpService>();
        totpMock.Setup(t => t.Validate("TESTSECRET", "123456", null))
                .Returns(new TotpValidationResult(true, 12345L));
        var signInMock = new Mock<IPublicUserSignInService>();
        signInMock.Setup(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var trustMock = new Mock<IDeviceTrustService>();

        var (sut, _) = BuildController(userId, totp: totpMock.Object, signIn: signInMock.Object,
            deviceTrust: trustMock.Object, seed: d => d.PublicUsers.Add(TotpUser(userId)));

        var vm = new MfaVerifyViewModel { Code = "123456", TrustDevice = false };
        var result = await sut.Verify(vm, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Dashboard", redirect.ControllerName);
        signInMock.Verify(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        trustMock.Verify(d => d.TrustAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Verify_Post_Totp_ValidCode_WithTrust_CallsTrustAsync()
    {
        var userId = Guid.NewGuid();
        var totpMock = new Mock<ITotpService>();
        totpMock.Setup(t => t.Validate("TESTSECRET", "123456", null))
                .Returns(new TotpValidationResult(true, 12345L));
        var signInMock = new Mock<IPublicUserSignInService>();
        signInMock.Setup(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var trustMock = new Mock<IDeviceTrustService>();
        trustMock.Setup(d => d.TrustAsync(userId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync("raw-token-value");

        var (sut, _) = BuildController(userId, totp: totpMock.Object, signIn: signInMock.Object,
            deviceTrust: trustMock.Object, seed: d => d.PublicUsers.Add(TotpUser(userId)));

        var vm = new MfaVerifyViewModel { Code = "123456", TrustDevice = true };
        await sut.Verify(vm, default);

        trustMock.Verify(d => d.TrustAsync(userId, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Verify_Post_Totp_InvalidCode_ReturnsViewWithError()
    {
        var userId = Guid.NewGuid();
        var totpMock = new Mock<ITotpService>();
        totpMock.Setup(t => t.Validate(It.IsAny<string>(), "000000", null))
                .Returns(new TotpValidationResult(false, null));
        var signInMock = new Mock<IPublicUserSignInService>();

        var (sut, _) = BuildController(userId, totp: totpMock.Object, signIn: signInMock.Object,
            seed: d => d.PublicUsers.Add(TotpUser(userId)));

        var result = await sut.Verify(new MfaVerifyViewModel { Code = "000000" }, default);

        Assert.IsType<ViewResult>(result);
        Assert.False(sut.ModelState.IsValid);
        signInMock.Verify(s => s.IssueFullSessionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Verify_Post_Sms_ValidCode_IssuesFullSession_RedirectsToDashboard()
    {
        var userId = Guid.NewGuid();
        var smsMock = new Mock<ISmsOtpService>();
        smsMock.Setup(s => s.ValidateAsync(userId, "123456", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var signInMock = new Mock<IPublicUserSignInService>();
        signInMock.Setup(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var (sut, _) = BuildController(userId, smsOtp: smsMock.Object, signIn: signInMock.Object,
            seed: d => d.PublicUsers.Add(SmsUser(userId)));

        var result = await sut.Verify(new MfaVerifyViewModel { Code = "123456" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        signInMock.Verify(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendSmsCode_Post_CallsSendAsync_RedirectsToVerify()
    {
        var userId = Guid.NewGuid();
        var smsMock = new Mock<ISmsOtpService>();
        smsMock.Setup(s => s.SendAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var (sut, _) = BuildController(userId, smsOtp: smsMock.Object);

        var result = await sut.SendSmsCode(null, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Verify", redirect.ActionName);
        smsMock.Verify(s => s.SendAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
