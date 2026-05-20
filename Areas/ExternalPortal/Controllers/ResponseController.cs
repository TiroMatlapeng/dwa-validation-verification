using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Helpers;
using dwa_ver_val.Services.Notifications;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dwa_ver_val.Areas.ExternalPortal.Controllers;

[Area("ExternalPortal")]
[Route("ExternalPortal/[controller]/[action]")]
[Authorize(Policy = PortalPolicies.PortalAuthenticated)]
public class ResponseController : Controller
{
    private readonly ApplicationDBContext _db;
    private readonly IPublicUserPropertyAccessor _access;
    private readonly INotificationService _notify;

    public ResponseController(
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
    public IActionResult Submit(Guid fileMasterId, Guid? issuanceId)
    {
        return View(new LetterResponseViewModel
        {
            FileMasterId = fileMasterId,
            LetterIssuanceId = issuanceId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(LetterResponseViewModel model, CancellationToken ct)
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

        var comment = new CaseComment
        {
            CommentId = Guid.NewGuid(),
            FileMasterId = model.FileMasterId,
            PublicUserId = uid,
            AuthorType = "PublicUser",
            CommentText = model.ResponseText,
            SubmittedDate = DateTime.UtcNow
        };
        _db.CaseComments.Add(comment);
        await _db.SaveChangesAsync(ct);

        var snippet = model.ResponseText.Length > 200
            ? model.ResponseText[..200] + "..."
            : model.ResponseText;

        await _notify.NotifyDwsValidatorAsync(
            model.FileMasterId,
            "Comment",
            "Water user submitted a response",
            snippet,
            ct);

        return RedirectToAction("Detail", "Case", new { area = "ExternalPortal", id = model.FileMasterId });
    }
}
