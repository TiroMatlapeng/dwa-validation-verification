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
    private readonly IVirusScanner _virusScanner;

    public DocumentController(
        ApplicationDBContext db, IScopedCaseQuery scope, IFileStorage storage,
        IAuditService audit, IVirusScanner virusScanner)
    {
        _db = db; _scope = scope; _storage = storage; _audit = audit; _virusScanner = virusScanner;
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Not authenticated."));

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (raw is not null && Guid.TryParse(raw, out userId)) return true;
        userId = default;
        return false;
    }

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

        // DOC-02: virus scan — after magic-byte validation, before persisting the blob/row.
        // Fail-closed: only a Clean verdict is allowed; Infected or scanner Error rejects the upload.
        if (ModelState.IsValid && model.File is { Length: > 0 })
        {
            using var scanStream = model.File.OpenReadStream();
            var scan = await _virusScanner.ScanAsync(scanStream, ct);
            if (scan != VirusScanResult.Clean)
                ModelState.AddModelError(nameof(model.File),
                    "The file failed virus scanning and was rejected. Please upload a clean file.");
        }

        if (!ModelState.IsValid) return View(model);

        if (!TryGetCurrentUserId(out var uid)) return Forbid();
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
            VirusScanStatus = "Clean", // DOC-02: only Clean content reaches here (Infected/Error rejected above)
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

        // DOC-02: fail-closed virus-scan gate. Serve ONLY documents whose scan came back "Clean".
        // Anything else (Infected, Pending, Error, or null/not-yet-scanned) is not downloadable.
        if (doc.VirusScanStatus != "Clean")
            return NotFound();

        var stream = await _storage.OpenReadAsync(doc.BlobPath, ct);
        return File(stream, doc.ContentType ?? "application/octet-stream", doc.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanManageDocuments)]
    public async Task<IActionResult> Delete(Guid documentId, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var uid)) return Forbid();

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
            UserId: uid,
            UserDisplayName: User.Identity?.Name,
            FromValue: snapshot,
            IPAddress: HttpContext.Connection.RemoteIpAddress?.ToString()));

        return RedirectToAction("Details", "FileMaster", new { id = fileMasterId });
    }
}
