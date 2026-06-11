using System.Net;
using Xunit;

namespace dwa_ver_val.Tests.Integration;

/// <summary>
/// Regression tests for SEC/DOC-01: verifies that the /_uploads path is never served by
/// UseStaticFiles() — not even for unauthenticated callers — so that letter PDFs containing
/// PII cannot be fetched without going through the scope-checked FileMasterController.LetterPdf action.
///
/// These tests use the real WebApplicationFactory so the full middleware pipeline (including the
/// /_uploads short-circuit block and UseStaticFiles) runs exactly as it does in production.
/// No SQL interaction is needed — the middleware block fires before routing, so no DB seed
/// or authenticated user is required.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public class StaticFileExposureTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _factory;

    public StaticFileExposureTests(IntegrationTestFixture factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// An unauthenticated GET to any path under /_uploads must return 404 — the middleware
    /// short-circuit must fire before UseStaticFiles() can serve the file.
    /// This is the primary regression assertion for SEC/DOC-01.
    /// </summary>
    [Theory]
    [InlineData("/_uploads/letters/VV-9476BEAF-001-S35_L1.pdf")]
    [InlineData("/_uploads/letters/anything.pdf")]
    [InlineData("/_uploads/")]
    [InlineData("/_uploads/some/nested/path.pdf")]
    public async Task UploadsPath_Unauthenticated_Returns404(string path)
    {
        // Arrange — fresh client with no auth cookie (AllowAutoRedirect=false so a 302
        // does not mask a potential 200 from the static-files middleware).
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        // Act
        var response = await client.GetAsync(path);

        // Assert — must be 404 (the /_uploads middleware block), never 200 or 206.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Sanity check: a legitimate static asset (login page CSS) is still served normally,
    /// confirming we only blocked /_uploads and left the rest of UseStaticFiles() intact.
    /// </summary>
    [Fact]
    public async Task LegitimateStaticFile_IsStillServed()
    {
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        var response = await client.GetAsync("/css/dws.css");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
