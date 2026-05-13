using System.Security.Cryptography;
using dwa_ver_val.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Services.Letters;

public class LetterService : ILetterService
{
    private readonly ApplicationDBContext _db;
    private readonly ILetterTemplateRegistry _templates;
    private readonly IPdfRenderer _renderer;
    private readonly IBlobStore _blobs;
    private readonly IAuditService _audit;

    public LetterService(
        ApplicationDBContext db,
        ILetterTemplateRegistry templates,
        IPdfRenderer renderer,
        IBlobStore blobs,
        IAuditService audit)
    {
        _db = db;
        _templates = templates;
        _renderer = renderer;
        _blobs = blobs;
        _audit = audit;
    }

    public async Task<byte[]> RenderPreviewAsync(Guid fileMasterId, string letterCode)
    {
        var template = _templates.Get(letterCode);
        // Preview uses a placeholder signatory — production code would pass the current user's claims.
        var ctx = await BuildContextAsync(fileMasterId, letterCode, DateOnly.FromDateTime(DateTime.UtcNow),
            dueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)),
            signatoryName: "(preview signatory)", signatoryTitle: "Regional Manager", signatoryOrgUnit: "Regional Office",
            recipientName: "(preview recipient)", recipientAddress: null,
            irrigationBoardName: null, additionalNotes: "PREVIEW — not a valid issued letter.",
            lawfulVolumeM3: null, unlawfulVolumeM3: null);
        return _renderer.RenderLetter(template, ctx);
    }

    public async Task<LetterIssuance> IssueAsync(Guid fileMasterId, string letterCode, IssueLetterRequest req)
    {
        var template = _templates.Get(letterCode);
        var letterType = await _db.LetterTypes.SingleOrDefaultAsync(t => t.LetterName == letterCode)
            ?? throw new InvalidOperationException($"LetterType with LetterName '{letterCode}' not seeded.");

        // Personal-service requirement per S35(2)(d) for Letter 1.
        if (string.Equals(letterCode, "S35_L1", StringComparison.OrdinalIgnoreCase)
            && string.Equals(req.IssueMethod, "InPerson", StringComparison.OrdinalIgnoreCase)
            && req.ServedByOfficialId is null)
        {
            throw new InvalidOperationException("S35 Letter 1 served in person requires a ServedByOfficialId.");
        }

        var referenceNumber = await NextReferenceNumberAsync(fileMasterId, letterCode);

        var ctx = await BuildContextAsync(fileMasterId, letterCode, req.IssueDate, req.DueDate,
            signatoryName: req.SignedByDisplayName, signatoryTitle: req.SignedByTitle, signatoryOrgUnit: req.SignedByOrgUnit,
            recipientName: req.RecipientName, recipientAddress: req.RecipientAddress,
            irrigationBoardName: req.IrrigationBoardName, additionalNotes: req.AdditionalNotes,
            lawfulVolumeM3: req.LawfulVolumeM3, unlawfulVolumeM3: req.UnlawfulVolumeM3,
            referenceNumber: referenceNumber);

        var pdfBytes = _renderer.RenderLetter(template, ctx);
        var blobPath = await _blobs.WriteAsync($"letters/{referenceNumber}.pdf", pdfBytes);
        var hash = Convert.ToHexString(SHA256.HashData(pdfBytes));

        var issuance = new LetterIssuance
        {
            LetterIssuanceId = Guid.NewGuid(),
            FileMasterId = fileMasterId,
            LetterTypeId = letterType.LetterTypeId,
            IssuedDate = req.IssueDate,
            DueDate = req.DueDate,
            IssueMethod = req.IssueMethod,
            GeneratedDate = req.IssueDate,
            SignedDate = req.IssueDate,
            SignedById = req.SignedByUserId,
            ServedByOfficialId = req.ServedByOfficialId,
            ServingOfficialName = req.ServedByOfficialId.HasValue ? req.SignedByDisplayName : null,
            BlobPath = blobPath,
            SignatureHash = hash,
            ResponseStatus = "Pending"
        };
        _db.LetterIssuances.Add(issuance);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(new AuditEvent(
            EntityType: nameof(LetterIssuance),
            EntityId: issuance.LetterIssuanceId.ToString(),
            Action: "LetterIssued",
            UserId: req.SignedByUserId,
            UserDisplayName: req.SignedByDisplayName,
            ToValue: $"{letterCode} (#{referenceNumber})",
            Reason: req.AdditionalNotes));

        return issuance;
    }

    private async Task<LetterContext> BuildContextAsync(
        Guid fileMasterId, string letterCode, DateOnly issueDate, DateOnly? dueDate,
        string signatoryName, string signatoryTitle, string signatoryOrgUnit,
        string recipientName, string? recipientAddress,
        string? irrigationBoardName, string? additionalNotes,
        decimal? lawfulVolumeM3, decimal? unlawfulVolumeM3,
        string? referenceNumber = null)
    {
        var fm = await _db.FileMasters
            .Include(f => f.Property)
                .ThenInclude(p => p!.PropertyOwnerships).ThenInclude(po => po.PropertyOwner)
            .FirstOrDefaultAsync(f => f.FileMasterId == fileMasterId)
            ?? throw new InvalidOperationException($"FileMaster {fileMasterId} not found.");

        referenceNumber ??= $"VV-{fm.PropertyId.ToString()[..4]}-{issueDate:yyyyMMdd}-{letterCode}";
        var caseNumber = fm.CaseNumber ?? fm.RegistrationNumber;
        var propertyRef = fm.Property?.SGCode ?? fm.Property?.PropertyReferenceNumber ?? fm.PropertyId.ToString();

        return new LetterContext(
            ReferenceNumber: referenceNumber,
            IssueDate: issueDate,
            DueDate: dueDate,
            CaseNumber: caseNumber,
            FarmName: fm.FarmName,
            PropertyReference: propertyRef,
            RecipientName: recipientName,
            RecipientAddress: recipientAddress,
            IrrigationBoardName: irrigationBoardName,
            SignatoryName: signatoryName,
            SignatoryTitle: signatoryTitle,
            SignatoryOrgUnit: signatoryOrgUnit,
            LawfulVolumeM3: lawfulVolumeM3,
            UnlawfulVolumeM3: unlawfulVolumeM3,
            AdditionalNotes: additionalNotes);
    }

    private async Task<string> NextReferenceNumberAsync(Guid fileMasterId, string letterCode)
    {
        var count = await _db.LetterIssuances.CountAsync(l => l.FileMasterId == fileMasterId);
        return $"VV-{fileMasterId.ToString()[..8].ToUpperInvariant()}-{(count + 1):D3}-{letterCode}";
    }
}
