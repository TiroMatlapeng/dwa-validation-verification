using dwa_ver_val.E2E.Infrastructure;
using Microsoft.Playwright;

namespace dwa_ver_val.E2E;

/// <summary>
/// Admin console smoke: SystemAdmin reaches all three management surfaces and can run a full
/// create→list→delete round trip on reference data; non-admin roles are denied at the door.
/// </summary>
[Collection(E2ECollection.Name)]
public sealed class AdminConsoleTests
{
    private readonly E2EAppFixture _fixture;
    public AdminConsoleTests(E2EAppFixture fixture) => _fixture = fixture;

    private static readonly PageGotoOptions Idle = new() { WaitUntil = WaitUntilState.NetworkIdle };

    [Theory]
    [InlineData("/Admin/OrganisationalUnits/Index")]
    [InlineData("/Admin/Gwcas/Index")]
    [InlineData("/Admin/ReferenceData/Index")]
    public async Task Admin_CanReachManagementSurface(string url)
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.Admin);
            await page.GotoAsync(url, Idle);
            Assert.Contains(url, page.Url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/Account/AccessDenied", page.Url, StringComparison.OrdinalIgnoreCase);
        }
        finally { await E2EAppFixture.DisposePageAsync(page); }
    }

    [Theory]
    [InlineData("/Admin/OrganisationalUnits/Index")]
    [InlineData("/Admin/Gwcas/Index")]
    [InlineData("/Admin/ReferenceData/Index")]
    public async Task NonAdmin_IsDenied(string url)
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.ReadOnly);
            await page.GotoAsync(url, Idle);
            Assert.Contains("/Account/AccessDenied", page.Url, StringComparison.OrdinalIgnoreCase);
        }
        finally { await E2EAppFixture.DisposePageAsync(page); }
    }

    [Fact]
    public async Task Admin_CanCreateAndDeleteRiver_ThroughUi()
    {
        var riverName = $"E2E Test River {DateTime.UtcNow:HHmmssfff}";
        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.Admin);

            await page.GotoAsync("/Admin/ReferenceData/CreateRiver", Idle);
            await page.FillAsync("input[name='RiverName']", riverName);
            await page.ClickAsync("form.dws-form button[type='submit']");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.True(page.Url.Contains("/Admin/ReferenceData/Index", StringComparison.OrdinalIgnoreCase),
                $"Expected redirect to ReferenceData Index after create; landed on {page.Url}. Body: {(await page.InnerTextAsync("body"))[..Math.Min(400, (await page.InnerTextAsync("body")).Length)]}");
            Assert.Contains(riverName, await page.InnerTextAsync("body"));

            // Delete it again (unreferenced → allowed). The delete form sits in the river's row
            // behind a JS confirm() — Playwright dismisses dialogs by default, so accept it.
            page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
            var row = page.Locator("tr", new PageLocatorOptions { HasTextString = riverName });
            await row.Locator("form button[type='submit']").ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.DoesNotContain(riverName, await page.InnerTextAsync("body"));
        }
        finally { await E2EAppFixture.DisposePageAsync(page); }
    }
}
