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

    private static Guid? GetScopeWma(ClaimsPrincipal user)
    {
        var wmaClaim = user.FindFirst("wmaId")?.Value;
        return Guid.TryParse(wmaClaim, out var wma) ? wma : null;
    }

    public IQueryable<FileMaster> FilterFileMasters(IQueryable<FileMaster> source, ClaimsPrincipal user)
    {
        if (BypassesScope(user)) return source;
        var wmaId = GetScopeWma(user);
        if (wmaId is null) return source.Where(_ => false);
        return source.Where(fm => fm.Property!.WmaId == wmaId);
    }

    public IQueryable<Property> FilterProperties(IQueryable<Property> source, ClaimsPrincipal user)
    {
        if (BypassesScope(user)) return source;
        var wmaId = GetScopeWma(user);
        if (wmaId is null) return source.Where(_ => false);
        return source.Where(p => p.WmaId == wmaId);
    }

    public bool IsInScope(FileMaster fileMaster, ClaimsPrincipal user)
    {
        if (BypassesScope(user)) return true;
        var wmaId = GetScopeWma(user);
        if (wmaId is null) return false;

        var propertyWmaId = fileMaster.Property?.WmaId
            ?? _db.Properties.AsNoTracking()
                .Where(p => p.PropertyId == fileMaster.PropertyId)
                .Select(p => p.WmaId)
                .FirstOrDefault();

        return propertyWmaId == wmaId;
    }
}
