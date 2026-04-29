using System.Security.Claims;
using dwa_ver_val.Services.Audit;
using dwa_ver_val.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace dwa_ver_val.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IConfiguration _config;
    private readonly IAuditService _audit;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IConfiguration config,
        IAuditService audit,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _config = config;
        _audit = audit;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        var entraConfigured = !string.IsNullOrEmpty(_config["AzureAd:ClientId"])
                              && !string.IsNullOrEmpty(_config["AzureAd:TenantId"]);
        return View(new LoginViewModel { ReturnUrl = returnUrl, EntraEnabled = entraConfigured });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null || !user.IsActive)
        {
            await _audit.LogAsync(new AuditEvent(
                EntityType: nameof(ApplicationUser),
                EntityId: user?.Id.ToString() ?? "(unknown)",
                Action: "SignInFailed",
                UserDisplayName: model.Email,
                Reason: user is null ? "Unknown email" : "Account is deactivated",
                IPAddress: ipAddress));
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} signed in.", model.Email);
            await _audit.LogAsync(new AuditEvent(
                EntityType: nameof(ApplicationUser),
                EntityId: user.Id.ToString(),
                Action: "SignedIn",
                UserId: user.Id,
                UserDisplayName: $"{user.FirstName} {user.LastName}",
                IPAddress: ipAddress));
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);
            return RedirectToAction("Index", "Home");
        }

        if (result.IsLockedOut)
        {
            await _audit.LogAsync(new AuditEvent(
                EntityType: nameof(ApplicationUser),
                EntityId: user.Id.ToString(),
                Action: "AccountLockedOut",
                UserId: user.Id,
                UserDisplayName: $"{user.FirstName} {user.LastName}",
                Reason: "Too many failed sign-in attempts",
                IPAddress: ipAddress));
            ModelState.AddModelError(string.Empty, "Account locked. Try again later.");
            return View(model);
        }

        await _audit.LogAsync(new AuditEvent(
            EntityType: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            Action: "SignInFailed",
            UserId: user.Id,
            UserDisplayName: $"{user.FirstName} {user.LastName}",
            Reason: "Wrong password",
            IPAddress: ipAddress));
        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    /// <summary>
    /// Initiates an external (OIDC) sign-in challenge with the named provider, e.g. "Microsoft".
    /// Redirects the browser to the IdP; the IdP comes back to ExternalLoginCallback.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account",
            new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    /// <summary>
    /// Handles the redirect back from the IdP. Looks up the matching ApplicationUser by email
    /// (just-in-time creates one if missing); signs them in via the local Identity cookie.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(remoteError))
        {
            await _audit.LogAsync(new AuditEvent(
                EntityType: nameof(ApplicationUser),
                EntityId: "(unknown)",
                Action: "ExternalSignInFailed",
                Reason: remoteError,
                IPAddress: ipAddress));
            ModelState.AddModelError(string.Empty, $"Error from external provider: {remoteError}");
            return View(nameof(Login), new LoginViewModel { ReturnUrl = returnUrl, EntraEnabled = true });
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            ModelState.AddModelError(string.Empty, "Could not read external provider response.");
            return View(nameof(Login), new LoginViewModel { ReturnUrl = returnUrl, EntraEnabled = true });
        }

        // Sign in if we already have a federated link.
        var loginResult = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

        if (loginResult.Succeeded)
        {
            _logger.LogInformation("External login {Provider} succeeded for {Subject}.", info.LoginProvider, info.ProviderKey);
            var existingFromLogin = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            await _audit.LogAsync(new AuditEvent(
                EntityType: nameof(ApplicationUser),
                EntityId: existingFromLogin?.Id.ToString() ?? "(unknown)",
                Action: "ExternalSignedIn",
                UserId: existingFromLogin?.Id,
                UserDisplayName: existingFromLogin is null
                    ? info.Principal.Identity?.Name
                    : $"{existingFromLogin.FirstName} {existingFromLogin.LastName}",
                ToValue: info.LoginProvider,
                IPAddress: ipAddress));
            return RedirectLocal(returnUrl);
        }

        // Otherwise: link / JIT-create a local ApplicationUser keyed off the email claim.
        var email = info.Principal.FindFirstValue(ClaimTypes.Email)
                    ?? info.Principal.FindFirstValue("preferred_username")
                    ?? info.Principal.FindFirstValue(ClaimTypes.Upn);
        if (string.IsNullOrEmpty(email))
        {
            ModelState.AddModelError(string.Empty, "Microsoft account did not include an email address; cannot link to a DWA user.");
            return View(nameof(Login), new LoginViewModel { ReturnUrl = returnUrl, EntraEnabled = true });
        }

        var existing = await _userManager.FindByEmailAsync(email);
        var jitProvisioned = false;
        if (existing is null)
        {
            jitProvisioned = true;
            var givenName = info.Principal.FindFirstValue(ClaimTypes.GivenName) ?? "(Microsoft)";
            var surname = info.Principal.FindFirstValue(ClaimTypes.Surname) ?? "User";

            existing = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = givenName,
                LastName = surname,
                EmployeeNumber = "ENTRA-JIT",
                IsActive = true,
                OrgUnitId = null
            };
            var create = await _userManager.CreateAsync(existing);
            if (!create.Succeeded)
            {
                foreach (var e in create.Errors) ModelState.AddModelError(string.Empty, e.Description);
                return View(nameof(Login), new LoginViewModel { ReturnUrl = returnUrl, EntraEnabled = true });
            }

            // Promote to SystemAdmin if the email appears in the GrantAdminToEmails allowlist;
            // otherwise default to ReadOnly until a SystemAdmin upgrades the row.
            var adminEmails = _config["Identity:GrantAdminToEmails"]?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? Array.Empty<string>();
            var isAllowlistedAdmin = adminEmails.Any(e =>
                string.Equals(e, email, StringComparison.OrdinalIgnoreCase));
            var roleToAssign = isAllowlistedAdmin ? DwsRoles.SystemAdmin : DwsRoles.ReadOnly;
            if (await _roleManager.RoleExistsAsync(roleToAssign))
            {
                await _userManager.AddToRoleAsync(existing, roleToAssign);
            }

            await _audit.LogAsync(new AuditEvent(
                EntityType: nameof(ApplicationUser),
                EntityId: existing.Id.ToString(),
                Action: "ExternalUserJitProvisioned",
                UserId: existing.Id,
                UserDisplayName: $"{existing.FirstName} {existing.LastName}",
                ToValue: roleToAssign,
                Reason: $"Created from {info.LoginProvider} sign-in",
                IPAddress: ipAddress));
            _logger.LogInformation("JIT-created ApplicationUser {Email} as {Role} from external login {Provider}.",
                email, roleToAssign, info.LoginProvider);
        }

        if (!existing.IsActive)
        {
            await _audit.LogAsync(new AuditEvent(
                EntityType: nameof(ApplicationUser),
                EntityId: existing.Id.ToString(),
                Action: "ExternalSignInFailed",
                UserId: existing.Id,
                UserDisplayName: $"{existing.FirstName} {existing.LastName}",
                Reason: "Account is deactivated",
                IPAddress: ipAddress));
            ModelState.AddModelError(string.Empty, "Account is deactivated.");
            return View(nameof(Login), new LoginViewModel { ReturnUrl = returnUrl, EntraEnabled = true });
        }

        var addLogin = await _userManager.AddLoginAsync(existing, info);
        if (!addLogin.Succeeded
            && !addLogin.Errors.Any(e => e.Code == "LoginAlreadyAssociated"))
        {
            foreach (var e in addLogin.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(nameof(Login), new LoginViewModel { ReturnUrl = returnUrl, EntraEnabled = true });
        }

        await _signInManager.SignInAsync(existing, isPersistent: false);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        if (!jitProvisioned)
        {
            await _audit.LogAsync(new AuditEvent(
                EntityType: nameof(ApplicationUser),
                EntityId: existing.Id.ToString(),
                Action: "ExternalSignedIn",
                UserId: existing.Id,
                UserDisplayName: $"{existing.FirstName} {existing.LastName}",
                ToValue: info.LoginProvider,
                IPAddress: ipAddress));
        }

        _logger.LogInformation("External login linked + signed in {Email} via {Provider}.", email, info.LoginProvider);
        return RedirectLocal(returnUrl);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var displayName = User.FindFirstValue("displayName") ?? User.Identity?.Name;
        await _signInManager.SignOutAsync();

        if (Guid.TryParse(userId, out var parsed))
        {
            await _audit.LogAsync(new AuditEvent(
                EntityType: nameof(ApplicationUser),
                EntityId: parsed.ToString(),
                Action: "SignedOut",
                UserId: parsed,
                UserDisplayName: displayName,
                IPAddress: HttpContext.Connection.RemoteIpAddress?.ToString()));
        }
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        // Always return the same view so we don't reveal whether the email is registered.
        if (user is null || !user.IsActive)
        {
            await _audit.LogAsync(new AuditEvent(
                EntityType: nameof(ApplicationUser),
                EntityId: user?.Id.ToString() ?? "(unknown)",
                Action: "PasswordResetRequested",
                UserDisplayName: model.Email,
                Reason: user is null ? "Unknown email" : "Account is deactivated",
                IPAddress: HttpContext.Connection.RemoteIpAddress?.ToString()));
            return View("ForgotPasswordConfirmation", new ForgotPasswordResultViewModel { Email = model.Email, ResetLink = null });
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(token));
        var resetLink = Url.Action(
            nameof(ResetPassword),
            "Account",
            new { email = user.Email, token = encoded },
            protocol: Request.Scheme)!;

        await _audit.LogAsync(new AuditEvent(
            EntityType: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            Action: "PasswordResetRequested",
            UserId: user.Id,
            UserDisplayName: $"{user.FirstName} {user.LastName}",
            IPAddress: HttpContext.Connection.RemoteIpAddress?.ToString()));

        // Demo: surface the reset link directly. Production: send via email service.
        return View("ForgotPasswordConfirmation", new ForgotPasswordResultViewModel
        {
            Email = user.Email!,
            ResetLink = resetLink
        });
    }

    [HttpGet]
    public IActionResult ResetPassword(string email, string token)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        {
            return BadRequest("A reset token is required.");
        }
        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            // Don't reveal whether the email is registered.
            return View("ResetPasswordConfirmation");
        }

        string decodedToken;
        try
        {
            decodedToken = System.Text.Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token));
        }
        catch (FormatException)
        {
            ModelState.AddModelError(string.Empty, "Reset link is malformed.");
            return View(model);
        }

        var result = await _userManager.ResetPasswordAsync(user, decodedToken, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(model);
        }

        await _audit.LogAsync(new AuditEvent(
            EntityType: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            Action: "PasswordReset",
            UserId: user.Id,
            UserDisplayName: $"{user.FirstName} {user.LastName}",
            IPAddress: HttpContext.Connection.RemoteIpAddress?.ToString()));
        return View("ResetPasswordConfirmation");
    }

    private IActionResult RedirectLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }
}
