using dwa_ver_val.E2E.Infrastructure;
using Microsoft.Playwright;

namespace dwa_ver_val.E2E;

/// <summary>
/// PROVING TEST (Phase 1): a real Chromium browser performs an anonymous GET of the
/// app root against the real Kestrel-hosted app + isolated E2E database. The app's
/// cookie-auth pipeline must redirect the unauthenticated request to /Account/Login,
/// and the internal login page must render. This proves the whole harness end to end:
/// real socket, real browser, migrations + seeding applied to dwa_val_ver_e2e.
/// </summary>
[Collection(E2ECollection.Name)]
public sealed class AnonymousRootRedirectsToLoginTests
{
    private readonly E2EAppFixture _fixture;

    public AnonymousRootRedirectsToLoginTests(E2EAppFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Anonymous_GetRoot_RedirectsTo_InternalLoginPage()
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // 1. The unauthenticated request was redirected to the internal login page.
            Assert.Contains("/Account/Login", page.Url, StringComparison.OrdinalIgnoreCase);

            // 2. The login page rendered: known heading is visible.
            var heading = page.Locator("h1", new PageLocatorOptions { HasTextString = "Sign in to DWA V&V" });
            Assert.Equal(1, await heading.CountAsync());
            Assert.True(await heading.IsVisibleAsync());

            // 3. The email + password fields are present (the cookie-login form).
            Assert.Equal(1, await page.Locator("input[name='Email']").CountAsync());
            Assert.Equal(1, await page.Locator("input[name='Password']").CountAsync());
        }
        finally
        {
            await E2EAppFixture.DisposePageAsync(page);
        }
    }
}
