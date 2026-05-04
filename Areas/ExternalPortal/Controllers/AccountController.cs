using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dwa_ver_val.Areas.ExternalPortal.Controllers;

[Area("ExternalPortal")]
[Route("ExternalPortal/[controller]/[action]")]
public class AccountController : Controller
{
    private readonly IPublicUserRegistrationService _registration;
    private readonly IPublicUserSignInService _signIn;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IPublicUserRegistrationService registration,
        IPublicUserSignInService signIn,
        ILogger<AccountController> logger)
    {
        _registration = registration;
        _signIn = signIn;
        _logger = logger;
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(vm);

        var result = await _registration.RegisterAsync(new RegistrationRequest(
            EmailAddress: vm.Email,
            Password: vm.Password,
            FirstName: vm.FirstName,
            LastName: vm.LastName,
            IdentityNumber: vm.IdentityNumber,
            PhoneNumber: vm.PhoneNumber,
            IsHDI: vm.IsHDI,
            HdiConsent: vm.HdiConsent,
            AcceptTerms: vm.AcceptTerms), ct);

        if (!result.Success)
        {
            foreach (var err in result.Errors) ModelState.AddModelError("", err);
            return View(vm);
        }

        // Real implementation will substitute the link in the email body. For Stage 2a the
        // token is logged via LoggingEmailSender — we surface it here too so the demo can
        // copy/paste during testing. Stage 2b will move the link substitution into the service.
        var url = Url?.Action(nameof(ConfirmEmail), "Account", new { area = "ExternalPortal", token = result.ConfirmationToken });
        if (!string.IsNullOrEmpty(url)) TempData["DemoConfirmUrl"] = url;
        return RedirectToAction(nameof(RegisterConfirmation));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult RegisterConfirmation()
    {
        ViewData["DemoConfirmUrl"] = TempData["DemoConfirmUrl"];
        return View();
    }

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(string? token, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token))
        {
            ViewData["Success"] = false;
            ViewData["Error"] = "Missing confirmation token.";
            return View();
        }

        var result = await _registration.ConfirmEmailAsync(token, ct);
        ViewData["Success"] = result.Success;
        ViewData["Error"] = result.Success ? null : (result.Errors.FirstOrDefault() ?? "The confirmation link is invalid.");
        return View();
    }
}
