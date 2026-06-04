using System.Security.Claims;
using dwa_ver_val.Services.Audit;
using dwa_ver_val.Services.Documents;
using dwa_ver_val.Services.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Controllers;

[Authorize(Policy = DwsPolicies.CanRead)]
public class DocumentController : Controller
{
    // TIFF is allowed for internal uploads (scanned title deeds / GIS diagrams), unlike the external portal.
    private static readonly HashSet<string> _allowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".jpg", ".jpeg", ".png", ".tiff" };
    private const long MaxBytes = 25 * 1024 * 1024;

    private readonly ApplicationDBContext _db;
    private readonly IScopedCaseQuery _scope;
    private readonly IFileStorage _storage;
    private readonly IAuditService _audit;

    public DocumentController(ApplicationDBContext db, IScopedCaseQuery scope, IFileStorage storage, IAuditService audit)
    {
        _db = db; _scope = scope; _storage = storage; _audit = audit;
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Not authenticated."));

    private async Task<FileMaster?> ScopedCaseAsync(Guid fileMasterId)
    {
        var fm = await _db.FileMasters.Include(f => f.Property)
            .FirstOrDefaultAsync(f => f.FileMasterId == fileMasterId);
        if (fm is null) return null;
        return _scope.IsInScope(fm, User) ? fm : null;
    }

    [HttpGet]
    [Authorize(Policy = DwsPolicies.CanCapture)]
    public async Task<IActionResult> Upload(Guid fileMasterId)
    {
        if (await ScopedCaseAsync(fileMasterId) is null) return Forbid();
        return View(new CaseDocumentUploadViewModel { FileMasterId = fileMasterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCapture)]
    [RequestSizeLimit(MaxBytes)]
    public async Task<IActionResult> Upload(CaseDocumentUploadViewModel model, CancellationToken ct)
    {
        var fm = await ScopedCaseAsync(model.FileMasterId);
        if (fm is null) return Forbid();

        if (!DocumentTypes.IsKnown(model.DocumentType))
            ModelState.AddModelError(nameof(model.DocumentType), "Unknown document type.");

        if (model.File is null || model.File.Length <= 0)
            ModelState.AddModelError(nameof(model.File), "Please select a file.");
        else
        {
            var ext = Path.GetExtension(model.File.FileName);
            if (!_allowedExtensions.Contains(ext))
                ModelState.AddModelError(nameof(model.File), "Only PDF, JPG, PNG, and TIFF files are accepted.");
            if (model.File.Length > MaxBytes)
                ModelState.AddModelError(nameof(model.File), "File exceeds the 25 MB limit.");
        }

        // DOC-02: magic-byte content validation — must come after extension check, before saving.
        if (ModelState.IsValid && model.File is { Length: > 0 })
        {
            var ext = Path.GetExtension(model.File.FileName);
            using var peekStream = model.File.OpenReadStream();
            if (!FileSignatureValidator.MatchesExtension(peekStream, ext))
                ModelState.AddModelError(nameof(model.File), "File content does not match its extension.");
        }

        if (!ModelState.IsValid) return View(model);

        var uid = CurrentUserId();
        using var stream = model.File!.OpenReadStream();
        var stored = await _storage.SaveAsync(
            stream, model.File.ContentType ?? "application/octet-stream", model.File.FileName, ct);

        var doc = new Document
        {
            DocumentId = Guid.NewGuid(),
            FileMasterId = model.FileMasterId,
            WorkflowStateId = model.WorkflowStateId,
            DocumentType = model.DocumentType,
            FileName = model.File.FileName,
            BlobPath = stored.RelativePath,
            ContentType = stored.ContentType,
            FileSizeBytes = stored.SizeBytes,
            UploadedByUserId = uid,
            UploadDate = DateTime.UtcNow,
            VirusScanStatus = "Pending",
            SyncStatus = "NotSynced",
            DocumentHash = stored.Sha256Hex
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEvent(
            EntityType: "Document",
            EntityId: doc.DocumentId.ToString(),
            Action: "DocumentUploaded",
            UserId: uid,
            UserDisplayName: User.Identity?.Name,
            ToValue: $"{model.DocumentType}:{model.File.FileName}",
            IPAddress: HttpContext.Connection.RemoteIpAddress?.ToString()));

        return RedirectToAction("Details", "FileMaster", new { id = model.FileMasterId });
    }

    [HttpGet]
    public async Task<IActionResult> Download(Guid documentId, CancellationToken ct)
    {
        var doc = await _db.Documents.Include(d => d.FileMaster).ThenInclude(f => f!.Property)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId, ct);
        if (doc?.FileMaster is null || !_scope.IsInScope(doc.FileMaster, User)) return Forbid();

        // DOC-02: real AV scanning deferred (no scanner wired). Until an IVirusScanner sets "Clean"/"Infected",
        // we block only "Infected"; tighten to require "Clean" once scanning exists.
        if (doc.VirusScanStatus == "Infected")
            return BadRequest("File failed virus scanning and cannot be downloaded.");

        var stream = await _storage.OpenReadAsync(doc.BlobPath, ct);
        return File(stream, doc.ContentType ?? "application/octet-stream", doc.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanManageDocuments)]
    public async Task<IActionResult> Delete(Guid documentId, CancellationToken ct)
    {
        var doc = await _db.Documents.Include(d => d.FileMaster).ThenInclude(f => f!.Property)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId, ct);
        if (doc?.FileMaster is null || !_scope.IsInScope(doc.FileMaster, User)) return Forbid();

        var fileMasterId = doc.FileMasterId;
        var snapshot = $"{doc.DocumentType}:{doc.FileName}";
        var blobPath = doc.BlobPath;

        _db.Documents.Remove(doc);
        await _db.SaveChangesAsync(ct);
        await _storage.DeleteAsync(blobPath, ct); // after commit: a dangling blob is safer than a row pointing at a missing file

        await _audit.LogAsync(new AuditEvent(
            EntityType: "Document",
            EntityId: documentId.ToString(),
            Action: "DocumentDeleted",
            UserId: CurrentUserId(),
            UserDisplayName: User.Identity?.Name,
            FromValue: snapshot,
            IPAddress: HttpContext.Connection.RemoteIpAddress?.ToString()));

        return RedirectToAction("Details", "FileMaster", new { id = fileMasterId });
    }
}
