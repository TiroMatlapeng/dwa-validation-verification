using System.Security.Claims;
using dwa_ver_val.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace dwa_ver_val.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IConfiguration _config;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IConfiguration config,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _config = config;
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

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} signed in.", model.Email);
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);
            return RedirectToAction("Index", "Home");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Account locked. Try again later.");
            return View(model);
        }

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
        if (!string.IsNullOrEmpty(remoteError))
        {
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
        if (existing is null)
        {
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

            // Default JIT-created Entra users to ReadOnly until a SystemAdmin promotes them.
            // Lower-friction than locking them out entirely on first sign-in.
            if (await _roleManager.RoleExistsAsync(DwsRoles.ReadOnly))
            {
                await _userManager.AddToRoleAsync(existing, DwsRoles.ReadOnly);
            }
            _logger.LogInformation("JIT-created ApplicationUser {Email} from external login {Provider}.", email, info.LoginProvider);
        }

        if (!existing.IsActive)
        {
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
        _logger.LogInformation("External login linked + signed in {Email} via {Provider}.", email, info.LoginProvider);
        return RedirectLocal(returnUrl);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }
}
