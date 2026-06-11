using dwa_ver_val.E2E.Infrastructure;
using Microsoft.Playwright;

namespace dwa_ver_val.E2E;

/// <summary>
/// Phase-2 browser E2E for the sticky report filter form (Slice 4).
/// Exercises a real Kestrel + DB session:
///   - The filter form is present on the report page.
///   - Filling DateFrom + selecting ValidationStatus and clicking Apply carries those
///     values into the page URL as querystring parameters.
///   - The export link hrefs also carry the active filter parameters.
///   - The form inputs are pre-populated (sticky) after the filtered page loads.
/// </summary>
[Collection(E2ECollection.Name)]
public sealed class ReportFilterTests
{
    private readonly E2EAppFixture _fixture;

    public ReportFilterTests(E2EAppFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Drives the REAL user interaction: open the report, fill DateFrom + select a
    /// ValidationStatus, and click the actual "Apply" button. Asserts the GET form submit
    /// carries the values into the URL, the export link hrefs pick them up, and the form
    /// is sticky after the filtered page loads.
    ///
    /// Note: the Apply button is targeted by its stable id <c>#report-apply</c>, NOT the
    /// generic <c>button[type='submit']</c> — the shared layout renders a "Sign out" submit
    /// button too, so a generic selector would click logout and bounce to /Account/Login.
    /// </summary>
    [Fact]
    public async Task FilterForm_ApplyFilter_CarriesParamsInUrlAndExportLinks_AndIsSticky()
    {
        const string dateFrom         = "2026-01-01";
        const string validationStatus = "In Process";

        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.ReadOnly);

            // Open the UNFILTERED report, then fill + submit the real form.
            await page.GotoAsync("/Reports/CatchmentProgress",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            Assert.DoesNotContain("/Account/Login", page.Url, StringComparison.OrdinalIgnoreCase);

            await page.FillAsync("input[name='DateFrom']", dateFrom);
            await page.SelectOptionAsync("select[name='ValidationStatus']", validationStatus);
            // Click the ACTUAL Apply button (a GET form submit → navigation) and wait for
            // the filtered URL. #report-apply, not button[type='submit'] (see note above).
            await page.ClickAsync("#report-apply");
            await page.WaitForURLAsync(u => u.Contains("DateFrom=2026-01-01", StringComparison.OrdinalIgnoreCase));

            // Confirm we landed on the report (not bounced to login or access-denied).
            Assert.Contains("/Reports/CatchmentProgress", page.Url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/Account/Login", page.Url, StringComparison.OrdinalIgnoreCase);

            // ── Assert: URL carries the filter params ──────────────────────────
            var currentUrl = page.Url;
            Assert.Contains("DateFrom=2026-01-01", currentUrl, StringComparison.OrdinalIgnoreCase);
            Assert.True(
                currentUrl.Contains("ValidationStatus=In+Process", StringComparison.OrdinalIgnoreCase)
                || currentUrl.Contains("ValidationStatus=In%20Process", StringComparison.OrdinalIgnoreCase),
                $"URL did not contain encoded ValidationStatus. Actual URL: {currentUrl}");

            // ── Assert: export links carry the same params ─────────────────────
            // The three export anchors are rendered with asp-all-route-data from the filter,
            // so their hrefs must also carry DateFrom and ValidationStatus.
            var exportSelectors = new[]
            {
                "a[href*='format=csv']",
                "a[href*='format=xlsx']",
                "a[href*='format=pdf']",
            };

            foreach (var selector in exportSelectors)
            {
                var href = await page.GetAttributeAsync(selector, "href");
                Assert.NotNull(href);
                Assert.True(
                    href!.Contains("DateFrom=2026-01-01", StringComparison.OrdinalIgnoreCase),
                    $"Export link ({selector}) missing DateFrom. href={href}");
                Assert.True(
                    href.Contains("ValidationStatus=In+Process", StringComparison.OrdinalIgnoreCase)
                    || href.Contains("ValidationStatus=In%20Process", StringComparison.OrdinalIgnoreCase),
                    $"Export link ({selector}) missing ValidationStatus. href={href}");
            }

            // ── Assert: form inputs are sticky (pre-populated after the filtered page loads) ──
            var dateFromValue = await page.InputValueAsync("input[name='DateFrom']");
            Assert.Equal(dateFrom, dateFromValue);

            var statusValue = await page.InputValueAsync("select[name='ValidationStatus']");
            Assert.Equal(validationStatus, statusValue);
        }
        finally
        {
            await E2EAppFixture.DisposePageAsync(page);
        }
    }

    [Fact]
    public async Task FilterForm_Clear_RemovesFiltersFromUrl()
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.ReadOnly);

            // Navigate with an active filter already in the querystring.
            await page.GotoAsync(
                "/Reports/CatchmentProgress?DateFrom=2026-01-01&ValidationStatus=In+Process",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Click the Clear link (stable id; an <a> with no route data pointing at the action).
            await page.ClickAsync("#report-clear");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // After clearing, the URL must not carry either filter parameter.
            var clearedUrl = page.Url;
            Assert.DoesNotContain("DateFrom", clearedUrl, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ValidationStatus", clearedUrl, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await E2EAppFixture.DisposePageAsync(page);
        }
    }
}
