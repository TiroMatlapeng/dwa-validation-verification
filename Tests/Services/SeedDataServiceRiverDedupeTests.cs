using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Tests.Services;

/// <summary>
/// TDD tests for BUG-030 seed remediation: SeedRiversAsync self-healing dedupe.
///
/// Covers three assertions per the spec:
///   (a) only ONE row for a river name survives after seed,
///   (b) any DamCalculation that referenced a duplicate row is repointed to the
///       surviving (canonical) row,
///   (c) running seed a second time leaves exactly one row (idempotent).
/// </summary>
public class SeedDataServiceRiverDedupeTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // Minimal Property required by DamCalculation's non-null navigation.
    private static async Task<Property> SeedPropertyAsync(ApplicationDBContext db)
    {
        var prop = new Property
        {
            PropertyId = Guid.NewGuid(),
            PropertyReferenceNumber = "TEST-001",
            SGCode = "TTEST0001",
            QuaternaryDrainage = "A21A",
        };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();
        return prop;
    }

    [Fact]
    public async Task SeedAsync_RemovesDuplicateRiverRows_LeavingExactlyOne()
    {
        // ARRANGE — pre-insert 3× "Limpopo" rows (simulates earlier concurrency bug)
        using var db = NewDb();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        db.Rivers.AddRange(
            new River { RiverId = id1, RiverName = "Limpopo" },
            new River { RiverId = id2, RiverName = "Limpopo" },
            new River { RiverId = id3, RiverName = "Limpopo" }
        );
        await db.SaveChangesAsync();

        // ACT
        var svc = new SeedDataService(db);
        await svc.SeedAsync();

        // ASSERT (a): exactly one "Limpopo" row remains
        var remaining = await db.Rivers
            .Where(r => r.RiverName == "Limpopo")
            .ToListAsync();
        Assert.Single(remaining);
    }

    [Fact]
    public async Task SeedAsync_RepointsDamCalculationFk_ToSurvivingRow()
    {
        // ARRANGE — 3× "Limpopo"; one DamCalculation references the SECOND duplicate
        using var db = NewDb();
        var prop = await SeedPropertyAsync(db);

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        // Make id1 the lowest GUID so it will be chosen as canonical
        // (canonical selection: prefer a row referenced by DamCalculation, else lowest Id)
        // Here we reference id2 (not the lowest) so the repoint logic is exercised.
        db.Rivers.AddRange(
            new River { RiverId = id1, RiverName = "Limpopo" },
            new River { RiverId = id2, RiverName = "Limpopo" },
            new River { RiverId = id3, RiverName = "Limpopo" }
        );

        // DamCalculation references id2 (a non-canonical duplicate).
        // Use RiverId FK scalar only — EF already tracks the River entities above,
        // so we must NOT supply a new River navigation object here.
        var river2 = await db.Rivers.FindAsync(id2);
        var dam = new DamCalculation
        {
            DamCalculationId = Guid.NewGuid(),
            PropertyId = prop.PropertyId,
            Property = prop,
            CalculationDate = DateOnly.FromDateTime(DateTime.Today),
            SateliteSurveyDate = DateOnly.FromDateTime(DateTime.Today),
            DamCapacity = 1000m,
            RiverId = id2,
            River = river2!,
            DamCalculationStatus = DamCalculationStatus.COMPLETED,
        };
        db.DamCalculations.Add(dam);
        await db.SaveChangesAsync();

        // ACT
        var svc = new SeedDataService(db);
        await svc.SeedAsync();

        // ASSERT (b): the DamCalculation now points at the surviving row
        var surviving = await db.Rivers
            .Where(r => r.RiverName == "Limpopo")
            .SingleAsync();

        var updatedDam = await db.DamCalculations.FindAsync(dam.DamCalculationId);
        Assert.NotNull(updatedDam);
        Assert.Equal(surviving.RiverId, updatedDam.RiverId);
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_SecondRunKeepsOneRow()
    {
        // ARRANGE — pre-insert 3× "Limpopo"
        using var db = NewDb();
        db.Rivers.AddRange(
            new River { RiverId = Guid.NewGuid(), RiverName = "Limpopo" },
            new River { RiverId = Guid.NewGuid(), RiverName = "Limpopo" },
            new River { RiverId = Guid.NewGuid(), RiverName = "Limpopo" }
        );
        await db.SaveChangesAsync();

        var svc = new SeedDataService(db);

        // ACT — run twice
        await svc.SeedAsync();
        await svc.SeedAsync();

        // ASSERT (c): still exactly one "Limpopo" row
        var remaining = await db.Rivers
            .Where(r => r.RiverName == "Limpopo")
            .ToListAsync();
        Assert.Single(remaining);
    }

    [Fact]
    public async Task SeedAsync_PrefersDamCalcReferencedRow_AsCanonical()
    {
        // ARRANGE — 3 duplicates; the THIRD (highest Id) is referenced by a DamCalc.
        // The canonical-selection rule must prefer the FK-referenced row, not just lowest Id.
        using var db = NewDb();
        var prop = await SeedPropertyAsync(db);

        // Craft GUIDs such that id3 > id2 > id1 (lowest first for clarity)
        var id1 = new Guid("00000000-0000-0000-0000-000000000001");
        var id2 = new Guid("00000000-0000-0000-0000-000000000002");
        var id3 = new Guid("00000000-0000-0000-0000-000000000003");

        db.Rivers.AddRange(
            new River { RiverId = id1, RiverName = "Vaal" },
            new River { RiverId = id2, RiverName = "Vaal" },
            new River { RiverId = id3, RiverName = "Vaal" }
        );

        // DamCalculation references id3 (highest — would NOT be chosen by lowest-Id rule alone).
        // Use the already-tracked entity to avoid duplicate-tracking conflict.
        var river3 = await db.Rivers.FindAsync(id3);
        var dam = new DamCalculation
        {
            DamCalculationId = Guid.NewGuid(),
            PropertyId = prop.PropertyId,
            Property = prop,
            CalculationDate = DateOnly.FromDateTime(DateTime.Today),
            SateliteSurveyDate = DateOnly.FromDateTime(DateTime.Today),
            DamCapacity = 500m,
            RiverId = id3,
            River = river3!,
            DamCalculationStatus = DamCalculationStatus.COMPLETED,
        };
        db.DamCalculations.Add(dam);
        await db.SaveChangesAsync();

        // ACT
        var svc = new SeedDataService(db);
        await svc.SeedAsync();

        // ASSERT: surviving row is id3 (the one referenced by DamCalc)
        var surviving = await db.Rivers.Where(r => r.RiverName == "Vaal").SingleAsync();
        Assert.Equal(id3, surviving.RiverId);

        var updatedDam = await db.DamCalculations.FindAsync(dam.DamCalculationId);
        Assert.NotNull(updatedDam);
        Assert.Equal(id3, updatedDam.RiverId);
    }
}
