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

    // Builds the minimal FileMaster + Property required to compile a full file.
    // Returns (db, fm, propertyId) with all 9 items seeded.
    private static async Task<(ApplicationDBContext db, FileMaster fm, Guid propId)> FullyCompiledCase()
    {
        var db = NewDb();
        var propId = Guid.NewGuid();
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
            FileMasterId       = Guid.NewGuid(),
            PropertyId         = propId,
            RegistrationNumber = "WARMS-001",
            SurveyorGeneralCode = "SG-001",
            PrimaryCatchment   = "A21",
            QuaternaryCatchment = "A21A",
            FarmName           = "Testfarm",
            FarmNumber         = 1,
            RegistrationDivision = "TD",
            FarmPortion        = "0",
            WarmsReviewedAt    = DateTime.UtcNow,
            EntitlementId      = Guid.NewGuid(),
            DamMarkedNA        = true,
            SfraMarkedNA       = true
        };
        db.FileMasters.Add(fm);

        db.Authorisations.Add(new Authorisation
        {
            AuthorisationId = Guid.NewGuid(),
            FileMasterId = fm.FileMasterId,
            AuthorisationTypeId = authType.AuthorisationTypeId
        });

        db.FieldAndCrops.Add(new FieldAndCrop
        {
            FieldAndCropId = Guid.NewGuid(),
            PropertyId = propId, Property = property,
            PeriodId = period.PeriodId, Period = period,
            Crop = crop,
            WaterSource = ws,
            FieldArea = 10m, CropArea = 8m, RotationFactor = 0.8m,
            SAPWATCalculationResult = 600m
        });

        db.Mapbooks.Add(new Mapbook { MapbookId = Guid.NewGuid(), FileMasterId = fm.FileMasterId, MapbookTitle = "Q-Map", MapType = "Qualifying" });
        db.Mapbooks.Add(new Mapbook { MapbookId = Guid.NewGuid(), FileMasterId = fm.FileMasterId, MapbookTitle = "C-Map", MapType = "Current" });

        await db.SaveChangesAsync();
        return (db, fm, propId);
    }

    private static GuardContext LeavingCp11(FileMaster fm) =>
        new(fm,
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP11_FileCompiled", DisplayOrder = 16, Phase = "Verification" },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP_PrePublicReview", DisplayOrder = 17, Phase = "Verification" });

    private static GuardContext NotLeavingCp11(FileMaster fm) =>
        new(fm,
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP9_SFRACalculated", DisplayOrder = 15, Phase = "Verification" },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP11_FileCompiled", DisplayOrder = 16, Phase = "Verification" });

    [Fact]
    public async Task Cp11_AllowsWhenAllNineItemsPresent()
    {
        var (db, fm, _) = await FullyCompiledCase();
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task Cp11_PassesWhenNotLeavingCp11()
    {
        var (db, fm, _) = await FullyCompiledCase();
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(NotLeavingCp11(fm));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task Cp11_DeniesWhenWarmsReviewMissing()
    {
        var (db, fm, _) = await FullyCompiledCase();
        fm.WarmsReviewedAt = null;
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("WARMS review", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenSGCodeMissing()
    {
        var (db, fm, propId) = await FullyCompiledCase();
        var prop = await db.Properties.FindAsync(propId);
        prop!.SGCode = null;
        await db.SaveChangesAsync();
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("SG code", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenNoAuthorisationRecord()
    {
        var (db, fm, _) = await FullyCompiledCase();
        db.Authorisations.RemoveRange(db.Authorisations.Where(a => a.FileMasterId == fm.FileMasterId));
        await db.SaveChangesAsync();
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("authorisation", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenNoSapwatResult()
    {
        var (db, fm, propId) = await FullyCompiledCase();
        var fc = db.FieldAndCrops.First(f => f.PropertyId == propId);
        fc.SAPWATCalculationResult = 0m;
        await db.SaveChangesAsync();
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("SAPWAT", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenNoQualifyingMapbook()
    {
        var (db, fm, _) = await FullyCompiledCase();
        var qm = db.Mapbooks.First(m => m.FileMasterId == fm.FileMasterId && m.MapType == "Qualifying");
        db.Mapbooks.Remove(qm);
        await db.SaveChangesAsync();
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("Qualifying", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenEntitlementMissing()
    {
        var (db, fm, _) = await FullyCompiledCase();
        fm.EntitlementId = null;
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("Entitlement", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenNoCurrentMapbook()
    {
        var (db, fm, _) = await FullyCompiledCase();
        var cm = db.Mapbooks.First(m => m.FileMasterId == fm.FileMasterId && m.MapType == "Current");
        db.Mapbooks.Remove(cm);
        await db.SaveChangesAsync();
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("Current", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenNoDamAndNotMarkedNA()
    {
        var (db, fm, _) = await FullyCompiledCase();
        fm.DamMarkedNA = false;
        // No DamCalculation seeded
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("Dam", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenNoSfraAndNotMarkedNA()
    {
        var (db, fm, _) = await FullyCompiledCase();
        fm.SfraMarkedNA = false;
        // No Forestation seeded
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("SFRA", result.Reason);
    }
}
