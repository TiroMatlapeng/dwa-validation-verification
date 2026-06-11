using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dwa_ver_val.E2E.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Xunit;

namespace dwa_ver_val.E2E;

/// <summary>
/// Browser E2E for SEC/DOC-02 (virus-scan gate). Proves end-to-end that:
///   - a CLEAN uploaded document is scanned, persisted as "Clean", and downloadable; and
///   - a document carrying the EICAR test signature (in a valid-PDF wrapper so it passes the
///     magic-byte check and actually reaches the virus scanner) is REJECTED on upload and
///     never persisted — so it can never be downloaded.
/// Runs the real Validator upload flow against the live Kestrel app + isolated E2E DB.
/// </summary>
[Collection(E2ECollection.Name)]
public sealed class DocumentVirusScanTests
{
    private readonly E2EAppFixture _fixture;
    public DocumentVirusScanTests(E2EAppFixture fixture) => _fixture = fixture;

    // The standard, harmless EICAR antivirus test signature.
    private const string Eicar = @"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";

    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseSqlServer(KestrelAppFixture.E2EConnectionString).Options);

    // A minimal but valid PDF: starts with the %PDF magic bytes (passes FileSignatureValidator),
    // with the given body embedded.
    private static byte[] Pdf(string body) =>
        Encoding.ASCII.GetBytes("%PDF-1.4\n" + body + "\n%%EOF\n");

    private async Task<Guid> FirstSeededFileMasterIdAsync()
    {
        await using var db = NewDb();
        var fm = await db.FileMasters.OrderBy(f => f.FileMasterId).FirstAsync();
        return fm.FileMasterId;
    }

    [Fact]
    public async Task CleanUpload_IsScannedClean_AndDownloadable()
    {
        var fileMasterId = await FirstSeededFileMasterIdAsync();
        const string fileName = "clean-evidence.pdf";

        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.Validator("3"));
            await page.GotoAsync($"/Document/Upload?fileMasterId={fileMasterId}",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            Assert.DoesNotContain("/Account/Login", page.Url, StringComparison.OrdinalIgnoreCase);

            await page.SetInputFilesAsync("input[name='File']", new FilePayload
            {
                Name = fileName,
                MimeType = "application/pdf",
                Buffer = Pdf("clean evidence document body"),
            });
            // Upload form submit button id — NOT button[type=submit] (layout has a Sign out submit).
            await page.ClickAsync("#upload-submit");
            // Success → redirect to the case Details page.
            await page.WaitForURLAsync(u => u.Contains("/FileMaster/Details", StringComparison.OrdinalIgnoreCase));

            // Confirm it persisted as Clean, and grab its id from the shared E2E DB.
            Guid docId;
            await using (var db = NewDb())
            {
                var doc = await db.Documents
                    .Where(d => d.FileMasterId == fileMasterId && d.FileName == fileName)
                    .OrderByDescending(d => d.UploadDate)
                    .FirstAsync();
                Assert.Equal("Clean", doc.VirusScanStatus);
                docId = doc.DocumentId;
            }

            // Download through the authenticated browser session (APIRequest shares the context cookies).
            var resp = await page.Context.APIRequest.GetAsync(
                $"{_fixture.BaseUrl}/Document/Download?documentId={docId}");
            Assert.Equal(200, resp.Status);
            var bytes = await resp.BodyAsync();
            Assert.True(bytes.Length >= 4, "Downloaded body is empty.");
            Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        }
        finally
        {
            await E2EAppFixture.DisposePageAsync(page);
        }
    }

    [Fact]
    public async Task EicarUpload_IsRejected_AndNeverPersisted()
    {
        var fileMasterId = await FirstSeededFileMasterIdAsync();
        const string fileName = "eicar-evil.pdf";

        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, DemoUsers.Validator("3"));
            await page.GotoAsync($"/Document/Upload?fileMasterId={fileMasterId}",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            await page.SetInputFilesAsync("input[name='File']", new FilePayload
            {
                Name = fileName,
                MimeType = "application/pdf",
                // Valid %PDF header (passes magic-byte) + EICAR signature in the body (scanner flags it).
                Buffer = Pdf(Eicar),
            });
            await page.ClickAsync("#upload-submit");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Rejected → the POST re-renders the Upload view (NOT a redirect to Details), with the
            // virus-scan error shown.
            Assert.Contains("/Document/Upload", page.Url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/FileMaster/Details", page.Url, StringComparison.OrdinalIgnoreCase);
            var body = await page.InnerTextAsync("body");
            Assert.Contains("failed virus scanning", body, StringComparison.OrdinalIgnoreCase);

            // And nothing was persisted for it — it can never be downloaded.
            await using var db = NewDb();
            var persisted = await db.Documents
                .AnyAsync(d => d.FileMasterId == fileMasterId && d.FileName == fileName);
            Assert.False(persisted, "An EICAR-infected upload must not persist a Document row.");
        }
        finally
        {
            await E2EAppFixture.DisposePageAsync(page);
        }
    }
}
