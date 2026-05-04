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
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.LoginPath = "/ExternalPortal/Account/Login";
        options.LogoutPath = "/ExternalPortal/Account/Logout";
        options.AccessDeniedPath = "/ExternalPortal/Account/AccessDenied";
    }
}
