using dwa_ver_val.Models.Enums;
using dwa_ver_val.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Controllers.Admin;

[Authorize(Policy = DwsPolicies.CanAdminister)]
[Route("Admin/[controller]/[action]")]
public class PropertyClaimsController : Controller
{
    private readonly ApplicationDBContext _db;
    private readonly INotificationService _notify;

    public PropertyClaimsController(ApplicationDBContext db, INotificationService notify)
    {
        _db = db;
        _notify = notify;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var claims = await _db.PublicUserProperties
            .Include(p => p.PublicUser)
            .Include(p => p.Property)
            .Where(p => p.Status == PropertyClaimStatus.Pending)
            .OrderBy(p => p.RequestedDate)
            .ToListAsync(ct);
        return View(claims);
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var claim = await _db.PublicUserProperties.FindAsync(new object[] { id }, ct)
            ?? throw new InvalidOperationException($"Claim {id} not found.");

        claim.Status = PropertyClaimStatus.Approved;
        claim.ApprovedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _notify.NotifyPublicUserAsync(claim.PublicUserId, null,
            "ClaimApproved",
            "Your property claim has been approved",
            "You can now log in to the V&V portal to view your case status.",
            actionUrl: null, ct);

        TempData["Success"] = "Claim approved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, string? reason, CancellationToken ct)
    {
        var claim = await _db.PublicUserProperties.FindAsync(new object[] { id }, ct)
            ?? throw new InvalidOperationException($"Claim {id} not found.");

        claim.Status = PropertyClaimStatus.Rejected;
        claim.RejectionReason = reason;
        await _db.SaveChangesAsync(ct);

        await _notify.NotifyPublicUserAsync(claim.PublicUserId, null,
            "ClaimRejected",
            "Your property claim could not be approved",
            reason ?? "Your claim has been reviewed and could not be approved at this time.",
            actionUrl: null, ct);

        TempData["Error"] = "Claim rejected.";
        return RedirectToAction(nameof(Index));
    }
}
