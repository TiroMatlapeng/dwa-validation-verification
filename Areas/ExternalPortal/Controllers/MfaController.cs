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

    // ── Verify (login flow) — stub for Task 8 ──

    [HttpGet]
    public IActionResult Verify(string? returnUrl = null)
        => View(new MfaVerifyViewModel { ReturnUrl = returnUrl });

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Verify(MfaVerifyViewModel vm, CancellationToken ct)
        => Task.FromResult<IActionResult>(View(vm));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> SendSmsCode(string? returnUrl, CancellationToken ct)
        => Task.FromResult<IActionResult>(RedirectToAction(nameof(Verify), new { returnUrl }));
}
