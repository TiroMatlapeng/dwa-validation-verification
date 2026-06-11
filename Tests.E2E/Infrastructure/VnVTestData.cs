using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.E2E.Infrastructure;

/// <summary>
/// Direct-to-DB seeding and fast-forward helpers for the V&V workflow E2E tests, run against
/// the shared, isolated <c>dwa_val_ver_e2e</c> database (same connection the app uses).
///
/// Why direct DB access? Several pieces of workflow evidence have NO UI route in the system
/// (confirmed during reconnaissance): Mapbook, Entitlement-linking, Authorisation records, and
/// a letter's ServiceConfirmedDate. The CP8/CP9 "marked N/A" flags also have no UI toggle. These
/// are seeded here exactly as a back-office/import process would. Everything that DOES have a UI
/// route (case creation, per-CP evidence flags, document upload, Field &amp; Crop + SAPWAT, letter
/// issuance and responses, PAJA checklist) is driven through the real browser in the test bodies.
///
/// Every case is anchored to a freshly-seeded <see cref="Models"/> Property in the Inkomati-Usuthu
/// WMA (WMA code "3"), which is the WMA the demo Regional/Validator/Capturer users are scoped to
/// (see IdentitySeeder + SeedDataService.SeedSampleCasesAsync). That keeps each test's case inside
/// the acting user's organisational scope without depending on the shared seeded sample cases.
/// </summary>
internal static class VnVTestData
{
    public static ApplicationDBContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseSqlServer(KestrelAppFixture.E2EConnectionString).Options);

    /// <summary>Ids of the org/spatial anchors a Validator-scoped case needs in WMA "3".</summary>
    public sealed record Anchors(Guid PropertyId, Guid CatchmentAreaId, Guid OrgUnitId);

    /// <summary>
    /// Seeds a brand-new Property (with an SG code, so the CP11 guard's SG check passes) in the
    /// Inkomati-Usuthu WMA, reusing the Inkomati catchment + Mpumalanga Regional org unit that the
    /// app's own sample-case seeder creates. Returns the anchor ids for case creation.
    /// </summary>
    public static async Task<Anchors> SeedScopedPropertyAsync(string sgCodeSuffix)
    {
        await using var db = NewDb();

        var inkomati = await db.WaterManagementAreas.SingleAsync(w => w.WmaName == "Inkomati-Usuthu");
        var catchment = await db.CatchmentAreas.FirstAsync(c => c.WmaId == inkomati.WmaId);
        var orgUnit = await db.OrganisationalUnits
            .FirstAsync(o => o.Type == "Regional" && o.WmaId == inkomati.WmaId);

        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            PropertyReferenceNumber = $"E2E-{sgCodeSuffix}",
            SGCode = $"T0HT0000000{sgCodeSuffix}",
            QuaternaryDrainage = catchment.CatchmentCode,
            WmaId = inkomati.WmaId,
            CatchmentAreaId = catchment.CatchmentAreaId,
            PropertySize = 100m,
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        return new Anchors(property.PropertyId, catchment.CatchmentAreaId, orgUnit.OrgUnitId);
    }

    /// <summary>Finds the case id created in the UI by its unique registration number.</summary>
    public static async Task<Guid> FileMasterIdByRegistrationAsync(string registrationNumber)
    {
        await using var db = NewDb();
        return (await db.FileMasters.SingleAsync(f => f.RegistrationNumber == registrationNumber)).FileMasterId;
    }

    /// <summary>The current workflow state name for a case (resolved via the FileMaster→instance link).</summary>
    public static async Task<string> CurrentStateNameAsync(Guid fileMasterId)
    {
        await using var db = NewDb();
        var instance = await db.WorkflowInstances.SingleAsync(w => w.FileMasterId == fileMasterId);
        var state = await db.WorkflowStates.SingleAsync(s => s.WorkflowStateId == instance.CurrentWorkflowStateId);
        return state.StateName;
    }

    /// <summary>
    /// Fast-forwards a case directly to a named workflow state by repointing its WorkflowInstance.
    /// Used only by guard-negative tests to reach a control point cheaply WITHOUT the evidence that
    /// the guard under test demands — so the missing-evidence denial is what the test then asserts.
    /// </summary>
    public static async Task ForceStateAsync(Guid fileMasterId, string stateName)
    {
        await using var db = NewDb();
        var instance = await db.WorkflowInstances.SingleAsync(w => w.FileMasterId == fileMasterId);
        var target = await db.WorkflowStates.SingleAsync(s => s.StateName == stateName);
        instance.CurrentWorkflowStateId = target.WorkflowStateId;
        await db.SaveChangesAsync();
    }

    // ── No-UI evidence seeders ─────────────────────────────────────────────

    /// <summary>Seeds a Mapbook of the given MapType ("Qualifying" / "Current") for the case.</summary>
    public static async Task SeedMapbookAsync(Guid fileMasterId, string mapType)
    {
        await using var db = NewDb();
        db.Mapbooks.Add(new Mapbook
        {
            MapbookId = Guid.NewGuid(),
            FileMasterId = fileMasterId,
            MapbookTitle = $"{mapType} period mapbook",
            MapType = mapType,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Creates an Entitlement (using the first seeded EntitlementType) and links it to the case,
    /// satisfying the CP7 guard and the S33(2) issuance prerequisite.
    /// </summary>
    public static async Task SeedAndLinkEntitlementAsync(Guid fileMasterId, decimal volumeM3)
    {
        await using var db = NewDb();
        var type = await db.EntitlementTypes.FirstAsync();
        var entitlement = new Entitlement
        {
            EntitlementId = Guid.NewGuid(),
            Name = "ELU irrigation entitlement",
            Volume = volumeM3,
            EntitlementTypeId = type.EntitlementTypeId,
            EntitlementType = type,
        };
        db.Entitlements.Add(entitlement);
        var fm = await db.FileMasters.SingleAsync(f => f.FileMasterId == fileMasterId);
        fm.EntitlementId = entitlement.EntitlementId;
        await db.SaveChangesAsync();
    }

    /// <summary>Captures one Authorisation record (using the first seeded AuthorisationType) for CP11.</summary>
    public static async Task SeedAuthorisationAsync(Guid fileMasterId)
    {
        await using var db = NewDb();
        var type = await db.AuthorisationTypes.FirstAsync();
        db.Authorisations.Add(new Authorisation
        {
            AuthorisationId = Guid.NewGuid(),
            FileMasterId = fileMasterId,
            AuthorisationTypeId = type.AuthorisationTypeId,
            ReferenceNumber = "AUTH-E2E-001",
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Marks both Dam (CP8) and SFRA (CP9) as N/A on the case so those guards pass.</summary>
    public static async Task MarkDamAndSfraNAAsync(Guid fileMasterId)
    {
        await using var db = NewDb();
        var fm = await db.FileMasters.SingleAsync(f => f.FileMasterId == fileMasterId);
        fm.DamMarkedNA = true;
        fm.SfraMarkedNA = true;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Stamps proof-of-service on the most recent issuance of a letter code (e.g. "S35_L1"), which
    /// LetterServiceConfirmedGuard requires before the case can advance out of the *Issued state.
    /// </summary>
    public static async Task ConfirmLetterServiceAsync(Guid fileMasterId, string letterCode)
    {
        await using var db = NewDb();
        var issuance = await db.LetterIssuances
            .Include(l => l.LetterType)
            .Where(l => l.FileMasterId == fileMasterId && l.LetterType!.LetterName == letterCode)
            .OrderByDescending(l => l.IssuedDate)
            .FirstAsync();
        issuance.ServiceConfirmedDate = DateOnly.FromDateTime(DateTime.Today);
        await db.SaveChangesAsync();
    }

    /// <summary>Confirms S33(2) irrigation-board rates were paid (issuance prerequisite).</summary>
    public static async Task ConfirmRatesPaidAsync(Guid fileMasterId)
    {
        await using var db = NewDb();
        var fm = await db.FileMasters.SingleAsync(f => f.FileMasterId == fileMasterId);
        fm.S33_2_RatesPaidConfirmed = true;
        await db.SaveChangesAsync();
    }
}
