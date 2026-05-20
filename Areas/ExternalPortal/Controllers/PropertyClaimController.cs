using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Models.Enums;
using dwa_ver_val.Services.Notifications;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Areas.ExternalPortal.Controllers;

[Area("ExternalPortal")]
[Route("ExternalPortal/[controller]/[action]")]
[Authorize(Policy = PortalPolicies.PortalAuthenticated)]
public class PropertyClaimController : Controller
{
    private readonly ApplicationDBContext _db;
    private readonly INotificationService _notify;

    public PropertyClaimController(ApplicationDBContext db, INotificationService notify)
    {
        _db = db;
        _notify = notify;
    }

    [HttpGet]
    public IActionResult Submit() => View(new PropertyClaimViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(PropertyClaimViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(vm);

        var publicUserId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Portal user not authenticated."));

        var property = await _db.Properties
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.SGCode == vm.PropertyCode ||
                p.PropertyReferenceNumber == vm.PropertyCode, ct);

        if (property is null)
        {
            ModelState.AddModelError(nameof(vm.PropertyCode),
                "No property found with that code. Check your title deed or SGCode.");
            return View(vm);
        }

        var alreadyExists = await _db.PublicUserProperties.AnyAsync(p =>
            p.PublicUserId == publicUserId &&
            p.PropertyId == property.PropertyId, ct);

        if (alreadyExists)
        {
            ModelState.AddModelError(nameof(vm.PropertyCode),
                "You have already submitted a claim for this property.");
            return View(vm);
        }

        _db.PublicUserProperties.Add(new PublicUserProperty
        {
            Id = Guid.NewGuid(),
            PublicUserId = publicUserId,
            PropertyId = property.PropertyId,
            Status = PropertyClaimStatus.Pending,
            EvidenceType = PropertyClaimEvidenceType.IdMatch,
            RequestedDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Pending));
    }

    [HttpGet]
    public IActionResult Pending() => View();
}
