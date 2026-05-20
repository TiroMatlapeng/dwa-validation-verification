using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace dwa_ver_val.Services.Portal.Auth;

/// <summary>
/// CookieAuthenticationEvents for the PublicPortalScheme.
/// ValidatePrincipal fires on every authenticated request carrying the portal cookie.
/// It re-checks PublicUser.Status from the DB so suspended or deactivated accounts
/// are kicked out immediately — without waiting for the 8-hour cookie to expire.
/// </summary>
public class PortalCookieEvents : CookieAuthenticationEvents
{
    private readonly ApplicationDBContext _db;

    public PortalCookieEvents(ApplicationDBContext db)
    {
        _db = db;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            context.RejectPrincipal();
            return;
        }

        var user = await _db.PublicUsers.FindAsync(
            new object[] { userId }, context.HttpContext.RequestAborted);

        if (user is null || user.Status != "Active")
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(PortalCookieOptions.SchemeName);
            return;
        }

        await base.ValidatePrincipal(context);
    }
}
