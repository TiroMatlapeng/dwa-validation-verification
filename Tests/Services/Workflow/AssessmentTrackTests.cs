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
    public async Task AdvanceAsync_OnS33_2Track_SkipsCp5_LandsOnReadyForDeclaration()
    {
        using var db = NewDb();

        // Seed states: CP4 (current), CP5 (would-be-next, triggers skip), S33_2_ReadyForDeclaration (skip target).
        var cp4   = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP4_AdditionalInfo",         DisplayOrder = 10, Phase = "Validation",   IsTerminal = false };
        var cp5   = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP5_GISAnalysis",            DisplayOrder = 11, Phase = "Validation",   IsTerminal = false };
        var ready = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S33_2_ReadyForDeclaration",  DisplayOrder = 33, Phase = "Verification", IsTerminal = false };
        db.WorkflowStates.AddRange(cp4, cp5, ready);

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

        Assert.Equal(ready.WorkflowStateId, result.CurrentWorkflowStateId);
        Assert.Equal("Active", result.Status);  // non-terminal → still Active
    }

    [Fact]
    public async Task AdvanceAsync_OnS35Track_FollowsDefaultOrder_DoesNotSkip()
    {
        using var db = NewDb();
        var cp4 = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP4_AdditionalInfo", DisplayOrder = 4, Phase = "Validation" };
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

    [Fact]
    public async Task AdvanceAsync_FromS33_2ReadyForDeclaration_ThrowsInvalidOperation()
    {
        // Direct workflow advance from S33_2_ReadyForDeclaration is blocked — the declaration
        // letter must be issued via IssueLetter, which calls TransitionToAsync instead.
        using var db = NewDb();

        var ready = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S33_2_ReadyForDeclaration", DisplayOrder = 33, Phase = "Verification", IsTerminal = false };
        var decl  = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S33_2_DeclarationIssued",  DisplayOrder = 34, Phase = "Verification", IsTerminal = false };
        db.WorkflowStates.AddRange(ready, decl);

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
            AssessmentTrack = "S33_2_Declaration"
        };
        db.FileMasters.Add(fm);

        var instance = new WorkflowInstance
        {
            WorkflowInstanceId = Guid.NewGuid(),
            FileMasterId = fm.FileMasterId,
            CurrentWorkflowStateId = ready.WorkflowStateId,
            Status = "Active",
            CreatedDate = DateTime.UtcNow
        };
        db.WorkflowInstances.Add(instance);
        fm.WorkflowInstanceId = instance.WorkflowInstanceId;
        await db.SaveChangesAsync();

        var svc = new WorkflowService(db, Array.Empty<ITransitionGuard>(), new TestAuditService());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AdvanceAsync(fm.FileMasterId, userId: null, notes: null));

        Assert.Contains("S33_2_ReadyForDeclaration", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdvanceAsync_OnS33_2Track_MissingSkipTargetState_ThrowsInvalidOperation()
    {
        // If S33_2_ReadyForDeclaration is absent from the DB (seed not run), the service must
        // throw rather than silently fall through to the default next state.
        using var db = NewDb();

        var cp4 = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP4_AdditionalInfo", DisplayOrder = 10, Phase = "Validation", IsTerminal = false };
        var cp5 = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP5_GISAnalysis",    DisplayOrder = 11, Phase = "Validation", IsTerminal = false };
        // S33_2_ReadyForDeclaration intentionally NOT seeded.
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
            AssessmentTrack = "S33_2_Declaration",
            AdditionalInfoReviewedAt = DateTime.UtcNow
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
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AdvanceAsync(fm.FileMasterId, userId: null, notes: "S33(2) skip should throw"));

        Assert.Contains("S33_2_ReadyForDeclaration", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SeedWorkflowStatesAsync", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
