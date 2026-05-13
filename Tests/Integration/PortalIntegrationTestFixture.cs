using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace dwa_ver_val.Tests.Integration;

/// <summary>
/// Same as IntegrationTestFixture but ALSO relaxes the SecurePolicy of the
/// PublicPortalScheme cookie so the in-memory test client (HTTP) can carry it.
/// </summary>
public class PortalIntegrationTestFixture : IntegrationTestFixture
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.Configure<CookieAuthenticationOptions>(PortalCookieOptions.SchemeName, o =>
            {
                o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });
        });
    }
}
