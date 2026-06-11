using dwa_ver_val.E2E.Infrastructure;
using Microsoft.Playwright;

namespace dwa_ver_val.E2E;

/// <summary>
/// BROWSER-level regression for SEC/DOC-01: statutory letter PDFs (which contain PII) must
/// never be publicly fetchable. This is the Chromium complement to the HttpClient-level
/// <c>Tests/Integration/StaticFileExposureTests.cs</c> — here a REAL browser drives the live
/// Kestrel app + isolated E2E database and we assert browser-observable behaviour:
///
///   1. Any path under <c>/_uploads</c> returns 404 to a real browser. The Program.cs
///      middleware short-circuit (StartsWithSegments("/_uploads") → 404) fires before
///      UseStaticFiles(), so a browser navigation gets a 404 response, never a 200 / a PDF.
///      Letter blobs were also physically moved out of wwwroot to letter-blobs/.
///   2. The gated <c>FileMasterController.LetterPdf</c> action — the only legitimate way to
///      fetch a letter PDF — is protected by the controller-level
///      <c>[Authorize(Policy = CanRead)]</c>. Anonymous navigation is redirected to
///      /Account/Login by the cookie-auth pipeline (auth fires before the action, so even a
///      non-existent id never serves a PDF — it always redirects to login).
///
/// All tests here are ANONYMOUS — no login is performed.
/// </summary>
[Collection(E2ECollection.Name)]
public sealed class LetterExposureRegressionTests
{
    private readonly E2EAppFixture _fixture;

    public LetterExposureRegressionTests(E2EAppFixture fixture) => _fixture = fixture;

    /// <summary>
    /// A real browser GET of any path under <c>/_uploads</c> must receive a 404 — never a 200
    /// or a served PDF. Note: a 404 is NOT a navigation error in Playwright, so GotoAsync
    /// returns the response and we can read its status directly.
    /// </summary>
    [Theory]
    [InlineData("/_uploads/letters/VV-9476BEAF-001-S35_L1.pdf")]
    [InlineData("/_uploads/anything.pdf")]
    public async Task StaticLetterPath_AnonymousBrowser_Returns404(string path)
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            var response = await page.GotoAsync(path);

            // GotoAsync returns the HTTP response for a 404 (it is not a navigation error).
            Assert.NotNull(response);
            Assert.Equal(404, response!.Status);

            // The browser must NOT have been served a PDF.
            var contentType = await response.HeaderValueAsync("content-type");
            if (contentType is not null)
            {
                Assert.DoesNotContain("application/pdf", contentType, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            await E2EAppFixture.DisposePageAsync(page);
        }
    }

    /// <summary>
    /// The gated <c>LetterPdf</c> action requires auth. An anonymous browser navigation to it
    /// (with random ids) must be redirected to /Account/Login by the cookie-auth pipeline, and
    /// the rendered content must be the login HTML — never a PDF.
    /// </summary>
    [Fact]
    public async Task GatedLetterPdf_AnonymousBrowser_RedirectsToLogin_NotPdf()
    {
        var page = await _fixture.NewPageAsync();
        try
        {
            var response = await page.GotoAsync(
                $"/FileMaster/LetterPdf?id={Guid.NewGuid()}&letterIssuanceId={Guid.NewGuid()}",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // 1. Anonymous access is redirected to the internal login page (auth fires before
            //    the action runs, so the non-existent ids never reach the PDF-serving code).
            Assert.Contains("/Account/Login", page.Url, StringComparison.OrdinalIgnoreCase);

            // 2. The final response is the login HTML, NOT application/pdf.
            Assert.NotNull(response);
            var contentType = await response!.HeaderValueAsync("content-type");
            Assert.NotNull(contentType);
            Assert.DoesNotContain("application/pdf", contentType!, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("text/html", contentType!, StringComparison.OrdinalIgnoreCase);

            // 3. The login form actually rendered (same marker the proving test relies on).
            Assert.Equal(1, await page.Locator("input[name='Email']").CountAsync());
            Assert.Equal(1, await page.Locator("input[name='Password']").CountAsync());
        }
        finally
        {
            await E2EAppFixture.DisposePageAsync(page);
        }
    }
}
