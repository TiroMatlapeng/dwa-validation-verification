using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Services.Portal.Mfa;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dwa_ver_val.Areas.ExternalPortal.Controllers;

[Area("ExternalPortal")]
[Route("ExternalPortal/[controller]/[action]")]
public class AccountController : Controller
{
    private readonly IPublicUserRegistrationService _registration;
    private readonly IPublicUserSignInService _signIn;
    private readonly IDeviceTrustService _deviceTrust;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IPublicUserRegistrationService registration,
        IPublicUserSignInService signIn,
        IDeviceTrustService deviceTrust,
        ILogger<AccountController> logger)
    {
        _registration = registration;
        _signIn = signIn;
        _deviceTrust = deviceTrust;
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
        if (!result.Success)
        {
            ViewData["Success"] = false;
            ViewData["Error"] = result.Errors.FirstOrDefault() ?? "The confirmation link is invalid.";
            return View();
        }

        // Email confirmed — start MFA enrolment.
        if (result.PublicUserId.HasValue)
        {
            await _signIn.IssuePartialSessionAsync(result.PublicUserId.Value, ct);
            return RedirectToAction("SelectMethod", "Mfa", new { area = "ExternalPortal" });
        }

        ViewData["Success"] = true;
        return View();
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(vm);

        var result = await _signIn.SignInAsync(vm.Email, vm.Password, ct);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error ?? "Login failed.");
            return View(vm);
        }

        var userId = result.PublicUserId!.Value;

        if (!result.MfaEnabled)
        {
            await _signIn.IssuePartialSessionAsync(userId, ct);
            return RedirectToAction("SelectMethod", "Mfa", new { area = "ExternalPortal" });
        }

        // Check device trust cookie
        var dtrustCookie = Request.Cookies["dwa_dtrust"];
        bool trusted = dtrustCookie is not null
            && await _deviceTrust.IsTrustedAsync(userId, dtrustCookie, ct);

        if (trusted)
        {
            await _signIn.IssueFullSessionAsync(userId, ct);
            if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return Redirect(vm.ReturnUrl);
            return RedirectToAction("Index", "Dashboard", new { area = "ExternalPortal" });
        }

        await _signIn.IssuePartialSessionAsync(userId, ct);
        var safeReturnUrl = (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl)) ? vm.ReturnUrl : null;
        return RedirectToAction("Verify", "Mfa", new { area = "ExternalPortal", returnUrl = safeReturnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _signIn.SignOutAsync(ct);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
