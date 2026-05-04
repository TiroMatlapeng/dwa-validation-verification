using Microsoft.AspNetCore.Authentication.Cookies;

namespace dwa_ver_val.Services.Portal.Auth;

/// <summary>
/// CookieAuthenticationEvents for the PublicPortalScheme.
/// Stage 2a: stub that defers entirely to base behaviour.
/// Stage 2b: override OnValidatePrincipal to re-check PublicUser.Status from
/// DB on every sliding refresh and reject suspended/deactivated users.
/// </summary>
public class PortalCookieEvents : CookieAuthenticationEvents
{
    // Stage 2a: no overrides. Class exists so Program.cs can register
    // options.EventsType = typeof(PortalCookieEvents) once and Stage 2b
    // adds the override here without touching the wiring.
}
