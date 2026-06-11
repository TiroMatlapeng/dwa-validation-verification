using dwa_ver_val.E2E.Infrastructure;
using Microsoft.Playwright;

namespace dwa_ver_val.E2E;

/// <summary>
/// Phase-2 browser smoke regression for the internal home dashboard (D3 charts) and
/// the three readable reports plus their export links. Runs a real Chromium browser
/// against the shared Kestrel-hosted app + isolated E2E DB. Logs in as the seeded
/// ReadOnly demo user — sufficient for the dashboard and the three CanRead reports.
///
/// All assertions are STRUCTURAL (chart containers, table element, export anchors).
/// The E2E DB may have no report rows, so we never assert on data rows.
/// </summary>
[Collection(E2ECollection.Name)]
public sealed class DashboardAndReportsSmokeTests
{
    private readonly E2EAppFixture _fixture;

    public DashboardAndReportsSmokeTests(E2EAppFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Dashboard_Renders_D3_ChartContainers_And_ChartsScript()
    {
        var page = await _fixture.NewPageAsync();

        // Capture only genuine JS errors so a broken chart bundle is caught, without
        // making the test flaky on benign warnings/info messages.
        var consoleErrors = new List<string>();
        page.Console += (_, msg) =>
        {
            if (string.Equals(msg.Type, "error", StringComparison.OrdinalIgnoreCase))
            {
                consoleErrors.Add(msg.Text);
            }
        };

        try
        {
            await _fixture.LoginAsync(page, DemoUsers.ReadOnly);

            await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Both D3 chart mount points are present and visible.
            var phaseChart = page.Locator("#phaseChart");
            var statusChart = page.Locator("#statusChart");
            Assert.Equal(1, await phaseChart.CountAsync());
            Assert.Equal(1, await statusChart.CountAsync());
            Assert.True(await phaseChart.IsVisibleAsync());
            Assert.True(await statusChart.IsVisibleAsync());

            // The reporting chart bundle is wired into the page. asp-append-version adds a
            // ?v=... query string, so match on a substring rather than an exact src.
            var chartsScript = page.Locator("script[src*=\"charts.js\"]");
            Assert.Equal(1, await chartsScript.CountAsync());

            // No genuine JS errors fired while the dashboard loaded and the charts drew.
            Assert.True(
                consoleErrors.Count == 0,
                "Dashboard emitted console errors: " + string.Join(" | ", consoleErrors));
        }
        finally
        {
            await E2EAppFixture.DisposePageAsync(page);
        }
    }

    [Theory]
    [InlineData("/Reports/CatchmentProgress")]
    [InlineData("/Reports/LetterTracking")]
    [InlineData("/Reports/ValidationSummary")]
    public async Task ReadableReport_Renders_Table_And_ExportLinks(string reportPath)
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.ReadOnly);

            await page.GotoAsync(reportPath, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // The shared report view renders a <table class="table"> regardless of data.
            var table = page.Locator("table.table");
            Assert.Equal(1, await table.CountAsync());
            Assert.True(await table.IsVisibleAsync());

            // The three export anchors (asp-route-format) render ?format=csv|xlsx|pdf.
            var csv = page.Locator("a[href*=\"format=csv\"]");
            var xlsx = page.Locator("a[href*=\"format=xlsx\"]");
            var pdf = page.Locator("a[href*=\"format=pdf\"]");

            Assert.Equal(1, await csv.CountAsync());
            Assert.Equal(1, await xlsx.CountAsync());
            Assert.Equal(1, await pdf.CountAsync());

            Assert.True(await csv.IsVisibleAsync());
            Assert.True(await xlsx.IsVisibleAsync());
            Assert.True(await pdf.IsVisibleAsync());
        }
        finally
        {
            await E2EAppFixture.DisposePageAsync(page);
        }
    }
}
