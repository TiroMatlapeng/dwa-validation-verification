using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

public class ScopedCaseQuery : IScopedCaseQuery
{
    private readonly ApplicationDBContext _db;

    public ScopedCaseQuery(ApplicationDBContext db)
    {
        _db = db;
    }

    private static bool BypassesScope(ClaimsPrincipal user) =>
        user.IsInRole(DwsRoles.SystemAdmin) || user.IsInRole(DwsRoles.NationalManager);

    // ── Effective scope resolution (narrowest wins) ───────────────────────────

    private enum ScopeLevel { None, Province, Wma, Catchment }

    private readonly record struct EffectiveScope(ScopeLevel Level, Guid ScopeId);

    /// <summary>
    /// Reads the narrowest non-empty scope claim from the principal.
    /// Precedence: catchmentId > wmaId > provinceId > none.
    /// </summary>
    private static EffectiveScope GetEffectiveScope(ClaimsPrincipal user)
    {
        var catchmentClaim = user.FindFirst("catchmentId")?.Value;
        if (Guid.TryParse(catchmentClaim, out var catchment))
            return new(ScopeLevel.Catchment, catchment);

        var wmaClaim = user.FindFirst("wmaId")?.Value;
        if (Guid.TryParse(wmaClaim, out var wma))
            return new(ScopeLevel.Wma, wma);

        var provinceClaim = user.FindFirst("provinceId")?.Value;
        if (Guid.TryParse(provinceClaim, out var province))
            return new(ScopeLevel.Province, province);

        return new(ScopeLevel.None, Guid.Empty);
    }

    // ── Public interface ──────────────────────────────────────────────────────

    public IQueryable<FileMaster> FilterFileMasters(IQueryable<FileMaster> source, ClaimsPrincipal user)
    {
        if (BypassesScope(user)) return source;

        var scope = GetEffectiveScope(user);
        return scope.Level switch
        {
            ScopeLevel.Catchment => source.Where(fm => fm.Property!.CatchmentAreaId == scope.ScopeId),
            ScopeLevel.Wma       => source.Where(fm => fm.Property!.WmaId == scope.ScopeId),
            ScopeLevel.Province  => source.Where(fm => fm.Property!.WaterManagementArea!.ProvinceId == scope.ScopeId),
            _                    => source.Where(_ => false),
        };
    }

    public IQueryable<Property> FilterProperties(IQueryable<Property> source, ClaimsPrincipal user)
    {
        if (BypassesScope(user)) return source;

        var scope = GetEffectiveScope(user);
        return scope.Level switch
        {
            ScopeLevel.Catchment => source.Where(p => p.CatchmentAreaId == scope.ScopeId),
            ScopeLevel.Wma       => source.Where(p => p.WmaId == scope.ScopeId),
            ScopeLevel.Province  => source.Where(p => p.WaterManagementArea!.ProvinceId == scope.ScopeId),
            _                    => source.Where(_ => false),
        };
    }

    public IQueryable<WaterManagementArea> FilterWaterManagementAreas(IQueryable<WaterManagementArea> source, ClaimsPrincipal user)
    {
        if (BypassesScope(user)) return source;

        var scope = GetEffectiveScope(user);
        return scope.Level switch
        {
            // Catchment-scoped: the user belongs to exactly one catchment; show the owning WMA.
            ScopeLevel.Catchment => source.Where(wma =>
                _db.CatchmentAreas.Any(c => c.CatchmentAreaId == scope.ScopeId && c.WmaId == wma.WmaId)),

            // WMA-scoped: exactly the one WMA they belong to.
            ScopeLevel.Wma      => source.Where(wma => wma.WmaId == scope.ScopeId),

            // Province-scoped: all WMAs in that province.
            ScopeLevel.Province => source.Where(wma => wma.ProvinceId == scope.ScopeId),

            // No valid scope → empty.
            _                   => source.Where(_ => false),
        };
    }

    public bool IsInScope(FileMaster fileMaster, ClaimsPrincipal user)
    {
        if (BypassesScope(user)) return true;

        var scope = GetEffectiveScope(user);
        if (scope.Level == ScopeLevel.None) return false;

        // Try to resolve the needed values from the nav property first (avoids a DB round-trip).
        var prop = fileMaster.Property;

        switch (scope.Level)
        {
            case ScopeLevel.Catchment:
            {
                var catchmentId = prop?.CatchmentAreaId
                    ?? _db.Properties.AsNoTracking()
                        .Where(p => p.PropertyId == fileMaster.PropertyId)
                        .Select(p => p.CatchmentAreaId)
                        .FirstOrDefault();
                return catchmentId == scope.ScopeId;
            }

            case ScopeLevel.Wma:
            {
                var wmaId = prop?.WmaId
                    ?? _db.Properties.AsNoTracking()
                        .Where(p => p.PropertyId == fileMaster.PropertyId)
                        .Select(p => p.WmaId)
                        .FirstOrDefault();
                return wmaId == scope.ScopeId;
            }

            case ScopeLevel.Province:
            {
                // Province requires a join through WaterManagementArea.
                Guid? provinceId = null;

                if (prop?.WaterManagementArea is { } wma)
                {
                    provinceId = wma.ProvinceId;
                }
                else
                {
                    provinceId = _db.Properties.AsNoTracking()
                        .Where(p => p.PropertyId == fileMaster.PropertyId)
                        .Select(p => (Guid?)p.WaterManagementArea!.ProvinceId)
                        .FirstOrDefault();
                }

                return provinceId == scope.ScopeId;
            }

            default:
                return false;
        }
    }
}
