using dwa_ver_val.E2E.Infrastructure;
using Microsoft.Playwright;

namespace dwa_ver_val.E2E;

/// <summary>
/// Phase-2 browser E2E regression for the auth gate + role-based access control on the
/// Reports area. Exercises the real cookie-auth pipeline against the live Kestrel app
/// and isolated E2E DB:
///   - anonymous access to a national report is gated to /Account/Login;
///   - the ReadOnly demo user can read <c>CanRead</c> reports (AtLeastReadOnly);
///   - the ReadOnly demo user is denied national reports (AtLeastNationalManager);
///   - the NationalManager demo user can read national reports.
/// Assertions are deliberately structural (URLs + the "Access denied" heading) so they
/// hold regardless of whether the E2E DB contains any report rows.
/// </summary>
[Collection(E2ECollection.Name)]
public sealed class AuthGateAndRbacTests
{
    private readonly E2EAppFixture _fixture;

    public AuthGateAndRbacTests(E2EAppFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Anonymous_GetNationalReport_IsGatedTo_Login()
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            await page.GotoAsync("/Reports/UserActivity", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Unauthenticated request to a guarded report is redirected to the internal login page.
            Assert.Contains("/Account/Login", page.Url, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await E2EAppFixture.DisposePageAsync(page);
        }
    }

    [Fact]
    public async Task ReadOnly_CanRead_CanReadReport()
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.ReadOnly);

            await page.GotoAsync("/Reports/CatchmentProgress", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // The ReadOnly user (AtLeastReadOnly) is permitted: URL stays on the report action,
            // not bounced to login or access-denied.
            Assert.Contains("/Reports/CatchmentProgress", page.Url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/Account/Login", page.Url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/Account/AccessDenied", page.Url, StringComparison.OrdinalIgnoreCase);

            // And the "Access denied" page did not render.
            var denied = page.Locator("h1", new PageLocatorOptions { HasTextString = "Access denied" });
            Assert.Equal(0, await denied.CountAsync());
        }
        finally
        {
            await E2EAppFixture.DisposePageAsync(page);
        }
    }

    [Fact]
    public async Task ReadOnly_IsDenied_NationalReport()
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.ReadOnly);

            await page.GotoAsync("/Reports/UserActivity", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // The ReadOnly user lacks AtLeastNationalManager: the request is forbidden and the
            // cookie pipeline routes to the access-denied page (URL) which renders the heading.
            var deniedUrl = page.Url.Contains("/Account/AccessDenied", StringComparison.OrdinalIgnoreCase);
            var deniedHeading = page.Locator("h1", new PageLocatorOptions { HasTextString = "Access denied" });
            var deniedHeadingVisible = await deniedHeading.CountAsync() > 0 && await deniedHeading.IsVisibleAsync();

            Assert.True(
                deniedUrl || deniedHeadingVisible,
                $"Expected access-denied (URL or heading). Actual URL: {page.Url}");

            // Crucially, the national report itself did NOT render for ReadOnly.
            Assert.DoesNotContain("/Reports/UserActivity", page.Url, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await E2EAppFixture.DisposePageAsync(page);
        }
    }

    [Fact]
    public async Task NationalManager_CanRead_NationalReport()
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.National);

            await page.GotoAsync("/Reports/UserActivity", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // The NationalManager (AtLeastNationalManager) is permitted: URL stays on the report
            // action and the access-denied page did not render.
            Assert.Contains("/Reports/UserActivity", page.Url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/Account/Login", page.Url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/Account/AccessDenied", page.Url, StringComparison.OrdinalIgnoreCase);

            var denied = page.Locator("h1", new PageLocatorOptions { HasTextString = "Access denied" });
            Assert.Equal(0, await denied.CountAsync());
        }
        finally
        {
            await E2EAppFixture.DisposePageAsync(page);
        }
    }
}
