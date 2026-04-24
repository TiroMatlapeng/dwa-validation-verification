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
}
