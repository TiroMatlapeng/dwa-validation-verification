using System.Net;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace dwa_ver_val.Tests.Integration;

public class IdentityFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IdentityFlowTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.ConfigureServices(services =>
            {
                // Allow auth cookies over plain HTTP so the test client (which doesn't do TLS)
                // can carry the Identity cookie between requests. Do NOT relax this in prod.
                services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, o =>
                {
                    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                });
            });
        });
    }

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/AccessDenied")]
    public async Task PublicPage_IsReachableAnonymously(string path)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedPage_RedirectsAnonymousToLogin()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync("/FileMaster");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task AdminUsersPage_NonAdmin_RedirectsToAccessDenied()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var token = await GetAntiForgeryToken(client, "/Account/Login");
        var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", "readonly@dwa.demo"),
            new KeyValuePair<string, string>("Password", "Demo@Pass2026"),
            new KeyValuePair<string, string>("RememberMe", "false"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync("/Admin/Users/Index");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.OriginalString);
    }

    private static async Task<string> GetAntiForgeryToken(HttpClient client, string path)
    {
        var body = await client.GetStringAsync(path);
        // Razor emits: <input name="__RequestVerificationToken" type="hidden" value="...">
        // Search by name= first, then back out to the value= on the same tag.
        var nameMarker = "name=\"__RequestVerificationToken\"";
        var nameIdx = body.IndexOf(nameMarker);
        if (nameIdx < 0)
            throw new InvalidOperationException("Antiforgery token input not found on " + path);

        var valueMarker = "value=\"";
        var valueIdx = body.IndexOf(valueMarker, nameIdx);
        if (valueIdx < 0)
            throw new InvalidOperationException("Antiforgery token value attribute not found on " + path);
        var start = valueIdx + valueMarker.Length;
        var end = body.IndexOf('"', start);
        return body[start..end];
    }
}
