using dwa_ver_val.Services.Calculator;
using dwa_ver_val.Services.Letters;
using dwa_ver_val.Services.Letters.Templates;
using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

namespace dwa_ver_val.Tests.Services.Calculator;

public class CalculatorServiceTests
{
    [Fact]
    public async Task ComputeSapwat_SetsCalculationResult()
    {
        // Arrange
        var db = TestDbContextFactory.Create();

        var crop = new Crop { CropId = Guid.NewGuid(), CropName = "Maize" };
        var waterSource = new WaterSource { WaterSourceId = Guid.NewGuid(), WaterSourceName = "River" };
        var period = new Period { PeriodId = Guid.NewGuid(), PeriodName = "Qualifying" };
        var property = new Property { PropertyId = Guid.NewGuid(), PropertyReferenceNumber = "P-SAPWAT", SGCode = "SG-SAPWAT" };

        db.Crops.Add(crop);
        db.WaterSources.Add(waterSource);
        db.Periods.Add(period);
        db.Properties.Add(property);

        db.CropWaterRates.Add(new CropWaterRate
        {
            CropWaterRateId = Guid.NewGuid(),
            CropId = crop.CropId,
            IrrigationSystemId = null,
            RatePerHaPerAnnum = 550m
        });

        var fc = new FieldAndCrop
        {
            FieldAndCropId = Guid.NewGuid(),
            Property = property,
            PropertyId = property.PropertyId,
            Period = period,
            PeriodId = period.PeriodId,
            FieldArea = 5m,
            Crop = crop,
            WaterSource = waterSource,
            IrrigationSystem = null,
            CropArea = 4m,
            RotationFactor = 0.75m,
            SAPWATCalculationResult = 0m
        };
        db.FieldAndCrops.Add(fc);
        await db.SaveChangesAsync();

        var sut = new CalculatorService(db);

        // Act
        var result = await sut.ComputeSapwatAsync(fc.FieldAndCropId);

        // Assert
        Assert.Equal(412.5m, result);

        var reloaded = await db.FieldAndCrops.FirstAsync(f => f.FieldAndCropId == fc.FieldAndCropId);
        Assert.Equal(412.5m, reloaded.SAPWATCalculationResult);
    }

    [Fact]
    public async Task ComputeDamVolume_Method2_SetsCapacity()
    {
        // Arrange
        var db = TestDbContextFactory.Create();

        var river = new River { RiverId = Guid.NewGuid(), RiverName = "Test River" };
        var property = new Property { PropertyId = Guid.NewGuid(), PropertyReferenceNumber = "P-DAM", SGCode = "SG-DAM" };
        db.Rivers.Add(river);
        db.Properties.Add(property);

        var dam = new DamCalculation
        {
            DamCalculationId = Guid.NewGuid(),
            CalculationDate = new DateOnly(2026, 5, 19),
            Property = property,
            PropertyId = property.PropertyId,
            SateliteSurveyDate = new DateOnly(1998, 6, 1),
            River = river,
            RiverId = river.RiverId,
            CalculationMethod = "Method2",
            DamArea = 2m,
            DamDepth = 3m,
            ShapeFactor = 0.5m,
            DamCapacity = 0m
        };
        db.DamCalculations.Add(dam);
        await db.SaveChangesAsync();

        var sut = new CalculatorService(db);

        // Act
        var result = await sut.ComputeDamVolumeAsync(dam.DamCalculationId);

        // Assert: 2 ha × 3 m × 0.5 × 1000 = 3000 m³
        Assert.Equal(3000m, result);

        var reloaded = await db.DamCalculations.FirstAsync(d => d.DamCalculationId == dam.DamCalculationId);
        Assert.Equal(3000m, reloaded.DamCapacity);
    }

    [Fact]
    public async Task ComputeSfra_UpdatesForestationFields()
    {
        // Arrange
        var db = TestDbContextFactory.Create();

        var property = new Property { PropertyId = Guid.NewGuid(), PropertyReferenceNumber = "P-SFRA", SGCode = "SG-SFRA" };
        db.Properties.Add(property);

        db.SfraSpeciesRates.Add(new SfraSpeciesRate
        {
            SfraSpeciesRateId = Guid.NewGuid(),
            SpeciesName = "Pine",
            RateM3PerHaPerAnnum = 5500m
        });

        var forestation = new Forestation
        {
            ForestationId = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            Property = property,
            Specie = "Pine",
            Pre1972Hectares = 0m,
            SFRAPermitHectares = 8m,
            QualifyPeriodSFRAHectares = 12m,
            RegisteredHectares = 0m,
            RegisteredVolume = 0m
        };
        db.Forestations.Add(forestation);
        await db.SaveChangesAsync();

        var sut = new CalculatorService(db);

        // Act
        var result = await sut.ComputeSfraAsync(forestation.ForestationId);

        // Assert: authorisedHa = min(0 + 8, 12) = 8; unlawful = 12 - 8 = 4; eluVolume = 8 × 5500 = 44000
        Assert.Equal(8m, result.EluHa);
        Assert.Equal(44_000m, result.EluVolume);
        Assert.Equal(4m, result.UnlawfulHa);

        var reloaded = await db.Forestations.FirstAsync(f => f.ForestationId == forestation.ForestationId);
        Assert.Equal(8m, reloaded.ELUHectares);
        Assert.Equal(44_000m, reloaded.ELUVolume);
        Assert.Equal(4m, reloaded.UnlawfulHectares);
    }

    // ----------------------------------------------------------------------
    // DamVolumeCalculator.ComputeMethod1 — RiverDistance = 0 guard
    // ----------------------------------------------------------------------

    [Fact]
    public void Method1_ThrowsWhenRiverDistanceIsZero()
    {
        Assert.Throws<ArgumentException>(() =>
            DamVolumeCalculator.ComputeMethod1(
                wallLength: 200m,
                fetch: 50m,
                riverDistance: 0m,
                contourDifference: 10m,
                shapeFactor: 0.4m));
    }

    // ----------------------------------------------------------------------
    // CalculatorService.ComputeSapwatAsync — missing CropWaterRate row
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ComputeSapwat_ThrowsWhenNoCropWaterRate()
    {
        // Arrange — same shape as ComputeSapwat_SetsCalculationResult but with NO CropWaterRate seeded
        var db = TestDbContextFactory.Create();

        var crop = new Crop { CropId = Guid.NewGuid(), CropName = "Tobacco" };
        var waterSource = new WaterSource { WaterSourceId = Guid.NewGuid(), WaterSourceName = "River" };
        var period = new Period { PeriodId = Guid.NewGuid(), PeriodName = "Qualifying" };
        var property = new Property { PropertyId = Guid.NewGuid(), PropertyReferenceNumber = "P-SAPWAT-NoRate", SGCode = "SG-SAPWAT-NoRate" };

        db.Crops.Add(crop);
        db.WaterSources.Add(waterSource);
        db.Periods.Add(period);
        db.Properties.Add(property);

        var fc = new FieldAndCrop
        {
            FieldAndCropId = Guid.NewGuid(),
            Property = property,
            PropertyId = property.PropertyId,
            Period = period,
            PeriodId = period.PeriodId,
            FieldArea = 5m,
            Crop = crop,
            WaterSource = waterSource,
            IrrigationSystem = null,
            CropArea = 4m,
            RotationFactor = 0.75m,
            SAPWATCalculationResult = 0m
        };
        db.FieldAndCrops.Add(fc);
        await db.SaveChangesAsync();

        var sut = new CalculatorService(db);

        // Act + Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ComputeSapwatAsync(fc.FieldAndCropId));
        // CalculatorService throws "No CropWaterRate found for crop 'Tobacco'..."
        Assert.Contains("CropWaterRate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ----------------------------------------------------------------------
    // LetterService.IssueAsync — PAJA gate on S35_L3
    // ----------------------------------------------------------------------

    static CalculatorServiceTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static (LetterService svc, ApplicationDBContext db, FileMaster fm, Guid letterTypeId, IssueLetterRequest req) BuildLetterServiceForLetter3()
    {
        var db = TestDbContextFactory.Create();

        var property = new Property { PropertyId = Guid.NewGuid(), PropertyReferenceNumber = "P-L3", SGCode = "SG-L3" };
        db.Properties.Add(property);

        var fm = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            RegistrationNumber = "W-L3",
            PropertyId = property.PropertyId,
            SurveyorGeneralCode = "SG-L3",
            PrimaryCatchment = "A",
            QuaternaryCatchment = "A21A",
            FarmName = "Doornhoek",
            FarmNumber = 1,
            RegistrationDivision = "JR",
            FarmPortion = "0",
            FileCreatedDate = DateOnly.FromDateTime(DateTime.Today),
            CaseNumber = "VV-CASE-L3"
        };
        db.FileMasters.Add(fm);

        var letterType = new LetterType
        {
            LetterTypeId = Guid.NewGuid(),
            LetterName = "S35_L3",
            LetterDescription = "S35(4) ELU certificate"
        };
        db.LetterTypes.Add(letterType);
        db.SaveChanges();

        var templates = new LetterTemplateRegistry(new ILetterTemplate[] { new S35Letter3Template() });
        var blobRoot = Path.Combine(Path.GetTempPath(), "dws-letter-l3-" + Guid.NewGuid());
        var blobs = new FileSystemBlobStore(blobRoot);
        var renderer = new QuestPdfRenderer();
        var audit = new TestAuditService();

        var svc = new LetterService(db, templates, renderer, blobs, audit);

        var req = new IssueLetterRequest(
            RecipientName: "John Owner",
            RecipientAddress: "PO Box 1, Pretoria",
            IssueMethod: "RegisteredPost",
            IssueDate: new DateOnly(2026, 5, 19),
            DueDate: new DateOnly(2026, 7, 19),
            ServedByOfficialId: null,
            AdditionalNotes: null,
            SignedByUserId: Guid.NewGuid(),
            SignedByDisplayName: "Jane Manager",
            SignedByTitle: "Regional Manager",
            SignedByOrgUnit: "Limpopo Regional Office",
            LawfulVolumeM3: 12345m);

        return (svc, db, fm, letterType.LetterTypeId, req);
    }

    [Fact]
    public async Task IssueAsync_Letter3_ThrowsWhenPAJAChecklistMissing()
    {
        var (svc, _, fm, _, req) = BuildLetterServiceForLetter3();
        // No PAJAChecklist seeded.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.IssueAsync(fm.FileMasterId, "S35_L3", req));
        Assert.Contains("PAJA", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IssueAsync_Letter3_ThrowsWhenPAJAChecklistIncomplete()
    {
        var (svc, db, fm, _, req) = BuildLetterServiceForLetter3();

        // Seed a PAJAChecklist with FactualBasis missing — IsComplete returns false.
        db.PAJAChecklists.Add(new PAJAChecklist
        {
            PAJAChecklistId = Guid.NewGuid(),
            FileMasterId = fm.FileMasterId,
            FactualBasis = null,
            LegalBasis = "Legal grounds.",
            UserInputConsideration = "Considered.",
            FinalReasoning = "Reasoned.",
            CompletedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.IssueAsync(fm.FileMasterId, "S35_L3", req));
        Assert.Contains("PAJA", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IssueAsync_Letter3_SucceedsWhenPAJAChecklistComplete()
    {
        var (svc, db, fm, letterTypeId, req) = BuildLetterServiceForLetter3();

        db.PAJAChecklists.Add(new PAJAChecklist
        {
            PAJAChecklistId = Guid.NewGuid(),
            FileMasterId = fm.FileMasterId,
            FactualBasis = "Factual grounds established from field survey and WARMS data.",
            LegalBasis = "Section 35 of the National Water Act.",
            UserInputConsideration = "User submissions reviewed and addressed.",
            FinalReasoning = "On balance, ELU is confirmed at the volume stated.",
            CompletedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var issuance = await svc.IssueAsync(fm.FileMasterId, "S35_L3", req);

        Assert.NotNull(issuance);
        Assert.Equal(fm.FileMasterId, issuance.FileMasterId);
        Assert.Equal(letterTypeId, issuance.LetterTypeId);
        Assert.False(string.IsNullOrWhiteSpace(issuance.BlobPath));
        Assert.False(string.IsNullOrWhiteSpace(issuance.SignatureHash));
    }
}
