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

public class MfaControllerEnrolmentTests
{
    private static Guid NewUserId() => Guid.NewGuid();

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

    private static PublicUser EnrolingUser(Guid userId) => new PublicUser
    {
        PublicUserId = userId,
        EmailAddress = "u@test.com",
        PasswordHash = "h",
        FirstName = "U",
        LastName = "U",
        Status = "Active",
        EmailConfirmed = true,
        MfaEnabled = false,
        PhoneNumber = "+27821234567",
        RegistrationDate = DateTime.UtcNow
    };

    [Fact]
    public async Task SelectMethod_Post_Totp_RedirectsToEnrolTotp()
    {
        var userId = NewUserId();
        var (sut, _) = BuildController(userId);

        var result = await sut.SelectMethod(new MfaSelectMethodViewModel { MfaMethod = "TOTP" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("EnrolTotp", redirect.ActionName);
    }

    [Fact]
    public async Task SelectMethod_Post_Sms_RedirectsToEnrolSms()
    {
        var userId = NewUserId();
        var (sut, _) = BuildController(userId);

        var result = await sut.SelectMethod(new MfaSelectMethodViewModel { MfaMethod = "SMS" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("EnrolSms", redirect.ActionName);
    }

    [Fact]
    public async Task EnrolTotp_Get_SavesSecretAndReturnsView()
    {
        var userId = NewUserId();
        var totpMock = new Mock<ITotpService>();
        totpMock.Setup(t => t.GenerateSecret()).Returns("TESTSECRET");
        totpMock.Setup(t => t.GetQrCodeUri("TESTSECRET", "u@test.com")).Returns("otpauth://totp/test");
        totpMock.Setup(t => t.GetQrCodePng("otpauth://totp/test")).Returns(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        var (sut, db) = BuildController(userId, totp: totpMock.Object,
            seed: d => d.PublicUsers.Add(EnrolingUser(userId)));

        var result = await sut.EnrolTotp(default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MfaEnrolTotpViewModel>(view.Model);
        Assert.False(string.IsNullOrEmpty(model.QrCodeBase64));
        var user = db.PublicUsers.Single();
        Assert.Equal("TESTSECRET", user.MfaSecret);
    }

    [Fact]
    public async Task EnrolTotp_Post_ValidCode_SetsMfaMethodAndRedirectsToDashboard()
    {
        var userId = NewUserId();
        var totpMock = new Mock<ITotpService>();
        totpMock.Setup(t => t.Validate("TESTSECRET", "123456", null))
                .Returns(new TotpValidationResult(true, 12345L));
        var signInMock = new Mock<IPublicUserSignInService>();
        signInMock.Setup(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

        var (sut, db) = BuildController(userId, totp: totpMock.Object, signIn: signInMock.Object,
            seed: d => d.PublicUsers.Add(new PublicUser
            {
                PublicUserId = userId,
                EmailAddress = "u@test.com",
                PasswordHash = "h",
                FirstName = "U",
                LastName = "U",
                Status = "Active",
                EmailConfirmed = true,
                MfaSecret = "TESTSECRET",
                RegistrationDate = DateTime.UtcNow
            }));

        var result = await sut.EnrolTotp(new MfaEnrolTotpViewModel { Code = "123456" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Dashboard", redirect.ControllerName);
        var user = db.PublicUsers.Single();
        Assert.True(user.MfaEnabled);
        Assert.Equal("TOTP", user.MfaMethod);
        signInMock.Verify(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnrolTotp_Post_InvalidCode_ReturnsViewWithError()
    {
        var userId = NewUserId();
        var totpMock = new Mock<ITotpService>();
        totpMock.Setup(t => t.Validate(It.IsAny<string>(), "000000", null))
                .Returns(new TotpValidationResult(false, null));

        var (sut, db) = BuildController(userId, totp: totpMock.Object,
            seed: d => d.PublicUsers.Add(new PublicUser
            {
                PublicUserId = userId,
                EmailAddress = "u@test.com",
                PasswordHash = "h",
                FirstName = "U",
                LastName = "U",
                Status = "Active",
                EmailConfirmed = true,
                MfaSecret = "TESTSECRET",
                RegistrationDate = DateTime.UtcNow
            }));

        var result = await sut.EnrolTotp(new MfaEnrolTotpViewModel { Code = "000000" }, default);

        Assert.IsType<ViewResult>(result);
        Assert.False(sut.ModelState.IsValid);
        var user = db.PublicUsers.Single();
        Assert.False(user.MfaEnabled);
    }

    [Fact]
    public async Task EnrolSms_Post_CallsSendAsync_RedirectsToVerify()
    {
        var userId = NewUserId();
        var smsMock = new Mock<ISmsOtpService>();
        smsMock.Setup(s => s.SendAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var (sut, _) = BuildController(userId, smsOtp: smsMock.Object,
            seed: d => d.PublicUsers.Add(EnrolingUser(userId)));

        var result = await sut.EnrolSms(new MfaEnrolSmsViewModel { PhoneNumber = "+27821234567" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("VerifySmsEnrolment", redirect.ActionName);
        smsMock.Verify(s => s.SendAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifySmsEnrolment_Post_ValidCode_SetsMfaMethodAndRedirects()
    {
        var userId = NewUserId();
        var smsMock = new Mock<ISmsOtpService>();
        smsMock.Setup(s => s.ValidateAsync(userId, "123456", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var signInMock = new Mock<IPublicUserSignInService>();
        signInMock.Setup(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var (sut, db) = BuildController(userId, smsOtp: smsMock.Object, signIn: signInMock.Object,
            seed: d => d.PublicUsers.Add(EnrolingUser(userId)));

        var result = await sut.VerifySmsEnrolment(new MfaVerifySmsEnrolmentViewModel { Code = "123456" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Dashboard", redirect.ControllerName);
        var user = db.PublicUsers.Single();
        Assert.True(user.MfaEnabled);
        Assert.Equal("SMS", user.MfaMethod);
        signInMock.Verify(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifySmsEnrolment_Post_InvalidCode_ReturnsViewWithError()
    {
        var userId = NewUserId();
        var smsMock = new Mock<ISmsOtpService>();
        smsMock.Setup(s => s.ValidateAsync(userId, "999999", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var (sut, db) = BuildController(userId, smsOtp: smsMock.Object,
            seed: d => d.PublicUsers.Add(EnrolingUser(userId)));

        var result = await sut.VerifySmsEnrolment(new MfaVerifySmsEnrolmentViewModel { Code = "999999" }, default);

        Assert.IsType<ViewResult>(result);
        Assert.False(sut.ModelState.IsValid);
        var user = db.PublicUsers.Single();
        Assert.False(user.MfaEnabled);
    }
}
