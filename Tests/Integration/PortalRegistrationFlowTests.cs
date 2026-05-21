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
    public async Task Register_Confirm_RedirectsToMfaSelectMethod()
    {
        // Replaces the old Register_Confirm_Login_Dashboard_HappyPath test.
        // Post-MFA: confirm email issues a partial session and redirects to MFA enrolment,
        // not directly to dashboard.
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

        // 4. GET confirm — now redirects to MFA SelectMethod (partial session issued)
        var confirmResp = await client.GetAsync(confirmUrl);
        Assert.Equal(HttpStatusCode.Redirect, confirmResp.StatusCode);
        Assert.Contains("/ExternalPortal/Mfa/SelectMethod", confirmResp.Headers.Location?.OriginalString ?? "");

        // 5. The PublicUser row is now Active + EmailConfirmed.
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
            var user = await db.PublicUsers.AsNoTracking().FirstAsync(u => u.EmailAddress == email);
            Assert.True(user.EmailConfirmed);
            Assert.Equal("Active", user.Status);
        }
    }

    [Fact]
    public async Task Login_NoMfaEnrolled_RedirectsToMfaSelectMethod()
    {
        // A user who has confirmed email but not yet enrolled MFA lands on SelectMethod.
        var client = _fixture.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var email = $"login-mfa-{Guid.NewGuid():N}@example.test";

        // Register and confirm
        var regResp = await IntegrationTestHelpers.RegisterPublicUser(client, email);
        var confirmPage = await client.GetAsync(regResp.Headers.Location!);
        var confirmBody = await confirmPage.Content.ReadAsStringAsync();
        var marker = "/ExternalPortal/Account/ConfirmEmail?token=";
        var idx = confirmBody.IndexOf(marker, StringComparison.Ordinal);
        var endIdx = confirmBody.IndexOf('"', idx);
        var confirmUrl = confirmBody[idx..endIdx];
        await client.GetAsync(confirmUrl);   // consume partial session from confirm redirect

        // Log in — MFA not enrolled, so redirected to SelectMethod (not Dashboard)
        var loginToken = await IntegrationTestHelpers.GetAntiForgeryToken(client, "/ExternalPortal/Account/Login");
        var loginResp = await client.PostAsync("/ExternalPortal/Account/Login", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", email),
            new KeyValuePair<string, string>("Password", "Validuserpassword12!"),
            new KeyValuePair<string, string>("__RequestVerificationToken", loginToken)
        }));
        Assert.Equal(HttpStatusCode.Redirect, loginResp.StatusCode);
        Assert.Contains("/ExternalPortal/Mfa/SelectMethod", loginResp.Headers.Location?.OriginalString ?? "");
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

    [Fact]
    public async Task SuspendedUser_AfterLogin_IsRejectedOnNextRequest()
    {
        // Replaces SuspendedUser_ExistingSession_IsRejectedOnNextRequest.
        // Post-MFA: login redirects to Mfa/SelectMethod. We follow to SelectMethod and then
        // attempt to access the dashboard directly. A suspended-user check still happens on
        // each cookie validation via PortalCookieEvents, so the suspended user must be rejected.
        var client = _fixture.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var email = $"suspend-{Guid.NewGuid():N}@example.test";

        // 1. Register
        var regResp = await IntegrationTestHelpers.RegisterPublicUser(client, email);
        Assert.Equal(HttpStatusCode.Redirect, regResp.StatusCode);

        // 2. Follow redirect to RegisterConfirmation and extract the confirm URL
        var confirmPage = await client.GetAsync(regResp.Headers.Location!);
        var confirmBody = await confirmPage.Content.ReadAsStringAsync();
        var marker = "/ExternalPortal/Account/ConfirmEmail?token=";
        var idx = confirmBody.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(idx >= 0, "Confirm URL not found in RegisterConfirmation page.");
        var endIdx = confirmBody.IndexOf('"', idx);
        var confirmUrl = confirmBody[idx..endIdx];

        // 3. Confirm email — partial session is issued, redirects to MFA SelectMethod
        var confirmResp = await client.GetAsync(confirmUrl);
        Assert.Equal(HttpStatusCode.Redirect, confirmResp.StatusCode);

        // 4. Log in — no MFA enrolled, so partial session issued, redirects to SelectMethod
        var loginToken = await IntegrationTestHelpers.GetAntiForgeryToken(client, "/ExternalPortal/Account/Login");
        var loginResp = await client.PostAsync("/ExternalPortal/Account/Login", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", email),
            new KeyValuePair<string, string>("Password", "Validuserpassword12!"),
            new KeyValuePair<string, string>("__RequestVerificationToken", loginToken)
        }));
        Assert.Equal(HttpStatusCode.Redirect, loginResp.StatusCode);
        // Partial session: redirected to MFA, not dashboard
        Assert.Contains("/ExternalPortal/Mfa/SelectMethod", loginResp.Headers.Location?.OriginalString ?? "");

        // 5. Suspend the user via direct DB access
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
            var user = await db.PublicUsers.FirstAsync(u => u.EmailAddress == email);
            user.Status = "Suspended";
            await db.SaveChangesAsync();
        }

        // 6. Access a protected portal page — session must be rejected because user is suspended
        var afterSuspend = await client.GetAsync("/ExternalPortal/Dashboard/Index");
        Assert.Equal(HttpStatusCode.Redirect, afterSuspend.StatusCode);
        Assert.Contains("/ExternalPortal/Account/Login",
            afterSuspend.Headers.Location?.OriginalString ?? "");
    }
}
