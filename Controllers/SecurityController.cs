using dwa_ver_val.Services.Audit;
using dwa_ver_val.Services.Portal.Mfa;
using dwa_ver_val.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace dwa_ver_val.Controllers;

/// <summary>
/// Self-service security settings for internal DWS staff signing in with local Identity
/// credentials: authenticator-app (TOTP) two-factor enrolment, recovery codes, and disable.
/// Entra-authenticated users do not need this — Entra enforces MFA via Conditional Access
/// (their sign-ins bypass local 2FA, see AccountController.ExternalLoginCallback).
/// </summary>
[Authorize]
public class SecurityController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITotpService _totp;
    private readonly IAuditService _audit;

    public SecurityController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITotpService totp,
        IAuditService audit)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _totp = totp;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        return View(new SecurityIndexViewModel
        {
            TwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user),
            RecoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user),
        });
    }

    [HttpGet]
    public async Task<IActionResult> EnableAuthenticator()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        return View(await BuildEnableModelAsync(user));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableAuthenticator(EnableAuthenticatorViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var code = model.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var valid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, code);

        if (!valid)
        {
            ModelState.AddModelError(nameof(model.Code), "The verification code is invalid. Scan the QR code and try again.");
            return View(await BuildEnableModelAsync(user));
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 8))!.ToList();

        await _audit.LogAsync(new AuditEvent(
            EntityType: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            Action: "MfaEnabled",
            UserId: user.Id,
            UserDisplayName: $"{user.FirstName} {user.LastName}"));

        TempData["Success"] = "Two-factor authentication is now enabled.";
        return View("RecoveryCodes", new RecoveryCodesViewModel { Codes = recoveryCodes });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableAuthenticator()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);

        await _audit.LogAsync(new AuditEvent(
            EntityType: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            Action: "MfaDisabled",
            UserId: user.Id,
            UserDisplayName: $"{user.FirstName} {user.LastName}"));

        TempData["Success"] = "Two-factor authentication has been disabled.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<EnableAuthenticatorViewModel> BuildEnableModelAsync(ApplicationUser user)
    {
        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        var uri = _totp.GetQrCodeUri(key!, user.Email ?? user.UserName!);
        var png = _totp.GetQrCodePng(uri);

        return new EnableAuthenticatorViewModel
        {
            SharedKey = FormatKey(key!),
            QrCodeDataUri = $"data:image/png;base64,{Convert.ToBase64String(png)}",
        };
    }

    private static string FormatKey(string key)
    {
        // Group in fours for readability when typing the key manually.
        var chunks = key.Chunk(4).Select(c => new string(c));
        return string.Join(" ", chunks).ToLowerInvariant();
    }
}
