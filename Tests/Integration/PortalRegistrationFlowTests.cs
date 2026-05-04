using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace dwa_ver_val.Tests.Integration;

public class PortalRegistrationFlowTests : IClassFixture<PortalIntegrationTestFixture>
{
    private readonly PortalIntegrationTestFixture _fixture;

    public PortalRegistrationFlowTests(PortalIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Register_Confirm_Login_Dashboard_HappyPath()
    {
        var client = _fixture.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var email = $"flow-{Guid.NewGuid():N}@example.test";

        // 1. POST register
        var registerResp = await IntegrationTestHelpers.RegisterPublicUser(client, email);
        Assert.Equal(HttpStatusCode.Redirect, registerResp.StatusCode);
        Assert.Contains("/ExternalPortal/Account/RegisterConfirmation", registerResp.Headers.Location?.OriginalString ?? "");

        // 2. Follow redirect to RegisterConfirmation; the TempData carries the demo confirm URL
        var confirmationPage = await client.GetAsync(registerResp.Headers.Location!);
        Assert.Equal(HttpStatusCode.OK, confirmationPage.StatusCode);
        var confirmationBody = await confirmationPage.Content.ReadAsStringAsync();

        // 3. Pull the confirm URL out of the TempData-rendered demo helper.
        var marker = "/ExternalPortal/Account/ConfirmEmail?token=";
        var idx = confirmationBody.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(idx >= 0, "Demo confirm URL not found in RegisterConfirmation page.");
        var endIdx = confirmationBody.IndexOf('"', idx);
        var confirmUrl = confirmationBody.Substring(idx, endIdx - idx);

        // 4. GET confirm
        var confirmResp = await client.GetAsync(confirmUrl);
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);
        var confirmBody = await confirmResp.Content.ReadAsStringAsync();
        Assert.Contains("Email confirmed", confirmBody);

        // 5. The PublicUser row is now Active + EmailConfirmed.
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
            var user = await db.PublicUsers.AsNoTracking().FirstAsync(u => u.EmailAddress == email);
            Assert.True(user.EmailConfirmed);
            Assert.Equal("Active", user.Status);
        }

        // 6. POST login
        var loginToken = await IntegrationTestHelpers.GetAntiForgeryToken(client, "/ExternalPortal/Account/Login");
        var loginResp = await client.PostAsync("/ExternalPortal/Account/Login", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", email),
            new KeyValuePair<string, string>("Password", "Validuserpassword12!"),
            new KeyValuePair<string, string>("__RequestVerificationToken", loginToken)
        }));
        Assert.Equal(HttpStatusCode.Redirect, loginResp.StatusCode);
        Assert.Contains("/ExternalPortal/Dashboard/Index", loginResp.Headers.Location?.OriginalString ?? "");

        // 7. GET dashboard — protected by PortalAuthorizationConvention; should succeed thanks to the cookie.
        var dashResp = await client.GetAsync(loginResp.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, dashResp.StatusCode);
        var dashBody = await dashResp.Content.ReadAsStringAsync();
        Assert.Contains(email, dashBody);
    }

    [Fact]
    public async Task Dashboard_Unauthenticated_RedirectsToLogin()
    {
        var client = _fixture.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var resp = await client.GetAsync("/ExternalPortal/Dashboard/Index");
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Contains("/ExternalPortal/Account/Login", resp.Headers.Location?.OriginalString ?? "");
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsViewWithGenericError()
    {
        var client = _fixture.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var email = $"wrong-{Guid.NewGuid():N}@example.test";
        await IntegrationTestHelpers.RegisterPublicUser(client, email);

        // For this test we don't need to confirm — wrong password should fail before the email-confirmed check.
        var loginToken = await IntegrationTestHelpers.GetAntiForgeryToken(client, "/ExternalPortal/Account/Login");
        var loginResp = await client.PostAsync("/ExternalPortal/Account/Login", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", email),
            new KeyValuePair<string, string>("Password", "wrongpasswordxx"),
            new KeyValuePair<string, string>("__RequestVerificationToken", loginToken)
        }));
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var body = await loginResp.Content.ReadAsStringAsync();
        Assert.Contains("Login failed", body);
    }
}
