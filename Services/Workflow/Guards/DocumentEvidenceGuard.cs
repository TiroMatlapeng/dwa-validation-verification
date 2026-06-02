using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Services.Workflow.Guards;

/// <summary>
/// Blocks leaving a control point until every MANDATORY document for that CP (per
/// DocumentRequirements.Map) has at least one non-"Infected" Document on the case.
/// Null/Pending/Clean virus statuses are acceptable (no AV scanner is wired yet).
/// </summary>
public class DocumentEvidenceGuard : ITransitionGuard
{
    private readonly ApplicationDBContext _db;
    public DocumentEvidenceGuard(ApplicationDBContext db) { _db = db; }

    public async Task<GuardResult> CheckAsync(GuardContext ctx)
    {
        foreach (var (cpPrefix, reqs) in DocumentRequirements.Map)
        {
            if (!Cp2SpatialInfoGuard.IsLeaving(ctx, cpPrefix)) continue;

            var missing = new List<string>();
            foreach (var req in reqs)
            {
                // FileMasterId is nullable on Document — null rows are correctly excluded by equality.
                // VirusScanStatus null/Pending/Clean satisfy; only "Infected" blocks. The explicit
                // null check keeps that intent even if relational-null semantics are ever changed.
                var present = await _db.Documents.AnyAsync(d =>
                    d.FileMasterId == ctx.FileMaster.FileMasterId
                    && d.DocumentType == req.DocumentType
                    && (d.VirusScanStatus == null || d.VirusScanStatus != "Infected"));

                if (!present) missing.Add(req.DisplayName);
            }

            if (missing.Count > 0)
                return GuardResult.Deny(
                    $"The following document(s) must be uploaded before leaving this control point: {string.Join(", ", missing)}.");
        }
        return GuardResult.Ok;
    }
}
