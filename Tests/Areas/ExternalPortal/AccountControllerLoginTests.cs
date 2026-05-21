using dwa_ver_val.Areas.ExternalPortal.Controllers;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Services.Portal.Mfa;
using SignInResult = dwa_ver_val.Services.Portal.Auth.SignInResult;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class AccountControllerLoginTests
{
    private static AccountController BuildController(
        IPublicUserSignInService signIn,
        IDeviceTrustService? deviceTrust = null)
    {
        var controller = new AccountController(
            new Mock<IPublicUserRegistrationService>().Object,
            signIn,
            deviceTrust ?? Mock.Of<IDeviceTrustService>(),
            NullLogger<AccountController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    [Fact]
    public async Task Login_Post_MfaNotEnrolled_IssuesPartialSession_RedirectsToSelectMethod()
    {
        var userId = Guid.NewGuid();
        var signIn = new Mock<IPublicUserSignInService>();
        signIn.Setup(s => s.SignInAsync("u@e.test", "Goodpassword12!", It.IsAny<CancellationToken>()))
              .ReturnsAsync(new SignInResult(true, null, userId, MfaEnabled: false));
        signIn.Setup(s => s.IssuePartialSessionAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var controller = BuildController(signIn.Object);
        var result = await controller.Login(new LoginViewModel { Email = "u@e.test", Password = "Goodpassword12!" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("SelectMethod", redirect.ActionName);
        Assert.Equal("Mfa", redirect.ControllerName);
        signIn.Verify(s => s.IssuePartialSessionAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Login_Post_MfaEnrolled_DeviceNotTrusted_IssuesPartialSession_RedirectsToVerify()
    {
        var userId = Guid.NewGuid();
        var signIn = new Mock<IPublicUserSignInService>();
        signIn.Setup(s => s.SignInAsync("u@e.test", "Goodpassword12!", It.IsAny<CancellationToken>()))
              .ReturnsAsync(new SignInResult(true, null, userId, MfaEnabled: true));
        signIn.Setup(s => s.IssuePartialSessionAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var deviceTrust = new Mock<IDeviceTrustService>();
        deviceTrust.Setup(d => d.IsTrustedAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

        var controller = BuildController(signIn.Object, deviceTrust.Object);
        var result = await controller.Login(new LoginViewModel { Email = "u@e.test", Password = "Goodpassword12!" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Verify", redirect.ActionName);
        Assert.Equal("Mfa", redirect.ControllerName);
        signIn.Verify(s => s.IssuePartialSessionAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Login_Post_MfaEnrolled_DeviceTrusted_IssuesFullSession_RedirectsToDashboard()
    {
        var userId = Guid.NewGuid();
        var signIn = new Mock<IPublicUserSignInService>();
        signIn.Setup(s => s.SignInAsync("u@e.test", "Goodpassword12!", It.IsAny<CancellationToken>()))
              .ReturnsAsync(new SignInResult(true, null, userId, MfaEnabled: true));
        signIn.Setup(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var deviceTrust = new Mock<IDeviceTrustService>();
        deviceTrust.Setup(d => d.IsTrustedAsync(userId, "trusted-token", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Cookie"] = "dwa_dtrust=trusted-token";

        var controller = new AccountController(
            new Mock<IPublicUserRegistrationService>().Object,
            signIn.Object,
            deviceTrust.Object,
            NullLogger<AccountController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = await controller.Login(new LoginViewModel { Email = "u@e.test", Password = "Goodpassword12!" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Dashboard", redirect.ControllerName);
        signIn.Verify(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Login_Post_FailureFromService_ReturnsViewWithError()
    {
        var signIn = new Mock<IPublicUserSignInService>();
        signIn.Setup(s => s.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new SignInResult(false, "Login failed."));
        var controller = BuildController(signIn.Object);

        var result = await controller.Login(new LoginViewModel { Email = "u@e.test", Password = "wrong" }, default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[""]!.Errors, e => e.ErrorMessage == "Login failed.");
    }

    [Fact]
    public async Task Login_Post_MfaEnrolled_DeviceTrusted_WithReturnUrl_RedirectsToReturnUrl()
    {
        var userId = Guid.NewGuid();
        var signIn = new Mock<IPublicUserSignInService>();
        signIn.Setup(s => s.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new SignInResult(true, null, userId, MfaEnabled: true));
        signIn.Setup(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var deviceTrust = new Mock<IDeviceTrustService>();
        deviceTrust.Setup(d => d.IsTrustedAsync(userId, "trusted-token", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Cookie"] = "dwa_dtrust=trusted-token";

        var controller = new AccountController(
            new Mock<IPublicUserRegistrationService>().Object,
            signIn.Object,
            deviceTrust.Object,
            NullLogger<AccountController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
        controller.Url = new Mock<IUrlHelper>().Object;
        Mock.Get(controller.Url).Setup(u => u.IsLocalUrl("/ExternalPortal/Dashboard/Index")).Returns(true);

        var result = await controller.Login(new LoginViewModel { Email = "u@e.test", Password = "Goodpassword12!", ReturnUrl = "/ExternalPortal/Dashboard/Index" }, default);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/ExternalPortal/Dashboard/Index", redirect.Url);
    }

    [Fact]
    public async Task Logout_Post_CallsServiceAndRedirectsToLogin()
    {
        var signIn = new Mock<IPublicUserSignInService>();
        signIn.Setup(s => s.SignOutAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var controller = BuildController(signIn.Object);

        var result = await controller.Logout(default);

        signIn.Verify(s => s.SignOutAsync(It.IsAny<CancellationToken>()), Times.Once);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
    }
}
