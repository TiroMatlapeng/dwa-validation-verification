using dwa_ver_val.Areas.ExternalPortal.Controllers;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class AccountControllerRegistrationTests
{
    [Fact]
    public async Task Register_Post_HappyPath_RedirectsToRegisterConfirmation()
    {
        var reg = new Mock<IPublicUserRegistrationService>();
        reg.Setup(r => r.RegisterAsync(It.IsAny<RegistrationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistrationResult(true, Array.Empty<string>(), "tok", Guid.NewGuid()));
        var sign = new Mock<IPublicUserSignInService>();

        var controller = new AccountController(reg.Object, sign.Object, NullLogger<AccountController>.Instance);

        var result = await controller.Register(new RegisterViewModel
        {
            Email = "alice@e.test",
            Password = "Aliceaaaa123!",
            ConfirmPassword = "Aliceaaaa123!",
            FirstName = "Alice",
            LastName = "Smith",
            IdentityNumber = "8001015009087",
            AcceptTerms = true
        }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("RegisterConfirmation", redirect.ActionName);
    }

    [Fact]
    public async Task Register_Post_ServiceFailure_ReturnsViewWithErrors()
    {
        var reg = new Mock<IPublicUserRegistrationService>();
        reg.Setup(r => r.RegisterAsync(It.IsAny<RegistrationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistrationResult(false, new[] { "Service-level error." }));
        var controller = new AccountController(reg.Object, new Mock<IPublicUserSignInService>().Object, NullLogger<AccountController>.Instance);

        var result = await controller.Register(new RegisterViewModel
        {
            Email = "x@y.test",
            Password = "validpassword12",
            ConfirmPassword = "validpassword12",
            FirstName = "X", LastName = "Y",
            IdentityNumber = "8001015009087",
            AcceptTerms = true
        }, default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[""]!.Errors, e => e.ErrorMessage == "Service-level error.");
    }

    [Fact]
    public async Task ConfirmEmail_Get_ValidToken_ShowsSuccessView()
    {
        var reg = new Mock<IPublicUserRegistrationService>();
        reg.Setup(r => r.ConfirmEmailAsync("good", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailConfirmationResult(true, Array.Empty<string>(), Guid.NewGuid()));
        var controller = new AccountController(reg.Object, new Mock<IPublicUserSignInService>().Object, NullLogger<AccountController>.Instance);

        var result = await controller.ConfirmEmail("good", default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(true, view.ViewData["Success"]);
    }

    [Fact]
    public async Task ConfirmEmail_Get_InvalidToken_ShowsFailureView()
    {
        var reg = new Mock<IPublicUserRegistrationService>();
        reg.Setup(r => r.ConfirmEmailAsync("bad", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailConfirmationResult(false, new[] { "Invalid token." }));
        var controller = new AccountController(reg.Object, new Mock<IPublicUserSignInService>().Object, NullLogger<AccountController>.Instance);

        var result = await controller.ConfirmEmail("bad", default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(false, view.ViewData["Success"]);
        Assert.Equal("Invalid token.", view.ViewData["Error"]);
    }
}
