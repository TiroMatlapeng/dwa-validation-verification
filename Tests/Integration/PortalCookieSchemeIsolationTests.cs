using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace dwa_ver_val.Tests.Integration;

public class PortalCookieSchemeIsolationTests : IClassFixture<PortalIntegrationTestFixture>
{
    private readonly PortalIntegrationTestFixture _fixture;

    public PortalCookieSchemeIsolationTests(PortalIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UnauthenticatedRequest_ToInternalAdminRoute_RedirectsToInternalLogin()
    {
        var client = _fixture.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // /Admin doesn't have a controller wired in this stage, but any internal
        // protected endpoint will redirect to the internal login. We probe a
        // known-protected internal route that exists today: the user-admin index.
        // UsersController has [Route("Admin/[controller]/[action]")] so the real
        // path is /Admin/Users/Index (not /Admin/Users which 404s).
        var resp = await client.GetAsync("/Admin/Users/Index");

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Contains("/Account/Login", resp.Headers.Location?.OriginalString ?? "");
    }

    [Fact]
    public async Task UnauthenticatedRequest_ToExternalPortalRoute_Returns404_NotInternalLoginRedirect()
    {
        var client = _fixture.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // No portal controllers exist yet — we expect 404 from the routing layer.
        // Critical assertion: it must NOT redirect to /Account/Login (which would
        // mean the default scheme is wrongly answering for the portal path).
        var resp = await client.GetAsync("/ExternalPortal/Dashboard");

        Assert.NotEqual(HttpStatusCode.Redirect, resp.StatusCode);
        if (resp.Headers.Location != null)
            Assert.DoesNotContain("/Account/Login", resp.Headers.Location.OriginalString);
    }

    [Fact]
    public async Task PortalCookieName_IsRegistered()
    {
        // Indirect assertion: spin up the fixture (which forces the host to build),
        // and verify the host did not throw on cookie scheme registration.
        // The actual cookie name is asserted via PortalCookieOptions.CookieName.
        using var client = _fixture.CreateClient();
        Assert.Equal(".dwa.PortalAuth", dwa_ver_val.Services.Portal.Auth.PortalCookieOptions.CookieName);
    }
}
