using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace dwa_ver_val.Tests.Integration;

/// <summary>
/// Shared <see cref="WebApplicationFactory{TEntryPoint}"/> configuration for integration tests:
/// runs the app in Development environment and relaxes the application cookie's SecurePolicy
/// to <c>SameAsRequest</c> so the in-memory test client (which talks plain HTTP) can carry the
/// auth cookie between requests. Production cookie config (SecurePolicy=Always) is unchanged.
///
/// AUTH-02: credentials are no longer committed in appsettings.json. The integration test
/// fixture loads the connection string from the environment variable
/// <c>ConnectionStrings__Default</c> (set by CI or local .env) or from the main project's
/// user-secrets (loaded automatically in Development via the UserSecretsId in dwa_ver_val.csproj).
/// The fixture adds an in-process fallback so the test runner does not crash on machines
/// that have no SQL Server; those tests will fail with a clean connectivity error rather
/// than a missing-connection-string exception.
/// </summary>
public class IntegrationTestFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // AUTH-02: if neither user-secrets nor env var supplies the connection string,
        // inject the local-dev default so integration tests still run on the dev machine
        // without requiring a second secret store.  On CI the env var takes precedence.
        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            var envCs = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
            if (!string.IsNullOrEmpty(envCs))
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = envCs,
                });
            }
            else
            {
                // Load user-secrets for the main app assembly (UserSecretsId in dwa_ver_val.csproj).
                // This picks up the real connection string without committing credentials.
                config.AddUserSecrets<Program>(optional: true);
            }

            // Ensure test-only Identity:InitialDemoPassword is set so seeder doesn't crash.
            var envPass = Environment.GetEnvironmentVariable("Identity__InitialDemoPassword");
            if (!string.IsNullOrEmpty(envPass))
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Identity:InitialDemoPassword"] = envPass,
                });
            }
        });

        builder.ConfigureServices(services =>
        {
            services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, o =>
            {
                o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });
        });
    }
}

/// <summary>
/// Helpers reused by multiple integration test classes — antiforgery extraction, demo-user login.
/// Kept as a static class so test classes don't need to inherit a base type.
/// </summary>
public static class IntegrationTestHelpers
{
    public const string DemoPassword = "Demo@Pass2026";

    /// <summary>Pulls the antiforgery token out of a Razor-rendered HTML form.</summary>
    public static async Task<string> GetAntiForgeryToken(HttpClient client, string path)
    {
        var body = await client.GetStringAsync(path);
        var nameMarker = "name=\"__RequestVerificationToken\"";
        var nameIdx = body.IndexOf(nameMarker, StringComparison.Ordinal);
        if (nameIdx < 0)
            throw new InvalidOperationException($"Antiforgery token input not found on {path}");

        var valueMarker = "value=\"";
        var valueIdx = body.IndexOf(valueMarker, nameIdx, StringComparison.Ordinal);
        if (valueIdx < 0)
            throw new InvalidOperationException($"Antiforgery token value attribute not found on {path}");
        var start = valueIdx + valueMarker.Length;
        var end = body.IndexOf('"', start);
        return body[start..end];
    }

    /// <summary>Posts the demo-user login form. Returns the response so the caller can inspect status / Location.</summary>
    public static async Task<HttpResponseMessage> LoginAsDemoUser(
        HttpClient client, string email, string password = DemoPassword)
    {
        var token = await GetAntiForgeryToken(client, "/Account/Login");
        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", email),
            new KeyValuePair<string, string>("Password", password),
            new KeyValuePair<string, string>("RememberMe", "false"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        }));
    }

    /// <summary>Creates a redirect-disabled, cookie-handling client suitable for multi-step flows.</summary>
    public static HttpClient CreateAuthenticatedClient(IntegrationTestFixture fixture) =>
        fixture.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    /// <summary>
    /// Posts the portal registration form. Returns the response so the caller can inspect
    /// status/Location and (for Stage 2a) the TempData demo confirm URL via following the redirect.
    /// </summary>
    public static async Task<HttpResponseMessage> RegisterPublicUser(
        HttpClient client,
        string email,
        string password = "Validuserpassword12!",
        string firstName = "Test",
        string lastName = "User",
        string identityNumber = "8001015009087")
    {
        var token = await GetAntiForgeryToken(client, "/ExternalPortal/Account/Register");
        return await client.PostAsync("/ExternalPortal/Account/Register", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", email),
            new KeyValuePair<string, string>("Password", password),
            new KeyValuePair<string, string>("ConfirmPassword", password),
            new KeyValuePair<string, string>("FirstName", firstName),
            new KeyValuePair<string, string>("LastName", lastName),
            new KeyValuePair<string, string>("IdentityNumber", identityNumber),
            new KeyValuePair<string, string>("AcceptTerms", "true"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        }));
    }
}
