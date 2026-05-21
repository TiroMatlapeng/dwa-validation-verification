using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace dwa_ver_val.Services.Portal.Auth;

public static class PortalCookieOptions
{
    public const string SchemeName = "PublicPortalScheme";
    public const string CookieName = ".dwa.PortalAuth";
    public const string CookiePath = "/ExternalPortal";

    public static void Configure(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = CookieName;
        options.Cookie.Path = CookiePath;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // BUG-023: SameAsRequest (not Always). The AKS pod terminates TLS at the ingress
        // and speaks plain HTTP to the app, so Always would mark the portal auth cookie
        // Secure and the browser would refuse to return it over HTTP — every post-login /
        // post-MFA request would then appear unauthenticated and bounce back to the portal
        // login (the observed "404 on all post-login routes"). With HTTPS in front the cookie
        // is still marked Secure. This matches the internal Identity.Application cookie policy.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.LoginPath = "/ExternalPortal/Account/Login";
        options.LogoutPath = "/ExternalPortal/Account/Logout";
        options.AccessDeniedPath = "/ExternalPortal/Account/AccessDenied";
    }
}
