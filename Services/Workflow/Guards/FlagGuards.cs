using dwa_ver_val.Services.Workflow;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Services.Workflow.Guards;

/// <summary>
/// Leaving CP2 (spatial-info capture) requires SpatialInfoConfirmedAt to be set.
/// The guard matches states whose name starts with "CP2" as the current state.
/// </summary>
public class Cp2SpatialInfoGuard : ITransitionGuard
{
    public Task<GuardResult> CheckAsync(GuardContext ctx)
    {
        if (!IsLeaving(ctx, "CP2")) return Task.FromResult(GuardResult.Ok);
        return Task.FromResult(ctx.FileMaster.SpatialInfoConfirmedAt.HasValue
            ? GuardResult.Ok
            : GuardResult.Deny("CP2 cannot be left until spatial information is confirmed (SG boundaries, catchments, rivers)."));
    }

    internal static bool IsLeaving(GuardContext ctx, string cpPrefix) =>
        ctx.CurrentState.StateName.StartsWith(cpPrefix, StringComparison.OrdinalIgnoreCase)
        && !ctx.TargetState.StateName.StartsWith(cpPrefix, StringComparison.OrdinalIgnoreCase);
}

public class Cp3WarmsReviewedGuard : ITransitionGuard
{
    public Task<GuardResult> CheckAsync(GuardContext ctx)
    {
        if (!Cp2SpatialInfoGuard.IsLeaving(ctx, "CP3")) return Task.FromResult(GuardResult.Ok);
        return Task.FromResult(ctx.FileMaster.WarmsReviewedAt.HasValue
            ? GuardResult.Ok
            : GuardResult.Deny("CP3 cannot be left until WARMS evaluation is recorded."));
    }
}

public class Cp4AdditionalInfoGuard : ITransitionGuard
{
    public Task<GuardResult> CheckAsync(GuardContext ctx)
    {
        if (!Cp2SpatialInfoGuard.IsLeaving(ctx, "CP4")) return Task.FromResult(GuardResult.Ok);
        return Task.FromResult(ctx.FileMaster.AdditionalInfoReviewedAt.HasValue
            ? GuardResult.Ok
            : GuardResult.Deny("CP4 cannot be left until additional information review is recorded."));
    }
}

public class Cp5MapbookPresentGuard : ITransitionGuard
{
    private readonly ApplicationDBContext _db;
    public Cp5MapbookPresentGuard(ApplicationDBContext db) { _db = db; }

    public async Task<GuardResult> CheckAsync(GuardContext ctx)
    {
        if (!Cp2SpatialInfoGuard.IsLeaving(ctx, "CP5")) return GuardResult.Ok;
        var hasMapbook = await _db.Mapbooks.AnyAsync(m => m.FileMasterId == ctx.FileMaster.FileMasterId);
        return hasMapbook
            ? GuardResult.Ok
            : GuardResult.Deny("CP5 cannot be left until at least one Mapbook exists for this case.");
    }
}

public class Cp8DamOrNAGuard : ITransitionGuard
{
    private readonly ApplicationDBContext _db;
    public Cp8DamOrNAGuard(ApplicationDBContext db) { _db = db; }

    public async Task<GuardResult> CheckAsync(GuardContext ctx)
    {
        if (!Cp2SpatialInfoGuard.IsLeaving(ctx, "CP8")) return GuardResult.Ok;
        if (ctx.FileMaster.DamMarkedNA) return GuardResult.Ok;
        var hasDam = await _db.DamCalculations.AnyAsync(d => d.PropertyId == ctx.FileMaster.PropertyId);
        return hasDam
            ? GuardResult.Ok
            : GuardResult.Deny("CP8 cannot be left until a DamCalculation exists or the case is marked Dam N/A.");
    }
}

public class Cp9SfraOrNAGuard : ITransitionGuard
{
    private readonly ApplicationDBContext _db;
    public Cp9SfraOrNAGuard(ApplicationDBContext db) { _db = db; }

    public async Task<GuardResult> CheckAsync(GuardContext ctx)
    {
        if (!Cp2SpatialInfoGuard.IsLeaving(ctx, "CP9")) return GuardResult.Ok;
        if (ctx.FileMaster.SfraMarkedNA) return GuardResult.Ok;
        var hasForestation = await _db.Forestations.AnyAsync(f => f.PropertyId == ctx.FileMaster.PropertyId);
        return hasForestation
            ? GuardResult.Ok
            : GuardResult.Deny("CP9 cannot be left until a Forestation record exists or the case is marked SFRA N/A.");
    }
}

/// <summary>
/// Leaving CP6 requires at least one FieldAndCrop record for this property with
/// a SAPWAT calculation result > 0. This evidences that crop water requirement
/// modelling has been completed for the case.
/// </summary>
public class Cp6FieldCropGuard : ITransitionGuard
{
    private readonly ApplicationDBContext _db;
    public Cp6FieldCropGuard(ApplicationDBContext db) { _db = db; }

    public async Task<GuardResult> CheckAsync(GuardContext ctx)
    {
        if (!Cp2SpatialInfoGuard.IsLeaving(ctx, "CP6")) return GuardResult.Ok;
        var hasResult = await _db.FieldAndCrops
            .AnyAsync(f => f.PropertyId == ctx.FileMaster.PropertyId && f.SAPWATCalculationResult > 0);
        return hasResult
            ? GuardResult.Ok
            : GuardResult.Deny("CP6 cannot be left until at least one Field & Crop record with a SAPWAT calculation result (> 0) exists for this property.");
    }
}

/// <summary>
/// Leaving CP7 requires the FileMaster to have an Entitlement linked — i.e. the
/// ELU volume calculation has been recorded against the case.
/// </summary>
public class Cp7EluGuard : ITransitionGuard
{
    public Task<GuardResult> CheckAsync(GuardContext ctx)
    {
        if (!Cp2SpatialInfoGuard.IsLeaving(ctx, "CP7")) return Task.FromResult(GuardResult.Ok);
        return Task.FromResult(ctx.FileMaster.EntitlementId.HasValue
            ? GuardResult.Ok
            : GuardResult.Deny("CP7 cannot be left until an Entitlement (ELU volume) is linked to this case."));
    }
}

/// <summary>
/// Leaving CP_PrePublicReview requires the approval timestamp to be set AND the
/// acting user must be a Regional Manager or above (sign-off authority).
/// </summary>
public class CpPrePublicReviewGuard : ITransitionGuard
{
    public Task<GuardResult> CheckAsync(GuardContext ctx)
    {
        if (!Cp2SpatialInfoGuard.IsLeaving(ctx, "CP_PrePublicReview")) return Task.FromResult(GuardResult.Ok);
        if (!ctx.FileMaster.PrePublicReviewApprovedAt.HasValue)
            return Task.FromResult(GuardResult.Deny(
                "Pre-public review approval has not been recorded. A Regional Manager or above must approve before the case can proceed."));

        var roles = ctx.UserRoles ?? Array.Empty<string>();
        if (!roles.Any(r => DwsRoles.AtLeastRegionalManager.Contains(r)))
            return Task.FromResult(GuardResult.Deny(
                "Only a Regional Manager or above may approve the pre-public participation review."));

        return Task.FromResult(GuardResult.Ok);
    }
}

/// <summary>
/// Leaving CP_StakeholderWorkshop requires a recorded workshop date and a
/// positive attendance count (workshop actually held with stakeholders).
/// </summary>
public class CpStakeholderWorkshopGuard : ITransitionGuard
{
    public Task<GuardResult> CheckAsync(GuardContext ctx)
    {
        if (!Cp2SpatialInfoGuard.IsLeaving(ctx, "CP_StakeholderWorkshop")) return Task.FromResult(GuardResult.Ok);
        if (!ctx.FileMaster.StakeholderWorkshopDate.HasValue)
            return Task.FromResult(GuardResult.Deny("Stakeholder workshop date has not been recorded."));
        if (ctx.FileMaster.StakeholderWorkshopAttendance is null or <= 0)
            return Task.FromResult(GuardResult.Deny("Stakeholder workshop attendance count must be greater than zero."));
        return Task.FromResult(GuardResult.Ok);
    }
}

/// <summary>
/// Leaving CP11 requires all 9 Appendix A evidence items to be present in the case file.
/// </summary>
public class Cp11FileCompilationGuard : ITransitionGuard
{
    private readonly ApplicationDBContext _db;
    public Cp11FileCompilationGuard(ApplicationDBContext db) { _db = db; }

    public async Task<GuardResult> CheckAsync(GuardContext ctx)
    {
        if (!Cp2SpatialInfoGuard.IsLeaving(ctx, "CP11")) return GuardResult.Ok;

        if (!ctx.FileMaster.WarmsReviewedAt.HasValue)
            return GuardResult.Deny("WARMS review must be recorded before file can be compiled.");

        var property = await _db.Properties.FindAsync(ctx.FileMaster.PropertyId);
        if (property is null || string.IsNullOrWhiteSpace(property.SGCode))
            return GuardResult.Deny("Property SG code must be confirmed before file can be compiled.");

        var hasAuth = await _db.Authorisations.AnyAsync(a => a.FileMasterId == ctx.FileMaster.FileMasterId);
        if (!hasAuth)
            return GuardResult.Deny("At least one authorisation record must be captured before file can be compiled.");

        var hasSapwat = await _db.FieldAndCrops
            .AnyAsync(f => f.PropertyId == ctx.FileMaster.PropertyId && f.SAPWATCalculationResult > 0);
        if (!hasSapwat)
            return GuardResult.Deny("At least one field with a SAPWAT result must be captured before file can be compiled.");

        var hasQualifyingMap = await _db.Mapbooks
            .AnyAsync(m => m.FileMasterId == ctx.FileMaster.FileMasterId && m.MapType == "Qualifying");
        if (!hasQualifyingMap)
            return GuardResult.Deny("Qualifying period mapbook must be present before file can be compiled.");

        if (!ctx.FileMaster.EntitlementId.HasValue)
            return GuardResult.Deny("Entitlement must be linked before file can be compiled.");

        var hasCurrentMap = await _db.Mapbooks
            .AnyAsync(m => m.FileMasterId == ctx.FileMaster.FileMasterId && m.MapType == "Current");
        if (!hasCurrentMap)
            return GuardResult.Deny("Current period mapbook must be present before file can be compiled.");

        if (!ctx.FileMaster.DamMarkedNA)
        {
            var hasDam = await _db.DamCalculations.AnyAsync(d => d.PropertyId == ctx.FileMaster.PropertyId);
            if (!hasDam)
                return GuardResult.Deny("Dam volume calculation must be recorded or marked N/A before file can be compiled.");
        }

        if (!ctx.FileMaster.SfraMarkedNA)
        {
            var hasSfra = await _db.Forestations.AnyAsync(f => f.PropertyId == ctx.FileMaster.PropertyId);
            if (!hasSfra)
                return GuardResult.Deny("SFRA/Forestation record must be recorded or marked N/A before file can be compiled.");
        }

        return GuardResult.Ok;
    }
}
