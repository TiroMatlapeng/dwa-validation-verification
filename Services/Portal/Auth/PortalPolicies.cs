using Microsoft.AspNetCore.Authorization;

namespace dwa_ver_val.Services.Portal.Auth;

public static class PortalPolicies
{
    public const string PortalAuthenticated = "PortalAuthenticated";
    public const string PortalRegistrationComplete = "PortalRegistrationComplete";
    public const string PortalMfaPending = "PortalMfaPending";

    // Claim names stamped at sign-in by PublicUserSignInService.
    public const string EmailConfirmedClaim = "EmailConfirmed";
    public const string MfaEnrolledClaim = "MfaEnrolled";
    public const string StatusClaim = "Status";

    public static void Configure(AuthorizationOptions options)
    {
        // Stage 2a: PortalAuthenticated requires the cookie + EmailConfirmed=true + Status=Active.
        // (Stage 2b will add MfaEnrolled=true.)
        options.AddPolicy(PortalAuthenticated, p => p
            .AddAuthenticationSchemes(PortalCookieOptions.SchemeName)
            .RequireAuthenticatedUser()
            .RequireClaim(EmailConfirmedClaim, "true")
            .RequireClaim(StatusClaim, "Active"));

        // Used during MFA enrolment in Stage 2b — for 2a it's reachable as soon
        // as the email is confirmed, so the holding-page-on-dashboard works.
        options.AddPolicy(PortalRegistrationComplete, p => p
            .AddAuthenticationSchemes(PortalCookieOptions.SchemeName)
            .RequireAuthenticatedUser()
            .RequireClaim(EmailConfirmedClaim, "true")
            .RequireClaim(StatusClaim, "Active"));

        // Stage 2b only: short-lived cookie carrying MfaPending=true between
        // password verification and TOTP entry. Defined now so AccountController
        // can reference the constant without a compile error if Stage 2b is
        // partially landed.
        options.AddPolicy(PortalMfaPending, p => p
            .AddAuthenticationSchemes(PortalCookieOptions.SchemeName)
            .RequireAuthenticatedUser()
            .RequireClaim("MfaPending", "true"));
    }
}
