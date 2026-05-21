using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Services.Portal.Mfa;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dwa_ver_val.Areas.ExternalPortal.Controllers;

[Area("ExternalPortal")]
[Route("ExternalPortal/[controller]/[action]")]
[Authorize(Policy = PortalPolicies.PortalMfaPending)]
public class MfaController : Controller
{
    private readonly ApplicationDBContext _db;
    private readonly ITotpService _totp;
    private readonly ISmsOtpService _smsOtp;
    private readonly IDeviceTrustService _deviceTrust;
    private readonly IPublicUserSignInService _signIn;

    public MfaController(
        ApplicationDBContext db,
        ITotpService totp,
        ISmsOtpService smsOtp,
        IDeviceTrustService deviceTrust,
        IPublicUserSignInService signIn)
    {
        _db = db;
        _totp = totp;
        _smsOtp = smsOtp;
        _deviceTrust = deviceTrust;
        _signIn = signIn;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── SelectMethod ──

    [HttpGet]
    public IActionResult SelectMethod() => View(new MfaSelectMethodViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> SelectMethod(MfaSelectMethodViewModel vm, CancellationToken ct)
    {
        IActionResult result = vm.MfaMethod == "SMS"
            ? RedirectToAction(nameof(EnrolSms))
            : RedirectToAction(nameof(EnrolTotp));
        return Task.FromResult(result);
    }

    // ── Enrol TOTP ──

    [HttpGet]
    public async Task<IActionResult> EnrolTotp(CancellationToken ct)
    {
        var user = await _db.PublicUsers.FindAsync(new object[] { UserId }, ct);
        if (user is null) return NotFound();

        if (string.IsNullOrEmpty(user.MfaSecret))
        {
            user.MfaSecret = _totp.GenerateSecret();
            await _db.SaveChangesAsync(ct);
        }

        var uri = _totp.GetQrCodeUri(user.MfaSecret, user.EmailAddress);
        var png = _totp.GetQrCodePng(uri);
        return View(new MfaEnrolTotpViewModel { QrCodeBase64 = Convert.ToBase64String(png) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EnrolTotp(MfaEnrolTotpViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _db.PublicUsers.FindAsync(new object[] { UserId }, ct);
        if (user is null) return NotFound();

        var validation = _totp.Validate(user.MfaSecret!, vm.Code, user.LastUsedOtpTimestamp);
        if (!validation.Valid)
        {
            ModelState.AddModelError("", "Invalid code. Please try again.");
            var uri = _totp.GetQrCodeUri(user.MfaSecret!, user.EmailAddress);
            vm.QrCodeBase64 = Convert.ToBase64String(_totp.GetQrCodePng(uri));
            return View(vm);
        }

        user.MfaEnabled = true;
        user.MfaMethod = "TOTP";
        user.MfaEnrolledDate = DateTime.UtcNow;
        user.LastUsedOtpTimestamp = validation.NewTimestamp;
        await _db.SaveChangesAsync(ct);

        await _signIn.IssueFullSessionAsync(UserId, ct);
        return RedirectToAction("Index", "Dashboard", new { area = "ExternalPortal" });
    }

    // ── Enrol SMS ──

    [HttpGet]
    public async Task<IActionResult> EnrolSms(CancellationToken ct)
    {
        var user = await _db.PublicUsers.FindAsync(new object[] { UserId }, ct);
        if (user is null) return NotFound();
        return View(new MfaEnrolSmsViewModel { PhoneNumber = user.PhoneNumber ?? "" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EnrolSms(MfaEnrolSmsViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _db.PublicUsers.FindAsync(new object[] { UserId }, ct);
        if (user is null) return NotFound();

        if (user.PhoneNumber != vm.PhoneNumber)
        {
            user.PhoneNumber = vm.PhoneNumber;
            await _db.SaveChangesAsync(ct);
        }

        await _smsOtp.SendAsync(UserId, ct);
        return RedirectToAction(nameof(VerifySmsEnrolment));
    }

    // ── Verify SMS Enrolment ──

    [HttpGet]
    public IActionResult VerifySmsEnrolment() => View(new MfaVerifySmsEnrolmentViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifySmsEnrolment(MfaVerifySmsEnrolmentViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(vm);

        if (!await _smsOtp.ValidateAsync(UserId, vm.Code, ct))
        {
            ModelState.AddModelError("", "Invalid or expired code. Please try again.");
            return View(vm);
        }

        var user = await _db.PublicUsers.FindAsync(new object[] { UserId }, ct);
        if (user is null) return NotFound();

        user.MfaEnabled = true;
        user.MfaMethod = "SMS";
        user.MfaEnrolledDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _signIn.IssueFullSessionAsync(UserId, ct);
        return RedirectToAction("Index", "Dashboard", new { area = "ExternalPortal" });
    }

    // ── Verify (login flow) ──

    [HttpGet]
    public async Task<IActionResult> Verify(string? returnUrl = null, CancellationToken ct = default)
    {
        var user = await _db.PublicUsers.FindAsync(new object[] { UserId }, ct);
        if (user is null) return NotFound();
        return View(new MfaVerifyViewModel { MfaMethod = user.MfaMethod ?? "", ReturnUrl = returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(MfaVerifyViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _db.PublicUsers.FindAsync(new object[] { UserId }, ct);
        if (user is null) return NotFound();

        bool valid = user.MfaMethod == "SMS"
            ? await _smsOtp.ValidateAsync(UserId, vm.Code, ct)
            : ValidateTotp(user, vm.Code);

        if (!valid)
        {
            ModelState.AddModelError("", "Invalid or expired code. Please try again.");
            vm.MfaMethod = user.MfaMethod ?? "";
            return View(vm);
        }

        await _db.SaveChangesAsync(ct);
        await _signIn.IssueFullSessionAsync(UserId, ct);

        if (vm.TrustDevice)
        {
            var rawToken = await _deviceTrust.TrustAsync(UserId, Request.Headers.UserAgent.ToString(), ct);
            Response.Cookies.Append("dwa_dtrust", rawToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });
        }

        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
            return Redirect(vm.ReturnUrl);

        return RedirectToAction("Index", "Dashboard", new { area = "ExternalPortal" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendSmsCode(string? returnUrl, CancellationToken ct)
    {
        await _smsOtp.SendAsync(UserId, ct);
        return RedirectToAction(nameof(Verify), new { returnUrl });
    }

    private bool ValidateTotp(PublicUser user, string code)
    {
        var result = _totp.Validate(user.MfaSecret!, code, user.LastUsedOtpTimestamp);
        if (!result.Valid) return false;
        user.LastUsedOtpTimestamp = result.NewTimestamp;
        return true;
    }
}
