using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace dwa_ver_val.E2E.Infrastructure;

/// <summary>
/// Boots the REAL application over a REAL Kestrel HTTP endpoint that a real
/// Playwright browser can connect to over TCP, against an ISOLATED E2E database.
///
/// Why not the default <see cref="WebApplicationFactory{TEntryPoint}"/> server?
/// The default host uses an in-memory <c>TestServer</c> with no TCP socket — an
/// out-of-process browser cannot reach it. We therefore override
/// <see cref="CreateHost"/> to ALSO build and start a parallel host bound to
/// Kestrel on <c>http://127.0.0.1:0</c> (a dynamically-allocated free port), then
/// read the actually-bound address from <see cref="IServerAddressesFeature"/> and
/// hand that base URL to Playwright. The TestServer host is still returned to the
/// factory so the base class lifecycle (and any future <c>CreateClient</c>) keeps working.
///
/// Config overrides (injected via in-memory configuration, NOT touching committed
/// appsettings or production behaviour):
///   - ConnectionStrings:Default  -> the isolated <c>dwa_val_ver_e2e</c> database.
///   - Identity:InitialDemoPassword -> so IdentitySeeder seeds the demo users.
///   - Environment "Development"   -> keeps the Program.cs Production-only POPIA
///     guard dormant and relaxes HTTPS redirect.
/// The app's own startup (Program.cs ~242-253) runs MigrateAsync + SeedDataService
/// + IdentitySeeder against the E2E DB the first time the host starts.
/// </summary>
public sealed class KestrelAppFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Local-dev SQL Server (Docker). These are local dev creds, not secrets.</summary>
    public const string E2EConnectionString =
        "Server=localhost,1433;Database=dwa_val_ver_e2e;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True";

    /// <summary>Config value that makes IdentitySeeder seed the *@dwa.demo users.</summary>
    public const string DemoPassword = "DwaDemo2026!";

    private IHost? _kestrelHost;

    /// <summary>The base URL of the live Kestrel listener, e.g. http://127.0.0.1:53123. Set after InitializeAsync.</summary>
    public string BaseUrl { get; private set; } = string.Empty;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = E2EConnectionString,
                ["Identity:InitialDemoPassword"] = DemoPassword,
                // Determinism: the app loads AzureAd user-secrets in Development, which
                // enables the Entra (Microsoft) OIDC scheme and adds a "Sign in with
                // Microsoft" submit button to the login page. That makes the suite depend
                // on whether a given machine has AzureAd secrets, and makes the generic
                // button[type=submit] selector challenge Entra (→ login.microsoftonline.com)
                // instead of performing local credential login. Blank these so the login
                // page exposes ONLY the local username/password form. (In-memory config is
                // added after the app's sources, so it wins over user-secrets.)
                ["AzureAd:TenantId"] = "",
                ["AzureAd:ClientId"] = "",
                ["AzureAd:ClientSecret"] = "",
            });
        });

        builder.ConfigureServices(services =>
        {
            // The Kestrel listener speaks plain HTTP; relax the auth cookie's
            // SecurePolicy so the cookie is returned over HTTP (mirrors the AKS
            // plain-HTTP posture). Production config (Always) is untouched.
            services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, o =>
            {
                o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });
        });
    }

    /// <summary>
    /// Build the host the factory expects (TestServer), AND a parallel host bound to
    /// real Kestrel on a free port. Start the Kestrel host and capture its address.
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Canonical "WebApplicationFactory + real Kestrel" ordering
        // (David Fowler gist / dotnet/aspnetcore#33846).

        // 1. Build (do NOT start) the TestServer host the factory expects.
        var testHost = builder.Build();

        // 2. Reconfigure the SAME builder for real Kestrel on a free port; applied
        //    AFTER the factory's UseTestServer, so Kestrel now wins as the IServer.
        builder.ConfigureWebHost(webHostBuilder =>
        {
            webHostBuilder.UseKestrel();
            webHostBuilder.UseUrls("http://127.0.0.1:0");
        });

        // 3. Build + start the Kestrel host BEFORE the test host (#33846), so the
        //    dynamically-bound address is available.
        _kestrelHost = builder.Build();
        _kestrelHost.Start();

        var addresses = _kestrelHost.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("Kestrel host exposed no IServerAddressesFeature.");

        // With UseUrls("...:0") the resolved bound address is the last entry.
        BaseUrl = addresses.Addresses.Last();

        // 4. Start the TestServer host and return it (the base class requires the
        //    returned server to be the TestServer instance).
        testHost.Start();
        return testHost;
    }

    public async Task InitializeAsync()
    {
        // Force host creation (which boots Kestrel, applies migrations, seeds the
        // E2E DB) by resolving the factory's server eagerly.
        _ = Server;

        // Ensure the Chromium browser binary is present. Idempotent / fast on repeat.
        BrowserInstaller.EnsureChromium();

        await Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_kestrelHost is not null)
        {
            await _kestrelHost.StopAsync();
            _kestrelHost.Dispose();
        }

        await base.DisposeAsync();
    }
}
