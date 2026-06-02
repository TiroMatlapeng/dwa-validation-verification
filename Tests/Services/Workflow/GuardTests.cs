using dwa_ver_val.Services.Workflow;
using dwa_ver_val.Services.Workflow.Guards;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace dwa_ver_val.Tests.Services.Workflow;

public class GuardTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static FileMaster Case(
        DateTime? spatialAt = null,
        DateTime? warmsAt = null,
        DateTime? addlAt = null,
        bool damNA = false,
        bool sfraNA = false,
        Guid? propertyId = null)
    {
        return new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = propertyId ?? Guid.NewGuid(),
            FileNumber = "T",
            RegistrationNumber = "N/A",
            SurveyorGeneralCode = "N/A",
            PrimaryCatchment = "N/A",
            QuaternaryCatchment = "N/A",
            FarmName = "N/A",
            FarmNumber = 0,
            RegistrationDivision = "N/A",
            FarmPortion = "N/A",
            SpatialInfoConfirmedAt = spatialAt,
            WarmsReviewedAt = warmsAt,
            AdditionalInfoReviewedAt = addlAt,
            DamMarkedNA = damNA,
            SfraMarkedNA = sfraNA
        };
    }

    private static GuardContext Leaving(FileMaster fm, string fromCp, string toCp) =>
        new(fm,
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = $"{fromCp}_Step", DisplayOrder = 1, Phase = "Test" },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = $"{toCp}_Step", DisplayOrder = 2, Phase = "Test" });

    [Fact]
    public async Task Cp2_DeniesWhenFlagMissing_AllowsWhenSet()
    {
        var sut = new Cp2SpatialInfoGuard();
        var denied = await sut.CheckAsync(Leaving(Case(), "CP2", "CP3"));
        Assert.False(denied.Allowed);
        var allowed = await sut.CheckAsync(Leaving(Case(spatialAt: DateTime.UtcNow), "CP2", "CP3"));
        Assert.True(allowed.Allowed);
    }

    [Fact]
    public async Task Cp3_DeniesWhenFlagMissing() =>
        Assert.False((await new Cp3WarmsReviewedGuard().CheckAsync(Leaving(Case(), "CP3", "CP4"))).Allowed);

    [Fact]
    public async Task Cp4_DeniesWhenFlagMissing() =>
        Assert.False((await new Cp4AdditionalInfoGuard().CheckAsync(Leaving(Case(), "CP4", "CP5"))).Allowed);

    [Fact]
    public async Task Cp5_DeniesWhenNoMapbook_AllowsWhenOnePresent()
    {
        using var db = NewDb();
        var fm = Case();
        db.FileMasters.Add(fm);
        await db.SaveChangesAsync();
        var sut = new Cp5MapbookPresentGuard(db);
        var denied = await sut.CheckAsync(Leaving(fm, "CP5", "CP6"));
        Assert.False(denied.Allowed);

        db.Mapbooks.Add(new Mapbook { MapbookId = Guid.NewGuid(), FileMasterId = fm.FileMasterId, MapbookTitle = "Q", MapType = "Qualifying" });
        await db.SaveChangesAsync();
        var allowed = await sut.CheckAsync(Leaving(fm, "CP5", "CP6"));
        Assert.True(allowed.Allowed);
    }

    [Fact]
    public async Task Cp8_DamOrNA_AllowsWhenFlagSet_DeniesWhenNoDataAndNoFlag()
    {
        using var db = NewDb();
        var fmNoneNoFlag = Case();
        var fmFlag = Case(damNA: true);
        var sut = new Cp8DamOrNAGuard(db);
        Assert.False((await sut.CheckAsync(Leaving(fmNoneNoFlag, "CP8", "CP9"))).Allowed);
        Assert.True((await sut.CheckAsync(Leaving(fmFlag, "CP8", "CP9"))).Allowed);
    }

    [Fact]
    public async Task Guards_DoNotFireOnInternalCpTransitions()
    {
        // Leaving CP2 only fires when target CP is different. CP2 → CP2_Sub should pass.
        var sut = new Cp2SpatialInfoGuard();
        var sameCp = await sut.CheckAsync(Leaving(Case(), "CP2", "CP2"));
        Assert.True(sameCp.Allowed);
    }

    // ----------------------------------------------------------------------
    // Cp6FieldCropGuard — leaving CP6 requires a FieldAndCrop with SAPWAT > 0
    // ----------------------------------------------------------------------

    private static (Property property, Period period, Crop crop, WaterSource ws) SeedFieldCropPrereqs(ApplicationDBContext db)
    {
        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            PropertyReferenceNumber = "P-Cp6",
            SGCode = "SG-Cp6"
        };
        var period = new Period { PeriodId = Guid.NewGuid(), PeriodName = "Qualifying" };
        var crop = new Crop { CropId = Guid.NewGuid(), CropName = "Maize" };
        var ws = new WaterSource { WaterSourceId = Guid.NewGuid(), WaterSourceName = "River" };
        db.Properties.Add(property);
        db.Periods.Add(period);
        db.Crops.Add(crop);
        db.WaterSources.Add(ws);
        return (property, period, crop, ws);
    }

    [Fact]
    public async Task Cp6_DeniesWhenNoSapwatResult()
    {
        using var db = NewDb();
        var (property, period, crop, ws) = SeedFieldCropPrereqs(db);

        var fm = Case(propertyId: property.PropertyId);
        db.FileMasters.Add(fm);

        db.FieldAndCrops.Add(new FieldAndCrop
        {
            FieldAndCropId = Guid.NewGuid(),
            Property = property,
            PropertyId = property.PropertyId,
            Period = period,
            PeriodId = period.PeriodId,
            FieldArea = 5m,
            Crop = crop,
            WaterSource = ws,
            CropArea = 4m,
            RotationFactor = 0.75m,
            SAPWATCalculationResult = 0m
        });
        await db.SaveChangesAsync();

        var sut = new Cp6FieldCropGuard(db);
        var result = await sut.CheckAsync(Leaving(fm, "CP6", "CP7"));
        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task Cp6_AllowsWhenSapwatResultPresent()
    {
        using var db = NewDb();
        var (property, period, crop, ws) = SeedFieldCropPrereqs(db);

        var fm = Case(propertyId: property.PropertyId);
        db.FileMasters.Add(fm);

        db.FieldAndCrops.Add(new FieldAndCrop
        {
            FieldAndCropId = Guid.NewGuid(),
            Property = property,
            PropertyId = property.PropertyId,
            Period = period,
            PeriodId = period.PeriodId,
            FieldArea = 5m,
            Crop = crop,
            WaterSource = ws,
            CropArea = 4m,
            RotationFactor = 0.75m,
            SAPWATCalculationResult = 500m
        });
        await db.SaveChangesAsync();

        var sut = new Cp6FieldCropGuard(db);
        var result = await sut.CheckAsync(Leaving(fm, "CP6", "CP7"));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task Cp6_PassesWhenNotLeavingCp6()
    {
        using var db = NewDb();
        var fm = Case();
        // No FieldAndCrop seeded — guard must short-circuit on IsLeaving.
        var sut = new Cp6FieldCropGuard(db);
        var result = await sut.CheckAsync(Leaving(fm, "CP5", "CP6"));
        Assert.True(result.Allowed);
    }

    // ----------------------------------------------------------------------
    // Cp7EluGuard — leaving CP7 requires EntitlementId on the FileMaster
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Cp7_DeniesWhenNoEntitlement()
    {
        var sut = new Cp7EluGuard();
        var fm = Case();
        fm.EntitlementId = null;
        var result = await sut.CheckAsync(Leaving(fm, "CP7", "CP8"));
        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task Cp7_AllowsWhenEntitlementLinked()
    {
        var sut = new Cp7EluGuard();
        var fm = Case();
        fm.EntitlementId = Guid.NewGuid();
        var result = await sut.CheckAsync(Leaving(fm, "CP7", "CP8"));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task Cp7_PassesWhenNotLeavingCp7()
    {
        var sut = new Cp7EluGuard();
        var fm = Case();
        fm.EntitlementId = null;
        var result = await sut.CheckAsync(Leaving(fm, "CP6", "CP7"));
        Assert.True(result.Allowed);
    }

    // ----------------------------------------------------------------------
    // CpPrePublicReviewGuard — requires approval timestamp + RegionalManager+
    // ----------------------------------------------------------------------

    private static GuardContext LeavingWithRoles(FileMaster fm, string fromCp, string toCp, IReadOnlyList<string>? roles) =>
        new(fm,
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = $"{fromCp}_Step", DisplayOrder = 1, Phase = "Test" },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = $"{toCp}_Step", DisplayOrder = 2, Phase = "Test" },
            User: null,
            UserRoles: roles);

    [Fact]
    public async Task CpPrePublicReview_DeniesWhenNotApproved()
    {
        var sut = new CpPrePublicReviewGuard();
        var fm = Case();
        fm.PrePublicReviewApprovedAt = null;
        var ctx = LeavingWithRoles(fm, "CP_PrePublicReview", "CP_StakeholderWorkshop",
            new[] { DwsRoles.RegionalManager });
        var result = await sut.CheckAsync(ctx);
        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task CpPrePublicReview_DeniesWhenUserLacksRole()
    {
        var sut = new CpPrePublicReviewGuard();
        var fm = Case();
        fm.PrePublicReviewApprovedAt = DateTime.UtcNow;
        var ctx = LeavingWithRoles(fm, "CP_PrePublicReview", "CP_StakeholderWorkshop",
            new[] { DwsRoles.Validator });
        var result = await sut.CheckAsync(ctx);
        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task CpPrePublicReview_AllowsWhenApprovedByRegionalManager()
    {
        var sut = new CpPrePublicReviewGuard();
        var fm = Case();
        fm.PrePublicReviewApprovedAt = DateTime.UtcNow;
        var ctx = LeavingWithRoles(fm, "CP_PrePublicReview", "CP_StakeholderWorkshop",
            new[] { DwsRoles.RegionalManager });
        var result = await sut.CheckAsync(ctx);
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task CpPrePublicReview_PassesWhenNotLeavingState()
    {
        // Guard only fires when LEAVING CP_PrePublicReview. Entering it should pass.
        var sut = new CpPrePublicReviewGuard();
        var fm = Case();
        fm.PrePublicReviewApprovedAt = null;
        var ctx = LeavingWithRoles(fm, "CP9", "CP_PrePublicReview", new[] { DwsRoles.Validator });
        var result = await sut.CheckAsync(ctx);
        Assert.True(result.Allowed);
    }

    // ----------------------------------------------------------------------
    // CpStakeholderWorkshopGuard — requires workshop date + attendance > 0
    // ----------------------------------------------------------------------

    [Fact]
    public async Task CpStakeholderWorkshop_DeniesWhenNoDates()
    {
        var sut = new CpStakeholderWorkshopGuard();
        var fm = Case();
        fm.StakeholderWorkshopDate = null;
        fm.StakeholderWorkshopAttendance = null;
        var result = await sut.CheckAsync(Leaving(fm, "CP_StakeholderWorkshop", "CP_PublicParticipation"));
        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task CpStakeholderWorkshop_DeniesWhenAttendanceZero()
    {
        var sut = new CpStakeholderWorkshopGuard();
        var fm = Case();
        fm.StakeholderWorkshopDate = DateTime.UtcNow;
        fm.StakeholderWorkshopAttendance = 0;
        var result = await sut.CheckAsync(Leaving(fm, "CP_StakeholderWorkshop", "CP_PublicParticipation"));
        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task CpStakeholderWorkshop_AllowsWhenDateAndAttendanceSet()
    {
        var sut = new CpStakeholderWorkshopGuard();
        var fm = Case();
        fm.StakeholderWorkshopDate = DateTime.UtcNow;
        fm.StakeholderWorkshopAttendance = 5;
        var result = await sut.CheckAsync(Leaving(fm, "CP_StakeholderWorkshop", "CP_PublicParticipation"));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task CpStakeholderWorkshop_PassesWhenNotLeavingState()
    {
        // Guard fires only when LEAVING CP_StakeholderWorkshop. Entering it must pass.
        var sut = new CpStakeholderWorkshopGuard();
        var fm = Case();
        fm.StakeholderWorkshopDate = null;
        fm.StakeholderWorkshopAttendance = null;
        var result = await sut.CheckAsync(Leaving(fm, "CP_PrePublicReview", "CP_StakeholderWorkshop"));
        Assert.True(result.Allowed);
    }
}

// -----------------------------------------------------------------------
// CP11 state seed verification
// -----------------------------------------------------------------------
public class Cp11SeedTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task SeedWorkflowStates_InsertsCP11FileCompiledAt16()
    {
        using var db = NewDb();
        var svc = new SeedDataService(db);
        await svc.SeedAsync();

        var state = await db.WorkflowStates
            .SingleOrDefaultAsync(s => s.StateName == "CP11_FileCompiled");

        Assert.NotNull(state);
        Assert.Equal(16, state!.DisplayOrder);
        Assert.Equal("Verification", state.Phase);
        Assert.False(state.IsTerminal);
    }

    [Fact]
    public async Task SeedWorkflowStates_PrePublicReviewIsAt17AfterCP11Inserted()
    {
        using var db = NewDb();
        var svc = new SeedDataService(db);
        await svc.SeedAsync();

        var state = await db.WorkflowStates
            .SingleOrDefaultAsync(s => s.StateName == "CP_PrePublicReview");

        Assert.NotNull(state);
        Assert.Equal(17, state!.DisplayOrder);
    }

    // BUG-018: River lookup feeds the DamCalculation/Create RiverId dropdown. Without seed
    // rows the dropdown is empty and CP8 cannot be completed.
    [Fact]
    public async Task SeedAsync_SeedsRivers_ForDamCalculationDropdown()
    {
        using var db = NewDb();
        var svc = new SeedDataService(db);
        await svc.SeedAsync();

        var rivers = await db.Rivers.ToListAsync();
        Assert.NotEmpty(rivers);
        Assert.Contains(rivers, r => r.RiverName == "Limpopo");
        Assert.Contains(rivers, r => r.RiverName == "Vaal");
        Assert.Contains(rivers, r => r.RiverName == "Blyde");
    }

    // Seeding must be idempotent — running twice must not duplicate river rows.
    [Fact]
    public async Task SeedAsync_RiverSeed_IsIdempotent()
    {
        using var db = NewDb();
        var svc = new SeedDataService(db);
        await svc.SeedAsync();
        var firstCount = await db.Rivers.CountAsync();

        await svc.SeedAsync();
        var secondCount = await db.Rivers.CountAsync();

        Assert.Equal(firstCount, secondCount);
    }
}

// -----------------------------------------------------------------------
// Cp11FileCompilationGuard
// -----------------------------------------------------------------------
public class Cp11FileCompilationGuardTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // Seeds all 9 items into an existing db. Caller owns db lifetime.
    private static async Task<(FileMaster fm, Guid propId)> SeedFullCase(ApplicationDBContext db)
    {
        var propId   = Guid.NewGuid();
        var property = new Property { PropertyId = propId, PropertyReferenceNumber = "P-CP11", SGCode = "SG-001" };
        db.Properties.Add(property);

        var authType = new AuthorisationType { AuthorisationTypeId = Guid.NewGuid(), AuthorisationTypeName = "Permit" };
        db.AuthorisationTypes.Add(authType);

        var period = new Period { PeriodId = Guid.NewGuid(), PeriodName = "Qualifying" };
        var crop   = new Crop { CropId = Guid.NewGuid(), CropName = "Maize" };
        var ws     = new WaterSource { WaterSourceId = Guid.NewGuid(), WaterSourceName = "River" };
        db.Periods.Add(period); db.Crops.Add(crop); db.WaterSources.Add(ws);

        var fm = new FileMaster
        {
            FileMasterId         = Guid.NewGuid(),
            PropertyId           = propId,
            RegistrationNumber   = "WARMS-001",
            SurveyorGeneralCode  = "SG-001",
            PrimaryCatchment     = "A21",
            QuaternaryCatchment  = "A21A",
            FarmName             = "Testfarm",
            FarmNumber           = 1,
            RegistrationDivision = "TD",
            FarmPortion          = "0",
            WarmsReviewedAt      = DateTime.UtcNow,
            EntitlementId        = Guid.NewGuid(),
            DamMarkedNA          = true,
            SfraMarkedNA         = true
        };
        db.FileMasters.Add(fm);

        db.Authorisations.Add(new Authorisation
        {
            AuthorisationId     = Guid.NewGuid(),
            FileMasterId        = fm.FileMasterId,
            AuthorisationTypeId = authType.AuthorisationTypeId
        });

        db.FieldAndCrops.Add(new FieldAndCrop
        {
            FieldAndCropId          = Guid.NewGuid(),
            PropertyId              = propId,
            Property                = property,
            PeriodId                = period.PeriodId,
            Period                  = period,
            Crop                    = crop,
            WaterSource             = ws,
            FieldArea               = 10m,
            CropArea                = 8m,
            RotationFactor          = 0.8m,
            SAPWATCalculationResult = 600m
        });

        db.Mapbooks.Add(new Mapbook { MapbookId = Guid.NewGuid(), FileMasterId = fm.FileMasterId, MapbookTitle = "Q-Map", MapType = "Qualifying" });
        db.Mapbooks.Add(new Mapbook { MapbookId = Guid.NewGuid(), FileMasterId = fm.FileMasterId, MapbookTitle = "C-Map", MapType = "Current" });

        await db.SaveChangesAsync();
        return (fm, propId);
    }

    private static void AddCp11Docs(ApplicationDBContext db, Guid fileMasterId)
    {
        foreach (var type in new[] { "WARMSReport", "TitleDeedReport", "SGDiagram" })
        {
            db.Documents.Add(new Document
            {
                DocumentId = Guid.NewGuid(),
                FileMasterId = fileMasterId,
                DocumentType = type,
                FileName = "f.pdf",
                BlobPath = "x/f.pdf",
                VirusScanStatus = "Clean",
                SyncStatus = "NotSynced",
                UploadDate = DateTime.UtcNow
            });
        }
    }

    private static GuardContext LeavingCp11(FileMaster fm) =>
        new(fm,
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP11_FileCompiled",  DisplayOrder = 16, Phase = "Verification" },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP_PrePublicReview", DisplayOrder = 17, Phase = "Verification" });

    private static GuardContext NotLeavingCp11(FileMaster fm) =>
        new(fm,
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP9_SFRACalculated", DisplayOrder = 15, Phase = "Verification" },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP11_FileCompiled",  DisplayOrder = 16, Phase = "Verification" });

    [Fact]
    public async Task Cp11_AllowsWhenAllNineItemsPresent()
    {
        using var db = NewDb();
        var (fm, _) = await SeedFullCase(db);
        AddCp11Docs(db, fm.FileMasterId);
        await db.SaveChangesAsync();
        var result = await new Cp11FileCompilationGuard(db).CheckAsync(LeavingCp11(fm));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task Cp11_PassesWhenNotLeavingCp11()
    {
        using var db = NewDb();
        var (fm, _) = await SeedFullCase(db);
        var result = await new Cp11FileCompilationGuard(db).CheckAsync(NotLeavingCp11(fm));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task Cp11_DeniesWhenWarmsReviewMissing()
    {
        using var db = NewDb();
        var (fm, _) = await SeedFullCase(db);
        fm.WarmsReviewedAt = null;
        var result = await new Cp11FileCompilationGuard(db).CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("WARMS review", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenSGCodeMissing()
    {
        using var db = NewDb();
        var (fm, propId) = await SeedFullCase(db);
        var prop = await db.Properties.FindAsync(propId);
        prop!.SGCode = null;
        await db.SaveChangesAsync();
        var result = await new Cp11FileCompilationGuard(db).CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("SG code", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenNoAuthorisationRecord()
    {
        using var db = NewDb();
        var (fm, _) = await SeedFullCase(db);
        db.Authorisations.RemoveRange(db.Authorisations.Where(a => a.FileMasterId == fm.FileMasterId));
        await db.SaveChangesAsync();
        var result = await new Cp11FileCompilationGuard(db).CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("authorisation", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenNoSapwatResult()
    {
        using var db = NewDb();
        var (fm, propId) = await SeedFullCase(db);
        var fc = db.FieldAndCrops.First(f => f.PropertyId == propId);
        fc.SAPWATCalculationResult = 0m;
        await db.SaveChangesAsync();
        var result = await new Cp11FileCompilationGuard(db).CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("SAPWAT", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenNoQualifyingMapbook()
    {
        using var db = NewDb();
        var (fm, _) = await SeedFullCase(db);
        var qm = db.Mapbooks.First(m => m.FileMasterId == fm.FileMasterId && m.MapType == "Qualifying");
        db.Mapbooks.Remove(qm);
        await db.SaveChangesAsync();
        var result = await new Cp11FileCompilationGuard(db).CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("Qualifying", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenEntitlementMissing()
    {
        using var db = NewDb();
        var (fm, _) = await SeedFullCase(db);
        fm.EntitlementId = null;
        var result = await new Cp11FileCompilationGuard(db).CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("Entitlement", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenNoCurrentMapbook()
    {
        using var db = NewDb();
        var (fm, _) = await SeedFullCase(db);
        var cm = db.Mapbooks.First(m => m.FileMasterId == fm.FileMasterId && m.MapType == "Current");
        db.Mapbooks.Remove(cm);
        await db.SaveChangesAsync();
        var result = await new Cp11FileCompilationGuard(db).CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("Current", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenNoDamAndNotMarkedNA()
    {
        using var db = NewDb();
        var (fm, _) = await SeedFullCase(db);
        fm.DamMarkedNA = false;
        var result = await new Cp11FileCompilationGuard(db).CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("Dam", result.Reason);
    }

    [Fact]
    public async Task Cp11_AllowsWhenDamRecordPresentAndFlagNotSet()
    {
        using var db = NewDb();
        var (fm, propId) = await SeedFullCase(db);
        fm.DamMarkedNA  = false;
        fm.SfraMarkedNA = true;

        var property = await db.Properties.FindAsync(propId);
        var river    = new River { RiverId = Guid.NewGuid(), RiverName = "Test River" };
        db.Rivers.Add(river);
        db.DamCalculations.Add(new DamCalculation
        {
            DamCalculationId  = Guid.NewGuid(),
            PropertyId        = propId,
            Property          = property!,
            SateliteSurveyDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DamCapacity       = 1000m,
            RiverId           = river.RiverId,
            River             = river
        });
        AddCp11Docs(db, fm.FileMasterId);
        await db.SaveChangesAsync();

        var result = await new Cp11FileCompilationGuard(db).CheckAsync(LeavingCp11(fm));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task Cp11_DeniesWhenNoSfraAndNotMarkedNA()
    {
        using var db = NewDb();
        var (fm, _) = await SeedFullCase(db);
        fm.SfraMarkedNA = false;
        var result = await new Cp11FileCompilationGuard(db).CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("SFRA", result.Reason);
    }

    [Fact]
    public async Task Cp11_AllowsWhenSfraRecordPresentAndFlagNotSet()
    {
        using var db = NewDb();
        var (fm, propId) = await SeedFullCase(db);
        fm.DamMarkedNA  = true;
        fm.SfraMarkedNA = false;

        var property = await db.Properties.FindAsync(propId);
        db.Forestations.Add(new Forestation
        {
            ForestationId     = Guid.NewGuid(),
            PropertyId        = propId,
            Property          = property!,
            RegisteredHectares = 15m,
            RegisteredVolume  = 500m
        });
        AddCp11Docs(db, fm.FileMasterId);
        await db.SaveChangesAsync();

        var result = await new Cp11FileCompilationGuard(db).CheckAsync(LeavingCp11(fm));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task Cp11_DeniesWhenMandatoryDocumentsMissing()
    {
        using var db = NewDb();
        var (fm, _) = await SeedFullCase(db); // seeds data records but NOT documents
        var result = await new Cp11FileCompilationGuard(db).CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("report", result.Reason!); // e.g. "WARMS report" / "Title Deed report"
    }

    [Fact]
    public async Task Cp11_AllowsWhenAllDataAndDocumentsPresent()
    {
        using var db = NewDb();
        var (fm, _) = await SeedFullCase(db);
        AddCp11Docs(db, fm.FileMasterId);
        await db.SaveChangesAsync();
        var result = await new Cp11FileCompilationGuard(db).CheckAsync(LeavingCp11(fm));
        Assert.True(result.Allowed);
    }
}

// -----------------------------------------------------------------------
// Cp19PajaChecklistGuard
// -----------------------------------------------------------------------
public class Cp19PajaChecklistGuardTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static FileMaster MinimalCase() => new FileMaster
    {
        FileMasterId         = Guid.NewGuid(),
        PropertyId           = Guid.NewGuid(),
        RegistrationNumber   = "WARMS-PAJA",
        SurveyorGeneralCode  = "SG-PAJA",
        PrimaryCatchment     = "A21",
        QuaternaryCatchment  = "A21A",
        FarmName             = "PAJAFarm",
        FarmNumber           = 1,
        RegistrationDivision = "TD",
        FarmPortion          = "0"
    };

    private static GuardContext TargetingLetter3(FileMaster fm) =>
        new(fm,
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_ELUConfirmed",   DisplayOrder = 30, Phase = "Verification" },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_Letter3Issued",  DisplayOrder = 29, Phase = "Verification" });

    private static GuardContext NotTargetingLetter3(FileMaster fm) =>
        new(fm,
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP_PrePublicReview",    DisplayOrder = 17, Phase = "Verification" },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP_StakeholderWorkshop", DisplayOrder = 18, Phase = "Verification" });

    [Fact]
    public async Task Cp19_DeniesWhenNoChecklistExists()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        await db.SaveChangesAsync();

        var sut = new Cp19PajaChecklistGuard(db);
        var result = await sut.CheckAsync(TargetingLetter3(fm));
        Assert.False(result.Allowed);
        Assert.Contains("PAJA checklist", result.Reason);
    }

    [Fact]
    public async Task Cp19_DeniesWhenChecklistExistsButIncomplete()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        db.PAJAChecklists.Add(new PAJAChecklist
        {
            PAJAChecklistId       = Guid.NewGuid(),
            FileMasterId          = fm.FileMasterId,
            FactualBasis          = "Present",
            LegalBasis            = "Present",
            UserInputConsideration = "Present",
            FinalReasoning        = null,   // missing — IsComplete == false
            CompletedAt           = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new Cp19PajaChecklistGuard(db);
        var result = await sut.CheckAsync(TargetingLetter3(fm));
        Assert.False(result.Allowed);
        Assert.Contains("incomplete", result.Reason);
    }

    [Fact]
    public async Task Cp19_AllowsWhenChecklistComplete()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        db.PAJAChecklists.Add(new PAJAChecklist
        {
            PAJAChecklistId        = Guid.NewGuid(),
            FileMasterId           = fm.FileMasterId,
            FactualBasis           = "The water use existed during the qualifying period.",
            LegalBasis             = "Authorised by riparian right under the old Water Act.",
            UserInputConsideration = "User confirmed use in writing.",
            FinalReasoning         = "Use is lawful — ELU confirmed.",
            CompletedAt            = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new Cp19PajaChecklistGuard(db);
        var result = await sut.CheckAsync(TargetingLetter3(fm));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task Cp19_PassesWhenNotTargetingLetter3()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        // No PAJAChecklist — guard must short-circuit.
        var sut = new Cp19PajaChecklistGuard(db);
        var result = await sut.CheckAsync(NotTargetingLetter3(fm));
        Assert.True(result.Allowed);
    }
}

// -----------------------------------------------------------------------
// LetterServiceConfirmedGuard
// -----------------------------------------------------------------------
public class LetterServiceConfirmedGuardTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static FileMaster MinimalCase() => new FileMaster
    {
        FileMasterId         = Guid.NewGuid(),
        PropertyId           = Guid.NewGuid(),
        RegistrationNumber   = "WARMS-LSC",
        SurveyorGeneralCode  = "SG-LSC",
        PrimaryCatchment     = "A21",
        QuaternaryCatchment  = "A21A",
        FarmName             = "LSCFarm",
        FarmNumber           = 2,
        RegistrationDivision = "TD",
        FarmPortion          = "0"
    };

    private static LetterType LetterTypeFor(string code) => new LetterType
    {
        LetterTypeId      = Guid.NewGuid(),
        LetterName        = code,
        LetterDescription = code,
        NWASection        = "S35"
    };

    private static LetterIssuance IssuanceFor(Guid fileMasterId, LetterType lt, DateOnly? confirmedDate) =>
        new LetterIssuance
        {
            LetterIssuanceId      = Guid.NewGuid(),
            FileMasterId          = fileMasterId,
            LetterTypeId          = lt.LetterTypeId,
            LetterType            = lt,
            IssuedDate            = DateOnly.FromDateTime(DateTime.UtcNow),
            ServiceConfirmedDate  = confirmedDate
        };

    private static GuardContext LeavingIssuedState(FileMaster fm, string issuedState, string nextState) =>
        new(fm,
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = issuedState, DisplayOrder = 1, Phase = "Verification" },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = nextState,   DisplayOrder = 2, Phase = "Verification" });

    // Letter 1 — deny without confirmed date
    [Fact]
    public async Task Letter1_DeniesWhenServiceNotConfirmed()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        var lt = LetterTypeFor("S35_L1");
        db.LetterTypes.Add(lt);
        db.LetterIssuances.Add(IssuanceFor(fm.FileMasterId, lt, confirmedDate: null));
        await db.SaveChangesAsync();

        var sut = new LetterServiceConfirmedGuard(db);
        var result = await sut.CheckAsync(LeavingIssuedState(fm, "S35_Letter1Issued", "S35_Letter1Responded"));
        Assert.False(result.Allowed);
        Assert.Contains("Letter 1", result.Reason);
    }

    // Letter 1 — allow when confirmed
    [Fact]
    public async Task Letter1_AllowsWhenServiceConfirmed()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        var lt = LetterTypeFor("S35_L1");
        db.LetterTypes.Add(lt);
        db.LetterIssuances.Add(IssuanceFor(fm.FileMasterId, lt, confirmedDate: DateOnly.FromDateTime(DateTime.UtcNow)));
        await db.SaveChangesAsync();

        var sut = new LetterServiceConfirmedGuard(db);
        var result = await sut.CheckAsync(LeavingIssuedState(fm, "S35_Letter1Issued", "S35_Letter1Responded"));
        Assert.True(result.Allowed);
    }

    // Letter 1A — deny without confirmed date
    [Fact]
    public async Task Letter1A_DeniesWhenServiceNotConfirmed()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        var lt = LetterTypeFor("S35_L1A");
        db.LetterTypes.Add(lt);
        db.LetterIssuances.Add(IssuanceFor(fm.FileMasterId, lt, confirmedDate: null));
        await db.SaveChangesAsync();

        var sut = new LetterServiceConfirmedGuard(db);
        var result = await sut.CheckAsync(LeavingIssuedState(fm, "S35_Letter1AIssued", "S35_Letter1AResponded"));
        Assert.False(result.Allowed);
        Assert.Contains("Letter 1A", result.Reason);
    }

    // Letter 1A — allow when confirmed
    [Fact]
    public async Task Letter1A_AllowsWhenServiceConfirmed()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        var lt = LetterTypeFor("S35_L1A");
        db.LetterTypes.Add(lt);
        db.LetterIssuances.Add(IssuanceFor(fm.FileMasterId, lt, confirmedDate: DateOnly.FromDateTime(DateTime.UtcNow)));
        await db.SaveChangesAsync();

        var sut = new LetterServiceConfirmedGuard(db);
        var result = await sut.CheckAsync(LeavingIssuedState(fm, "S35_Letter1AIssued", "S35_Letter1AResponded"));
        Assert.True(result.Allowed);
    }

    // Letter 2 — deny without confirmed date
    [Fact]
    public async Task Letter2_DeniesWhenServiceNotConfirmed()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        var lt = LetterTypeFor("S35_L2");
        db.LetterTypes.Add(lt);
        db.LetterIssuances.Add(IssuanceFor(fm.FileMasterId, lt, confirmedDate: null));
        await db.SaveChangesAsync();

        var sut = new LetterServiceConfirmedGuard(db);
        var result = await sut.CheckAsync(LeavingIssuedState(fm, "S35_Letter2Issued", "S35_Letter2Responded"));
        Assert.False(result.Allowed);
        Assert.Contains("Letter 2", result.Reason);
    }

    // Letter 2 — allow when confirmed
    [Fact]
    public async Task Letter2_AllowsWhenServiceConfirmed()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        var lt = LetterTypeFor("S35_L2");
        db.LetterTypes.Add(lt);
        db.LetterIssuances.Add(IssuanceFor(fm.FileMasterId, lt, confirmedDate: DateOnly.FromDateTime(DateTime.UtcNow)));
        await db.SaveChangesAsync();

        var sut = new LetterServiceConfirmedGuard(db);
        var result = await sut.CheckAsync(LeavingIssuedState(fm, "S35_Letter2Issued", "S35_Letter2Responded"));
        Assert.True(result.Allowed);
    }

    // Letter 2A — deny without confirmed date
    [Fact]
    public async Task Letter2A_DeniesWhenServiceNotConfirmed()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        var lt = LetterTypeFor("S35_L2A");
        db.LetterTypes.Add(lt);
        db.LetterIssuances.Add(IssuanceFor(fm.FileMasterId, lt, confirmedDate: null));
        await db.SaveChangesAsync();

        var sut = new LetterServiceConfirmedGuard(db);
        var result = await sut.CheckAsync(LeavingIssuedState(fm, "S35_Letter2AIssued", "S35_Letter3Issued"));
        Assert.False(result.Allowed);
        Assert.Contains("Letter 2A", result.Reason);
    }

    // Letter 2A — allow when confirmed
    [Fact]
    public async Task Letter2A_AllowsWhenServiceConfirmed()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        var lt = LetterTypeFor("S35_L2A");
        db.LetterTypes.Add(lt);
        db.LetterIssuances.Add(IssuanceFor(fm.FileMasterId, lt, confirmedDate: DateOnly.FromDateTime(DateTime.UtcNow)));
        await db.SaveChangesAsync();

        var sut = new LetterServiceConfirmedGuard(db);
        var result = await sut.CheckAsync(LeavingIssuedState(fm, "S35_Letter2AIssued", "S35_Letter3Issued"));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task LetterService_PassesWhenNotInAnyIssuedState()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        // No LetterIssuance seeded — guard must short-circuit.
        var sut = new LetterServiceConfirmedGuard(db);
        var result = await sut.CheckAsync(LeavingIssuedState(fm, "CP_StakeholderWorkshop", "S35_Letter1Issued"));
        Assert.True(result.Allowed);
    }
}
