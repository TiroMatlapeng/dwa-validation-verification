using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Helpers;
using dwa_ver_val.Services.Documents;
using dwa_ver_val.Services.Infrastructure.Storage;
using dwa_ver_val.Services.Notifications;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dwa_ver_val.Areas.ExternalPortal.Controllers;

[Area("ExternalPortal")]
[Route("ExternalPortal/[controller]/[action]")]
[Authorize(Policy = PortalPolicies.PortalAuthenticated)]
public class DocumentController : Controller
{
    private static readonly HashSet<string> _allowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".jpg", ".jpeg", ".png" };

    private readonly ApplicationDBContext _db;
    private readonly IPublicUserPropertyAccessor _access;
    private readonly IFileStorage _storage;
    private readonly INotificationService _notify;

    public DocumentController(
        ApplicationDBContext db,
        IPublicUserPropertyAccessor access,
        IFileStorage storage,
        INotificationService notify)
    {
        _db = db;
        _access = access;
        _storage = storage;
        _notify = notify;
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Not authenticated."));

    [HttpGet]
    public IActionResult Upload(Guid fileMasterId)
    {
        return View(new DocumentUploadViewModel { FileMasterId = fileMasterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload(DocumentUploadViewModel model, CancellationToken ct)
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

        // DOC-03: validate document type against the controlled vocabulary.
        if (!DocumentTypes.IsKnown(model.DocumentType))
            ModelState.AddModelError(nameof(model.DocumentType), "Unknown document type.");

        if (model.File is null || model.File.Length <= 0)
        {
            ModelState.AddModelError(nameof(model.File), "Please select a file.");
        }
        else
        {
            var ext = Path.GetExtension(model.File.FileName);
            if (!_allowedExtensions.Contains(ext))
                ModelState.AddModelError(nameof(model.File), "Only PDF, JPG, and PNG files are accepted.");

            // DOC-03: explicit size cap (mirrors the [RequestSizeLimit] attribute, 10 MB).
            const long ExternalMaxBytes = 10L * 1024 * 1024;
            if (model.File.Length > ExternalMaxBytes)
                ModelState.AddModelError(nameof(model.File), "File exceeds the 10 MB limit.");

            // DOC-02: magic-byte content validation — check after extension, before saving.
            if (ModelState.IsValid)
            {
                using var peekStream = model.File.OpenReadStream();
                if (!FileSignatureValidator.MatchesExtension(peekStream, ext))
                    ModelState.AddModelError(nameof(model.File), "File content does not match its extension.");
            }
        }

        if (!ModelState.IsValid) return View(model);

        using var stream = model.File!.OpenReadStream();
        var stored = await _storage.SaveAsync(
            stream,
            model.File.ContentType ?? "application/octet-stream",
            model.File.FileName,
            ct);

        var doc = new Document
        {
            DocumentId = Guid.NewGuid(),
            FileMasterId = model.FileMasterId,
            DocumentType = model.DocumentType,
            FileName = model.File.FileName,
            BlobPath = stored.RelativePath,
            ContentType = stored.ContentType,
            FileSizeBytes = stored.SizeBytes,
            UploadedByPublicUserId = uid,
            UploadDate = DateTime.UtcNow,
            VirusScanStatus = "Pending",
            DocumentHash = stored.Sha256Hex
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync(ct);

        await _notify.NotifyDwsValidatorAsync(
            model.FileMasterId,
            "Upload",
            "Water user uploaded a document",
            $"A {model.DocumentType} document ({model.File.FileName}) was uploaded by the water user.",
            ct);

        return RedirectToAction("Detail", "Case", new { area = "ExternalPortal", id = model.FileMasterId });
    }
}
