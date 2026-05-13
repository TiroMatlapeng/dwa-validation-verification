using System.Security.Claims;

public interface IScopedCaseQuery
{
    IQueryable<FileMaster> FilterFileMasters(IQueryable<FileMaster> source, ClaimsPrincipal user);

    IQueryable<Property> FilterProperties(IQueryable<Property> source, ClaimsPrincipal user);

    /// <summary>
    /// Returns true if the user is allowed to act on the given case under their scope.
    /// SystemAdmin and NationalManager always pass. Other roles must hold a <c>wmaId</c>
    /// claim matching the case's property WMA.
    /// </summary>
    /// <remarks>
    /// Callers MAY pre-load <see cref="FileMaster.Property"/> to avoid an extra DB round-trip,
    /// but this is not required — the implementation falls back to a DB lookup by
    /// <see cref="FileMaster.PropertyId"/> when the nav is null.
    /// </remarks>
    bool IsInScope(FileMaster fileMaster, ClaimsPrincipal user);
}
