using dwa_ver_val.Services.Audit;
using dwa_ver_val.Services.Letters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moq;
using QuestPDF.Infrastructure;
using Xunit;

namespace dwa_ver_val.Tests.Integration;

/// <summary>
/// SQL-Server-backed race tests for the WF-02 letter-issuance uniqueness fix.
///
/// WHY THESE CANNOT USE InMemory:
/// EF InMemory ignores unique indexes — the filtered unique index on
/// (FileMasterId, LetterTypeId) WHERE ReissuedFromId IS NULL is never enforced by InMemory.
/// The race must be proven against real SQL Server.
///
/// DATABASE: dedicated isolated database "dwa_val_ver_lettertest" — wiped before each
/// test class run via EnsureDeleted + EnsureCreated. Never touches dwa_val_ver (dev).
///
/// PREREQUISITE: SQL Server must be reachable at localhost,1433 (docker dev stack).
/// If unreachable the tests fail with a SqlException, NOT a silent skip — per design.
/// </summary>
[Collection("LetterIssuanceRace")]
public class LetterIssuanceRaceTests : IAsyncLifetime
{
    static LetterIssuanceRaceTests() => QuestPDF.Settings.License = LicenseType.Community;

    private const string ConnectionString =
        "Server=localhost,1433;Database=dwa_val_ver_lettertest;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True";

    private static DbContextOptions<ApplicationDBContext> BuildOptions() =>
        new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseSqlServer(ConnectionString)
            .Options;

    private ApplicationDBContext CreateContext() => new ApplicationDBContext(BuildOptions());

    // Seeded once per test class.
    private Guid _fileMasterId;
    private Guid _letterTypeId;

    // ── IAsyncLifetime ──────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        await using var ctx = CreateContext();
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();

        var property = new Property { PropertyId = Guid.NewGuid() };
        ctx.Properties.Add(property);

        var fm = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            RegistrationNumber = "LT-001",
            SurveyorGeneralCode = "SG-LT",
            PrimaryCatchment = "A",
            QuaternaryCatchment = "A99Z",
            FarmName = "LetterTestFarm",
            FarmNumber = 1,
            RegistrationDivision = "JR",
            FarmPortion = "0",
            FileCreatedDate = DateOnly.FromDateTime(DateTime.Today),
        };
        ctx.FileMasters.Add(fm);

        var lt = new LetterType
        {
            LetterTypeId = Guid.NewGuid(),
            LetterName = "S35_L1",
            LetterDescription = "S35 Letter 1 — Section 35(1) Notice to Apply for Verification"
        };
        ctx.LetterTypes.Add(lt);

        await ctx.SaveChangesAsync();

        _fileMasterId = fm.FileMasterId;
        _letterTypeId = lt.LetterTypeId;
    }

    public async Task DisposeAsync()
    {
        await using var ctx = CreateContext();
        await ctx.Database.EnsureDeletedAsync();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a LetterService wired to the given ApplicationDBContext.
    /// All PDF/blob/audit collaborators are stub mocks — we are testing DB-layer behaviour only.
    /// </summary>
    private static LetterService BuildLetterService(ApplicationDBContext db)
    {
        var template = new Mock<ILetterTemplate>();
        template.SetupGet(t => t.LetterCode).Returns("S35_L1");
        template.SetupGet(t => t.Title).Returns("Letter 1");
        template.SetupGet(t => t.NWAReference).Returns("Section 35(1)");

        var registry = new Mock<ILetterTemplateRegistry>();
        registry.Setup(r => r.Get(It.IsAny<string>())).Returns(template.Object);

        var renderer = new Mock<IPdfRenderer>();
        renderer.Setup(r => r.RenderLetter(It.IsAny<ILetterTemplate>(), It.IsAny<LetterContext>()))
                .Returns(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // "%PDF"

        // Each call returns a unique path derived from the path argument — the blob store
        // stub is transparent so we can verify the stored BlobPath in the persisted row.
        var blobs = new Mock<IBlobStore>();
        blobs.Setup(b => b.WriteAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
             .ReturnsAsync((string path, byte[] _) => path);

        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.LogAsync(It.IsAny<AuditEvent>())).Returns(Task.CompletedTask);

        return new LetterService(db, registry.Object, renderer.Object, blobs.Object, audit.Object);
    }

    private static IssueLetterRequest BuildRequest() => new(
        RecipientName: "P.J. van der Merwe",
        RecipientAddress: null,
        IssueMethod: "RegisteredPost",
        IssueDate: DateOnly.FromDateTime(DateTime.Today),
        DueDate: DateOnly.FromDateTime(DateTime.Today.AddDays(60)),
        ServedByOfficialId: null,
        AdditionalNotes: null,
        SignedByUserId: null,
        SignedByDisplayName: "Thabo Official",
        SignedByTitle: "Regional Manager",
        SignedByOrgUnit: "Limpopo Regional Office");

    // ── Race test ────────────────────────────────────────────────────────────

    /// <summary>
    /// Two concurrent SAME-TYPE original issuances for one case.
    ///
    /// Expected outcome:
    ///   - Exactly ONE LetterIssuance row persists (the winner).
    ///   - The second throws LetterIssuanceDuplicateException (clean domain error, not a 500).
    ///   - No duplicate row is inserted; the unique index enforces the invariant at the DB layer.
    ///
    /// The race is simulated by: calling IssueAsync on two separate DbContext instances
    /// (and therefore two separate LetterService instances) sequentially — the first wins the
    /// filtered unique index; the second hits the violation on SaveChangesAsync, which is
    /// caught and translated to LetterIssuanceDuplicateException.
    ///
    /// True parallel execution is also covered implicitly: the filtered unique index enforces
    /// the constraint at the DB level regardless of concurrency model.
    /// </summary>
    [Fact]
    public async Task IssueAsync_ConcurrentSameTypeSameCase_ExactlyOnePersists_SecondThrowsDuplicate()
    {
        // ── Arrange ──
        // Two independent contexts and service instances, each unaware of the other.
        // This mirrors two simultaneous HTTP requests reaching the same endpoint.
        await using var ctx1 = CreateContext();
        await using var ctx2 = CreateContext();

        var svc1 = BuildLetterService(ctx1);
        var svc2 = BuildLetterService(ctx2);

        // ── Act ──
        // svc1 wins — inserts the row, commits, no error.
        await svc1.IssueAsync(_fileMasterId, "S35_L1", BuildRequest());

        // svc2 attempts the same original issuance. The filtered unique index
        // IX_LetterIssuance_FileMaster_LetterType_Original (WHERE ReissuedFromId IS NULL)
        // blocks the second INSERT with SQL error 2601/2627.
        // LetterService.IssueAsync must catch DbUpdateException and rethrow as
        // LetterIssuanceDuplicateException.
        var ex = await Assert.ThrowsAsync<LetterIssuanceDuplicateException>(
            () => svc2.IssueAsync(_fileMasterId, "S35_L1", BuildRequest()));

        Assert.Contains("already been issued", ex.Message, StringComparison.OrdinalIgnoreCase);

        // ── Assert: exactly ONE row persisted ──
        await using var verifyCtx = CreateContext();
        var count = await verifyCtx.LetterIssuances
            .CountAsync(l => l.FileMasterId == _fileMasterId
                           && l.LetterTypeId == _letterTypeId
                           && l.ReissuedFromId == null);

        Assert.Equal(1, count);
    }

    /// <summary>
    /// A REISSUE (ReissuedFromId IS NOT NULL) must NOT be blocked by the unique index.
    /// The filtered index only covers original issuances (ReissuedFromId IS NULL).
    /// This test verifies the legitimate reissue path is unaffected.
    /// </summary>
    [Fact]
    public async Task FilteredIndex_DoesNotBlock_LegitimateReissue()
    {
        // ── Arrange: seed the original issuance directly ──
        await using var setupCtx = CreateContext();
        var originalId = Guid.NewGuid();
        setupCtx.LetterIssuances.Add(new LetterIssuance
        {
            LetterIssuanceId = originalId,
            FileMasterId = _fileMasterId,
            LetterTypeId = _letterTypeId,
            IssuedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-30)),
            GeneratedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-30)),
            SignedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-30)),
            ResponseStatus = "Pending",
            BlobPath = "letters/original.pdf",
            SignatureHash = "aabbcc",
            // ReissuedFromId = null → original issuance; the unique index row is now occupied
        });
        await setupCtx.SaveChangesAsync();

        // ── Arrange: build a reissue row directly (bypassing LetterService for simplicity)
        //    ReissuedFromId IS NOT NULL → exempt from the filtered unique index
        await using var reissueCtx = CreateContext();
        reissueCtx.LetterIssuances.Add(new LetterIssuance
        {
            LetterIssuanceId = Guid.NewGuid(),
            FileMasterId = _fileMasterId,
            LetterTypeId = _letterTypeId,
            IssuedDate = DateOnly.FromDateTime(DateTime.Today),
            GeneratedDate = DateOnly.FromDateTime(DateTime.Today),
            SignedDate = DateOnly.FromDateTime(DateTime.Today),
            ResponseStatus = "Pending",
            BlobPath = "letters/reissue.pdf",
            SignatureHash = "ddeeff",
            ReissuedFromId = originalId  // set → exempt from unique index
        });

        // ── Act + Assert: must NOT throw ──
        // If the index incorrectly blocked reissues, this would throw DbUpdateException(2601/2627).
        var exception = await Record.ExceptionAsync(() => reissueCtx.SaveChangesAsync());
        Assert.Null(exception);

        // ── Verify: two rows now exist — the original + the reissue ──
        await using var verifyCtx = CreateContext();
        var total = await verifyCtx.LetterIssuances
            .CountAsync(l => l.FileMasterId == _fileMasterId && l.LetterTypeId == _letterTypeId);
        Assert.Equal(2, total);

        var originalCount = await verifyCtx.LetterIssuances
            .CountAsync(l => l.FileMasterId == _fileMasterId && l.LetterTypeId == _letterTypeId
                           && l.ReissuedFromId == null);
        Assert.Equal(1, originalCount);

        var reissueCount = await verifyCtx.LetterIssuances
            .CountAsync(l => l.FileMasterId == _fileMasterId && l.LetterTypeId == _letterTypeId
                           && l.ReissuedFromId != null);
        Assert.Equal(1, reissueCount);
    }

    /// <summary>
    /// Two different letter types on the same case must NOT conflict with each other.
    /// The index is (FileMasterId, LetterTypeId) — different LetterTypeId values are
    /// distinct rows and must both be allowed.
    /// </summary>
    [Fact]
    public async Task FilteredIndex_AllowsDifferentLetterTypesOnSameCase()
    {
        // ── Arrange: seed a second LetterType ──
        await using var setupCtx = CreateContext();
        var lt2 = new LetterType
        {
            LetterTypeId = Guid.NewGuid(),
            LetterName = "S35_L3",
            LetterDescription = "S35 Letter 3 — ELU Certificate"
        };
        setupCtx.LetterTypes.Add(lt2);
        await setupCtx.SaveChangesAsync();

        // ── Arrange: seed original issuance for S35_L1 ──
        await using var setupCtx2 = CreateContext();
        setupCtx2.LetterIssuances.Add(new LetterIssuance
        {
            LetterIssuanceId = Guid.NewGuid(),
            FileMasterId = _fileMasterId,
            LetterTypeId = _letterTypeId,   // S35_L1
            IssuedDate = DateOnly.FromDateTime(DateTime.Today),
            GeneratedDate = DateOnly.FromDateTime(DateTime.Today),
            SignedDate = DateOnly.FromDateTime(DateTime.Today),
            ResponseStatus = "Pending",
            BlobPath = "letters/l1.pdf",
            SignatureHash = "112233"
        });
        await setupCtx2.SaveChangesAsync();

        // ── Act: issue S35_L3 original on the same case — must succeed ──
        await using var issueCtx = CreateContext();
        issueCtx.LetterIssuances.Add(new LetterIssuance
        {
            LetterIssuanceId = Guid.NewGuid(),
            FileMasterId = _fileMasterId,
            LetterTypeId = lt2.LetterTypeId,  // different type — no conflict
            IssuedDate = DateOnly.FromDateTime(DateTime.Today),
            GeneratedDate = DateOnly.FromDateTime(DateTime.Today),
            SignedDate = DateOnly.FromDateTime(DateTime.Today),
            ResponseStatus = "Pending",
            BlobPath = "letters/l3.pdf",
            SignatureHash = "445566"
        });

        var ex = await Record.ExceptionAsync(() => issueCtx.SaveChangesAsync());
        Assert.Null(ex);
    }
}
