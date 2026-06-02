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

            foreach (var req in reqs)
            {
                var present = await _db.Documents.AnyAsync(d =>
                    d.FileMasterId == ctx.FileMaster.FileMasterId
                    && d.DocumentType == req.DocumentType
                    && d.VirusScanStatus != "Infected");

                if (!present)
                    return GuardResult.Deny(
                        $"{req.DisplayName} must be uploaded before leaving this control point.");
            }
        }
        return GuardResult.Ok;
    }
}
