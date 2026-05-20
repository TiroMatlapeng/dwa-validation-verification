using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Helpers;
using dwa_ver_val.Services.Notifications;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Areas.ExternalPortal.Controllers;

[Area("ExternalPortal")]
[Route("ExternalPortal/[controller]/[action]")]
[Authorize(Policy = PortalPolicies.PortalAuthenticated)]
public class ObjectionController : Controller
{
    private readonly ApplicationDBContext _db;
    private readonly IPublicUserPropertyAccessor _access;
    private readonly INotificationService _notify;

    public ObjectionController(
        ApplicationDBContext db,
        IPublicUserPropertyAccessor access,
        INotificationService notify)
    {
        _db = db;
        _access = access;
        _notify = notify;
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Not authenticated."));

    [HttpGet]
    public IActionResult Lodge(Guid fileMasterId)
    {
        return View(new ObjectionViewModel { FileMasterId = fileMasterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lodge(ObjectionViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var uid = CurrentUserId();
        try
        {
            await _access.AssertHasAccessToFileMasterAsync(uid, model.FileMasterId, ct);
        }
        catch (NotFoundException)
        {
            return Forbid();
        }

        var existing = await _db.Objections
            .AnyAsync(o => o.FileMasterId == model.FileMasterId
                && o.PublicUserId == uid
                && o.Status == "Lodged", ct);
        if (existing)
        {
            ModelState.AddModelError(string.Empty,
                "You already have an open objection lodged on this case. Please wait for it to be resolved before lodging another.");
            return View(model);
        }

        var objection = new Objection
        {
            ObjectionId = Guid.NewGuid(),
            FileMasterId = model.FileMasterId,
            PublicUserId = uid,
            LodgedDate = DateTime.UtcNow,
            Status = "Lodged",
            Grounds = model.Grounds
        };
        try
        {
            _db.Objections.Add(objection);
            await _db.SaveChangesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "You already have an open objection on this case.");
            return View(model);
        }

        var snippet = model.Grounds.Length > 200
            ? model.Grounds[..200] + "..."
            : model.Grounds;

        await _notify.NotifyDwsValidatorAsync(
            model.FileMasterId,
            "Protest",
            "Water user lodged an objection / appeal",
            snippet,
            ct);

        return RedirectToAction("Detail", "Case", new { area = "ExternalPortal", id = model.FileMasterId });
    }
}
