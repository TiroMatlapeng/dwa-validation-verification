using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Helpers;
using dwa_ver_val.Services.Letters;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Areas.ExternalPortal.Controllers;

[Area("ExternalPortal")]
[Route("ExternalPortal/[controller]/[action]")]
[Authorize(Policy = PortalPolicies.PortalAuthenticated)]
public class CaseController : Controller
{
    private readonly ApplicationDBContext _db;
    private readonly IPublicUserPropertyAccessor _access;
    private readonly IBlobStore _blobs;

    public CaseController(ApplicationDBContext db, IPublicUserPropertyAccessor access, IBlobStore blobs)
    {
        _db = db;
        _access = access;
        _blobs = blobs;
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Not authenticated."));

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var uid = CurrentUserId();
        var approvedPropertyIds = await _access.GetApprovedPropertyIdsAsync(uid, ct);
        var idList = approvedPropertyIds.ToList();

        var cases = await _db.FileMasters
            .Include(f => f.Property)
            .Where(f => idList.Contains(f.PropertyId))
            .ToListAsync(ct);

        var caseIds = cases.Select(f => f.FileMasterId).ToList();

        var unread = await _db.Notifications
            .Where(n => n.PublicUserId == uid
                && !n.IsRead
                && n.FileMasterId != null
                && caseIds.Contains(n.FileMasterId!.Value))
            .GroupBy(n => n.FileMasterId!.Value)
            .Select(g => new { FileMasterId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var unreadMap = unread.ToDictionary(u => u.FileMasterId, u => u.Count);

        var vms = cases.Select(f => new CaseSummaryViewModel
        {
            FileMasterId = f.FileMasterId,
            FarmName = f.FarmName,
            PropertyReference = f.Property?.PropertyReferenceNumber ?? "",
            SGCode = f.Property?.SGCode ?? "",
            WorkflowState = null,
            UnreadNotifications = unreadMap.TryGetValue(f.FileMasterId, out var c) ? c : 0
        }).ToList();

        return View(vms);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var uid = CurrentUserId();
        try
        {
            await _access.AssertHasAccessToFileMasterAsync(uid, id, ct);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        var fm = await _db.FileMasters
            .Include(f => f.Property)
            .FirstOrDefaultAsync(f => f.FileMasterId == id, ct);
        if (fm is null) return NotFound();

        var letters = await _db.LetterIssuances
            .Include(l => l.LetterType)
            .Where(l => l.FileMasterId == id)
            .OrderBy(l => l.IssuedDate)
            .ToListAsync(ct);

        var comments = await _db.CaseComments
            .Where(c => c.FileMasterId == id && c.PublicUserId == uid)
            .OrderByDescending(c => c.SubmittedDate)
            .ToListAsync(ct);

        var docs = await _db.Documents
            .Where(d => d.FileMasterId == id && d.UploadedByPublicUserId == uid)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync(ct);

        var objections = await _db.Objections
            .Where(o => o.FileMasterId == id && o.PublicUserId == uid)
            .OrderByDescending(o => o.LodgedDate)
            .ToListAsync(ct);

        var unreadNotes = await _db.Notifications
            .Where(n => n.PublicUserId == uid && n.FileMasterId == id && !n.IsRead)
            .ToListAsync(ct);
        foreach (var n in unreadNotes) { n.IsRead = true; n.ReadDate = DateTime.UtcNow; }
        if (unreadNotes.Count > 0) await _db.SaveChangesAsync(ct);

        return View(new CaseDetailViewModel
        {
            FileMaster = fm,
            Letters = letters,
            Comments = comments,
            Documents = docs,
            Objections = objections
        });
    }

    [HttpGet("{fileMasterId:guid}/{issuanceId:guid}")]
    public async Task<IActionResult> DownloadLetter(Guid fileMasterId, Guid issuanceId, CancellationToken ct)
    {
        var uid = CurrentUserId();
        try
        {
            await _access.AssertHasAccessToFileMasterAsync(uid, fileMasterId, ct);
        }
        catch (NotFoundException)
        {
            return Forbid();
        }

        var issuance = await _db.LetterIssuances
            .FirstOrDefaultAsync(l => l.LetterIssuanceId == issuanceId && l.FileMasterId == fileMasterId, ct);
        if (issuance is null || string.IsNullOrEmpty(issuance.BlobPath)) return NotFound();

        var bytes = await _blobs.ReadAsync(issuance.BlobPath);
        var datePart = issuance.IssuedDate?.ToString("yyyyMMdd") ?? "undated";
        return File(bytes, "application/pdf",
            $"letter-{datePart}-{issuanceId.ToString()[..8]}.pdf");
    }
}
