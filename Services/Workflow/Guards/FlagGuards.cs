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
