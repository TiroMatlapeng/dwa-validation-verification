using dwa_ver_val.Services.Calculator;
using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace dwa_ver_val.Tests.Services;

public class LawfulnessAssessmentServiceTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static (ApplicationDBContext db, Guid fileMasterId, Guid propertyId) SeedBasicCase(
        Guid? gwcaId = null)
    {
        var db = TestDbContextFactory.Create();

        var eluType = new EntitlementType
        {
            EntitlementTypeId = Guid.NewGuid(),
            EntitlementName = "ELU_Irrigation",
            EntitlementDescription = "ELU Irrigation"
        };
        db.EntitlementTypes.Add(eluType);

        GovernmentWaterControlArea? gwca = null;
        if (gwcaId.HasValue)
        {
            gwca = new GovernmentWaterControlArea
            {
                WaterControlAreaId = gwcaId.Value,
                GovernmentWaterControlAreaName = "Test GWCA"
            };
            db.GovernmentWaterControlAreas.Add(gwca);

            db.GwcaProclamationRules.AddRange(
                new GwcaProclamationRule { RuleId = Guid.NewGuid(), WaterControlAreaId = gwcaId.Value, RuleCode = "MAX_HECTARES",             IsActive = true, NumericLimit = 30m,     Unit = "ha",    RuleDescription = "Max ha" },
                new GwcaProclamationRule { RuleId = Guid.NewGuid(), WaterControlAreaId = gwcaId.Value, RuleCode = "MAX_IRRIGABLE_PCT",        IsActive = true, NumericLimit = 53m,     Unit = "pct",   RuleDescription = "Max pct" },
                new GwcaProclamationRule { RuleId = Guid.NewGuid(), WaterControlAreaId = gwcaId.Value, RuleCode = "MAX_VOLUME_PER_HA",        IsActive = true, NumericLimit = 9_900m,  Unit = "m3/ha", RuleDescription = "Max vol/ha" },
                new GwcaProclamationRule { RuleId = Guid.NewGuid(), WaterControlAreaId = gwcaId.Value, RuleCode = "MAX_STORAGE_PER_HA",       IsActive = true, NumericLimit = 5_000m,  Unit = "m3/ha", RuleDescription = "Max storage/ha" },
                new GwcaProclamationRule { RuleId = Guid.NewGuid(), WaterControlAreaId = gwcaId.Value, RuleCode = "MAX_STORAGE_PER_PROPERTY", IsActive = true, NumericLimit = 50_000m, Unit = "m3",    RuleDescription = "Max storage prop" }
            );
        }

        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            PropertyReferenceNumber = "P-LAW-01",
            SGCode = "SG-LAW-01",
            IrrigableAreaHa = 80m,
            WaterControlAreaId = gwca?.WaterControlAreaId
        };
        db.Properties.Add(property);

        var fm = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            RegistrationNumber = "WARMS-LAW-01",
            SurveyorGeneralCode = "SG-LAW-01",
            PrimaryCatchment = "A21A",
            QuaternaryCatchment = "A21A",
            FarmName = "Test Farm",
            FarmNumber = 1,
            RegistrationDivision = "RD",
            FarmPortion = "0"
        };
        db.FileMasters.Add(fm);

        db.SaveChanges();
        return (db, fm.FileMasterId, property.PropertyId);
    }

    private static void SeedFieldAndCrop(ApplicationDBContext db, Guid propertyId, decimal sapwatRate, decimal cropArea)
    {
        var period = new Period { PeriodId = Guid.NewGuid(), PeriodName = "Qualifying Period: 1 Oct 1996 - 30 Sep 1998" };
        var crop = new Crop { CropId = Guid.NewGuid(), CropName = "Maize" };
        var ws = new WaterSource { WaterSourceId = Guid.NewGuid(), WaterSourceName = "River" };
        var property = db.Properties.Find(propertyId)!;
        db.Periods.Add(period);
        db.Crops.Add(crop);
        db.WaterSources.Add(ws);
        db.SaveChanges();

        db.FieldAndCrops.Add(new FieldAndCrop
        {
            FieldAndCropId = Guid.NewGuid(),
            Property = property,
            PropertyId = propertyId,
            Period = period,
            PeriodId = period.PeriodId,
            FieldArea = cropArea,
            CropArea = cropArea,
            Crop = crop,
            WaterSource = ws,
            SAPWATCalculationResult = sapwatRate,
            RotationFactor = 1m
        });
        db.SaveChanges();
    }

    private static void SeedDamCalculation(ApplicationDBContext db, Guid propertyId, decimal capacity)
    {
        var river = new River { RiverId = Guid.NewGuid(), RiverName = "Test River" };
        var property = db.Properties.Find(propertyId)!;
        db.Rivers.Add(river);
        db.SaveChanges();

        db.DamCalculations.Add(new DamCalculation
        {
            DamCalculationId = Guid.NewGuid(),
            Property = property,
            PropertyId = propertyId,
            River = river,
            RiverId = river.RiverId,
            CalculationDate = new DateOnly(2026, 1, 1),
            SateliteSurveyDate = new DateOnly(1998, 1, 1),
            DamCapacity = capacity,
            DamCalculationStatus = DamCalculationStatus.IN_PROGRESS
        });
        db.SaveChanges();
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AssessAsync_GeneralPath_CreatesResultAndSetsEntitlement()
    {
        var (db, fileMasterId, propertyId) = SeedBasicCase();
        // 10 ha × 500 mm/ha × 10 = 50,000 m³ demand; dam 30,000 m³ — both within general limits
        SeedFieldAndCrop(db, propertyId, sapwatRate: 500m, cropArea: 10m);
        SeedDamCalculation(db, propertyId, capacity: 30_000m);

        var sut = new LawfulnessAssessmentService(db);
        var result = await sut.AssessAsync(fileMasterId);

        Assert.Equal("General", result.LegalFramework);
        Assert.Equal(50_000m, result.TotalIrrigationDemandM3);
        Assert.Equal(50_000m, result.LawfulIrrigationM3);
        Assert.Equal(0m, result.UnlawfulIrrigationM3);
        Assert.Equal(30_000m, result.LawfulStorageM3);
        Assert.Equal(0m, result.UnlawfulStorageM3);

        // FileMaster.EntitlementId must be set (satisfies Cp7EluGuard)
        var fm = await db.FileMasters.FindAsync(fileMasterId);
        Assert.True(fm!.EntitlementId.HasValue);
        var ent = await db.Entitlements.FindAsync(fm.EntitlementId!.Value);
        Assert.Equal(50_000m, ent!.Volume);
    }

    [Fact]
    public async Task AssessAsync_GwcaPath_LegalFrameworkIsGwca()
    {
        var gwcaId = Guid.NewGuid();
        var (db, fileMasterId, propertyId) = SeedBasicCase(gwcaId: gwcaId);
        SeedFieldAndCrop(db, propertyId, sapwatRate: 500m, cropArea: 10m);

        var sut = new LawfulnessAssessmentService(db);
        var result = await sut.AssessAsync(fileMasterId);

        Assert.Equal("GWCA", result.LegalFramework);
        Assert.Equal(gwcaId, result.GwcaId);
    }

    [Fact]
    public async Task AssessAsync_NoFieldAndCropRecords_ZeroDemand()
    {
        var (db, fileMasterId, _) = SeedBasicCase();
        // No FieldAndCrop, no DamCalculation seeded

        var sut = new LawfulnessAssessmentService(db);
        var result = await sut.AssessAsync(fileMasterId);

        Assert.Equal(0m, result.TotalIrrigationDemandM3);
        Assert.Equal(0m, result.LawfulIrrigationM3);
        Assert.Equal(0m, result.TotalDamCapacityM3);
    }

    [Fact]
    public async Task AssessAsync_RunTwice_UpdatesExistingResultInPlace()
    {
        var (db, fileMasterId, propertyId) = SeedBasicCase();
        SeedFieldAndCrop(db, propertyId, sapwatRate: 200m, cropArea: 5m);

        var sut = new LawfulnessAssessmentService(db);
        var first = await sut.AssessAsync(fileMasterId);
        var firstId = first.LawfulnessAssessmentResultId;

        var second = await sut.AssessAsync(fileMasterId);
        Assert.Equal(firstId, second.LawfulnessAssessmentResultId); // same row updated
    }

    [Fact]
    public async Task AssessAsync_MissingFileMaster_ThrowsInvalidOperation()
    {
        var db = TestDbContextFactory.Create();
        var sut = new LawfulnessAssessmentService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.AssessAsync(Guid.NewGuid()));
    }
}
