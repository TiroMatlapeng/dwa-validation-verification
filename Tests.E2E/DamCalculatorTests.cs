using dwa_ver_val.E2E.Infrastructure;
using Microsoft.Playwright;

namespace dwa_ver_val.E2E;

/// <summary>
/// End-to-end proof of BUG-019 closure: the Appendix D dam capacity calculator works through the
/// real UI for both methods, with exact expected values from the Requirements Ed.3 Appendix D
/// formulas. Method 2 (Area): 2 ha × 3 m × 0.5 × 1000 = 3,000 m³. Method 1 (Wall Length):
/// slope = 200/10 = 20, depth = 50/20 = 2.5, capacity = 100 × 50 × 2.5 × 0.4 / 2 = 2,500 m³.
/// </summary>
[Collection(E2ECollection.Name)]
public sealed class DamCalculatorTests
{
    private readonly E2EAppFixture _fixture;
    public DamCalculatorTests(E2EAppFixture fixture) => _fixture = fixture;

    private static readonly PageGotoOptions Idle = new() { WaitUntil = WaitUntilState.NetworkIdle };

    [Fact]
    public async Task DamCapacity_CalculatedThroughUi_BothAppendixDMethods()
    {
        var suffix = $"Dam{DateTime.UtcNow:HHmmssfff}";
        var reg = $"E2E-DAM-{suffix}";
        var anchors = await VnVTestData.SeedScopedPropertyAsync(suffix);

        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.Validator("3"));

            // A FileMaster case must exist for the property — the Calculate action resolves
            // org scope through it.
            await page.GotoAsync("/FileMaster/Create", Idle);
            await page.FillAsync("input[name='RegistrationNumber']", reg);
            await page.FillAsync("input[name='SurveyorGeneralCode']", $"SG-{reg}");
            await page.FillAsync("input[name='PrimaryCatchment']", "X2");
            await page.FillAsync("input[name='QuaternaryCatchment']", "X21A");
            await page.FillAsync("input[name='FarmName']", "Dam Calc Farm");
            await page.FillAsync("input[name='FarmNumber']", "400");
            await page.FillAsync("input[name='RegistrationDivision']", "JT");
            await page.FillAsync("input[name='FarmPortion']", "0");
            await page.SelectOptionAsync("select[name='CatchmentAreaId']", anchors.CatchmentAreaId.ToString());
            await page.SelectOptionAsync("select[name='AssessmentTrack']", "S35_Verification");
            await page.SelectOptionAsync("select[name='PropertyId']", anchors.PropertyId.ToString());
            await page.SelectOptionAsync("select[name='OrgUnitId']", anchors.OrgUnitId.ToString());
            await page.ClickAsync("input.btn-primary[type='submit'], button[type='submit'].btn-primary");
            await page.WaitForURLAsync(u => u.Contains("/FileMaster/Details", StringComparison.OrdinalIgnoreCase));

            // ── Create the dam record with Method 2 (Area) inputs ──
            await page.GotoAsync($"/DamCalculation/Create?propertyId={anchors.PropertyId}", Idle);
            await page.FillAsync("input[name='DamNumber']", "D01");
            await page.SelectOptionAsync("select[name='RiverId']", new SelectOptionValue { Index = 1 });
            await page.FillAsync("input[name='CalculationDate']", "2026-06-11");
            await page.FillAsync("input[name='SateliteSurveyDate']", "2026-06-11");
            await page.FillAsync("input[name='DamCapacity']", "0");
            await page.SelectOptionAsync("select[name='CalculationMethod']", "Method2");
            await page.FillAsync("#method2-fields input[name='DamArea']", "2");
            await page.FillAsync("#method2-fields input[name='DamDepth']", "3");
            await page.SelectOptionAsync("select[name='ShapeFactor']", "0.50");
            await page.ClickAsync("form[action*='Create'] button[type='submit']");
            await page.WaitForURLAsync(u => u.Contains("/DamCalculation/Edit", StringComparison.OrdinalIgnoreCase));

            // ── Calculate: Method 2 → 2 × 3 × 0.5 × 1000 = 3,000 m³ ──
            await page.ClickAsync("form[action*='Calculate'] button[type='submit']");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var body = await page.InnerTextAsync("body");
            Assert.Contains("Dam capacity calculated: 3,000", body);
            Assert.Equal("3000.00", await page.InputValueAsync("input[name='DamCapacity']"));

            // ── Switch to Method 1 (Wall Length) on Edit, save, recalculate ──
            var editUrl = page.Url; // capture /DamCalculation/Edit/{id} — the save redirects to Index
            await page.SelectOptionAsync("select[name='CalculationMethod']", "Method1");
            await page.FillAsync("#method1-fields input[name='WallLength']", "100");
            await page.FillAsync("#method1-fields input[name='Fetch']", "50");
            await page.FillAsync("#method1-fields input[name='RiverDistance']", "200");
            await page.FillAsync("#method1-fields input[name='ContourDifference']", "10");
            await page.SelectOptionAsync("select[name='ShapeFactor']", "0.40");
            await page.ClickAsync("form[action*='Edit'] button[type='submit']");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // The Edit save redirects to the per-property Index; return to Edit to recalculate.
            await page.GotoAsync(editUrl, Idle);
            await page.ClickAsync("form[action*='Calculate'] button[type='submit']");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            body = await page.InnerTextAsync("body");
            Assert.Contains("Dam capacity calculated: 2,500", body);
            Assert.Equal("2500.00", await page.InputValueAsync("input[name='DamCapacity']"));
        }
        finally { await E2EAppFixture.DisposePageAsync(page); }
    }
}
