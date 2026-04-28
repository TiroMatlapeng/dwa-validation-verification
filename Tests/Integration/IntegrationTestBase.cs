using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace dwa_ver_val.Tests.Integration;

/// <summary>
/// Shared <see cref="WebApplicationFactory{TEntryPoint}"/> configuration for integration tests:
/// runs the app in Development environment and relaxes the application cookie's SecurePolicy
/// to <c>SameAsRequest</c> so the in-memory test client (which talks plain HTTP) can carry the
/// auth cookie between requests. Production cookie config (SecurePolicy=Always) is unchanged.
/// </summary>
public class IntegrationTestFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
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
}
