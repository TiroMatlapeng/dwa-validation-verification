using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

public class DwsClaimsTransformation : IClaimsTransformation
{
    private const string Marker = "dws:augmented";

    private readonly ApplicationDBContext _db;

    public DwsClaimsTransformation(ApplicationDBContext db)
    {
        _db = db;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return principal;

        if (identity.HasClaim(c => c.Type == Marker)) return principal;

        var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return principal;

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.FirstName,
                u.LastName,
                u.EmployeeNumber,
                u.OrgUnitId,
                OrgUnit = u.OrgUnit == null ? null : new
                {
                    u.OrgUnit.ProvinceId,
                    u.OrgUnit.WmaId,
                    u.OrgUnit.CatchmentAreaId
                }
            })
            .FirstOrDefaultAsync();

        if (user is null) return principal;

        identity.AddClaim(new Claim("displayName", $"{user.FirstName} {user.LastName}"));
        identity.AddClaim(new Claim("employeeNumber", user.EmployeeNumber));
        identity.AddClaim(new Claim("orgUnitId", user.OrgUnitId?.ToString() ?? string.Empty));
        identity.AddClaim(new Claim("provinceId", user.OrgUnit?.ProvinceId?.ToString() ?? string.Empty));
        identity.AddClaim(new Claim("wmaId", user.OrgUnit?.WmaId?.ToString() ?? string.Empty));
        identity.AddClaim(new Claim("catchmentId", user.OrgUnit?.CatchmentAreaId?.ToString() ?? string.Empty));
        identity.AddClaim(new Claim(Marker, "1"));

        return principal;
    }
}
