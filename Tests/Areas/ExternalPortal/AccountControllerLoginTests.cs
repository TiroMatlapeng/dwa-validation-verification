using dwa_ver_val.Areas.ExternalPortal.Controllers;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class AccountControllerLoginTests
{
    [Fact]
    public async Task Login_Post_HappyPath_RedirectsToDashboard()
    {
        var sign = new Mock<IPublicUserSignInService>();
        sign.Setup(s => s.SignInAsync("u@e.test", "Goodpassword12!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new dwa_ver_val.Services.Portal.Auth.SignInResult(true, null, Guid.NewGuid()));
        var controller = new AccountController(new Mock<IPublicUserRegistrationService>().Object, sign.Object, NullLogger<AccountController>.Instance);

        var result = await controller.Login(new LoginViewModel { Email = "u@e.test", Password = "Goodpassword12!" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Dashboard", redirect.ControllerName);
    }

    [Fact]
    public async Task Login_Post_FailureFromService_ReturnsViewWithError()
    {
        var sign = new Mock<IPublicUserSignInService>();
        sign.Setup(s => s.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new dwa_ver_val.Services.Portal.Auth.SignInResult(false, "Login failed."));
        var controller = new AccountController(new Mock<IPublicUserRegistrationService>().Object, sign.Object, NullLogger<AccountController>.Instance);

        var result = await controller.Login(new LoginViewModel { Email = "u@e.test", Password = "wrong" }, default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[""]!.Errors, e => e.ErrorMessage == "Login failed.");
    }

    [Fact]
    public async Task Login_Post_HappyPath_WithReturnUrl_RedirectsToLocalReturnUrl()
    {
        var sign = new Mock<IPublicUserSignInService>();
        sign.Setup(s => s.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new dwa_ver_val.Services.Portal.Auth.SignInResult(true, null, Guid.NewGuid()));
        var controller = new AccountController(new Mock<IPublicUserRegistrationService>().Object, sign.Object, NullLogger<AccountController>.Instance);
        controller.Url = new Mock<IUrlHelper>().Object;
        Mock.Get(controller.Url).Setup(u => u.IsLocalUrl("/ExternalPortal/Dashboard/Index")).Returns(true);

        var result = await controller.Login(new LoginViewModel { Email = "u@e.test", Password = "Goodpassword12!", ReturnUrl = "/ExternalPortal/Dashboard/Index" }, default);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/ExternalPortal/Dashboard/Index", redirect.Url);
    }

    [Fact]
    public async Task Logout_Post_CallsServiceAndRedirectsToLogin()
    {
        var sign = new Mock<IPublicUserSignInService>();
        sign.Setup(s => s.SignOutAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var controller = new AccountController(new Mock<IPublicUserRegistrationService>().Object, sign.Object, NullLogger<AccountController>.Instance);

        var result = await controller.Logout(default);

        sign.Verify(s => s.SignOutAsync(It.IsAny<CancellationToken>()), Times.Once);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
    }
}
