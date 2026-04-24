using System.Security.Claims;

public interface IScopedCaseQuery
{
    IQueryable<FileMaster> FilterFileMasters(IQueryable<FileMaster> source, ClaimsPrincipal user);
    IQueryable<Property> FilterProperties(IQueryable<Property> source, ClaimsPrincipal user);
    bool IsInScope(FileMaster fileMaster, ClaimsPrincipal user);
}
