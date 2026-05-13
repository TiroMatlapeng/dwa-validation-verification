using dwa_ver_val.Services.Workflow;
using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace dwa_ver_val.Tests.Services.Workflow;

public class AssessmentTrackTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task AdvanceAsync_OnS33_2Track_SkipsCp5_LandsOnDeclarationIssued()
    {
        using var db = NewDb();

        // Seed states: CP4 (leaving), CP5 (would-be-next), S33_2_DeclarationIssued.
        var cp4 = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP4_Additional", DisplayOrder = 4, Phase = "Validation" };
        var cp5 = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP5_Mapbook", DisplayOrder = 5, Phase = "Validation" };
        var decl = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S33_2_DeclarationIssued", DisplayOrder = 100, Phase = "Declaration", IsTerminal = true };
        db.WorkflowStates.AddRange(cp4, cp5, decl);

        var fm = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            RegistrationNumber = "N/A",
            SurveyorGeneralCode = "N/A",
            PrimaryCatchment = "N/A",
            QuaternaryCatchment = "N/A",
            FarmName = "N/A",
            FarmNumber = 0,
            RegistrationDivision = "N/A",
            FarmPortion = "N/A",
            AssessmentTrack = "S33_2_Declaration",
            AdditionalInfoReviewedAt = DateTime.UtcNow  // allow CP4 guard to pass
        };
        db.FileMasters.Add(fm);

        var instance = new WorkflowInstance
        {
            WorkflowInstanceId = Guid.NewGuid(),
            FileMasterId = fm.FileMasterId,
            CurrentWorkflowStateId = cp4.WorkflowStateId,
            Status = "Active",
            CreatedDate = DateTime.UtcNow
        };
        db.WorkflowInstances.Add(instance);
        fm.WorkflowInstanceId = instance.WorkflowInstanceId;
        await db.SaveChangesAsync();

        var svc = new WorkflowService(db, Array.Empty<ITransitionGuard>(), new TestAuditService());
        var result = await svc.AdvanceAsync(fm.FileMasterId, userId: null, notes: "Kader Asmal — skipping validation CPs");

        Assert.Equal(decl.WorkflowStateId, result.CurrentWorkflowStateId);
        Assert.Equal("Completed", result.Status);
    }

    [Fact]
    public async Task AdvanceAsync_OnS35Track_FollowsDefaultOrder_DoesNotSkip()
    {
        using var db = NewDb();
        var cp4 = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP4_Additional", DisplayOrder = 4, Phase = "Validation" };
        var cp5 = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP5_Mapbook", DisplayOrder = 5, Phase = "Validation" };
        db.WorkflowStates.AddRange(cp4, cp5);

        var fm = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            RegistrationNumber = "N/A",
            SurveyorGeneralCode = "N/A",
            PrimaryCatchment = "N/A",
            QuaternaryCatchment = "N/A",
            FarmName = "N/A",
            FarmNumber = 0,
            RegistrationDivision = "N/A",
            FarmPortion = "N/A",
            AssessmentTrack = "S35_Verification"
        };
        db.FileMasters.Add(fm);
        var instance = new WorkflowInstance
        {
            WorkflowInstanceId = Guid.NewGuid(),
            FileMasterId = fm.FileMasterId,
            CurrentWorkflowStateId = cp4.WorkflowStateId,
            Status = "Active",
            CreatedDate = DateTime.UtcNow
        };
        db.WorkflowInstances.Add(instance);
        fm.WorkflowInstanceId = instance.WorkflowInstanceId;
        await db.SaveChangesAsync();

        var svc = new WorkflowService(db, Array.Empty<ITransitionGuard>(), new TestAuditService());
        var result = await svc.AdvanceAsync(fm.FileMasterId, userId: null, notes: null);

        Assert.Equal(cp5.WorkflowStateId, result.CurrentWorkflowStateId);
    }
}
