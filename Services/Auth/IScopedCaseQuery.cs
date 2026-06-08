using System.Security.Claims;

public interface IScopedCaseQuery
{
    IQueryable<FileMaster> FilterFileMasters(IQueryable<FileMaster> source, ClaimsPrincipal user);

    IQueryable<Property> FilterProperties(IQueryable<Property> source, ClaimsPrincipal user);

    /// <summary>
    /// Filters a <see cref="WaterManagementArea"/> queryable to only those the user is
    /// permitted to see, mirroring the same scope logic as <see cref="FilterProperties"/>.
    /// Used to populate the WMA dropdown in the report filter form.
    ///
    /// Scope rules (narrowest wins):
    /// <list type="bullet">
    ///   <item>SystemAdmin / NationalManager → all WMAs (bypass).</item>
    ///   <item>Catchment scope → the single WMA that owns that catchment.</item>
    ///   <item>WMA scope → exactly that one WMA.</item>
    ///   <item>Province scope → all WMAs in that province.</item>
    ///   <item>None → empty set.</item>
    /// </list>
    /// </summary>
    IQueryable<WaterManagementArea> FilterWaterManagementAreas(IQueryable<WaterManagementArea> source, ClaimsPrincipal user);

    /// <summary>
    /// Returns true if the user is allowed to act on the given case under their scope.
    /// SystemAdmin and NationalManager always pass. All other roles are evaluated against
    /// the narrowest scope claim present: catchmentId > wmaId > provinceId (none → false).
    /// </summary>
    /// <remarks>
    /// Callers MAY pre-load <see cref="FileMaster.Property"/> (and its
    /// <see cref="Property.WaterManagementArea"/> nav for province-scoped users) to avoid
    /// extra DB round-trips, but this is not required — the implementation falls back to a
    /// DB lookup by <see cref="FileMaster.PropertyId"/> when the nav is null.
    /// </remarks>
    bool IsInScope(FileMaster fileMaster, ClaimsPrincipal user);
}
