using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace dwa_ver_val.Services.Portal.Auth;

/// <summary>
/// IControllerModelConvention that auto-applies
/// [Authorize(scheme=PublicPortalScheme, policy=PortalAuthenticated)] to every
/// controller inside the ExternalPortal area. Per-action [AllowAnonymous] still
/// wins (added by the action attribute, evaluated after this convention).
/// Saves us from sprinkling [Authorize(...)] on every portal controller and
/// removes the risk of forgetting it on a new one.
/// </summary>
public class PortalAuthorizationConvention : IControllerModelConvention
{
    private const string ExternalPortalAreaName = "ExternalPortal";

    public void Apply(ControllerModel controller)
    {
        var areaAttribute = controller.Attributes
            .OfType<Microsoft.AspNetCore.Mvc.AreaAttribute>()
            .FirstOrDefault();

        if (areaAttribute?.RouteValue != ExternalPortalAreaName)
            return;

        // Controllers that declare PortalMfaPending manage their own partial-session auth.
        // Do NOT add the full EmailConfirmed filter to them — it would block partial sessions.
        var hasMfaPendingPolicy = controller.Attributes
            .OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Any(a => a.Policy == PortalPolicies.PortalMfaPending);

        if (hasMfaPendingPolicy)
            return;

        var policy = new AuthorizationPolicyBuilder(PortalCookieOptions.SchemeName)
            .RequireAuthenticatedUser()
            .RequireClaim(PortalPolicies.EmailConfirmedClaim, "true")
            .Build();

        controller.Filters.Add(new AuthorizeFilter(policy));
    }
}
