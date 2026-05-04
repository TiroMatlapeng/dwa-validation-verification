using Microsoft.AspNetCore.Authorization;

namespace dwa_ver_val.Services.Portal.Auth;

public static class PortalPolicies
{
    public const string PortalAuthenticated = "PortalAuthenticated";
    public const string PortalRegistrationComplete = "PortalRegistrationComplete";
    public const string PortalMfaPending = "PortalMfaPending";

    public static void Configure(AuthorizationOptions options)
    {
        // Full-access policy — Stage 2 will enforce all of email-confirmed +
        // MFA enrolled + status-active claims; for Stage 1 it requires
        // authentication only, since claims are not yet stamped.
        options.AddPolicy(PortalAuthenticated, p => p
            .AddAuthenticationSchemes(PortalCookieOptions.SchemeName)
            .RequireAuthenticatedUser());

        options.AddPolicy(PortalRegistrationComplete, p => p
            .AddAuthenticationSchemes(PortalCookieOptions.SchemeName)
            .RequireAuthenticatedUser());

        options.AddPolicy(PortalMfaPending, p => p
            .AddAuthenticationSchemes(PortalCookieOptions.SchemeName)
            .RequireAuthenticatedUser());
    }
}
