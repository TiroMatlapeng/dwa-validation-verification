using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Tests.Services;

public class WorkflowServiceTests
{
    private static async Task<(ApplicationDBContext ctx, Guid fileMasterId, Guid firstStateId)> SetupAsync()
    {
        var ctx = TestDbContextFactory.Create();

        var firstState = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP1_A", Phase = "Inception", DisplayOrder = 1, IsTerminal = false };
        var secondState = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP1_B", Phase = "Inception", DisplayOrder = 2, IsTerminal = false };
        var terminal = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "Closed", Phase = "Verification", DisplayOrder = 3, IsTerminal = true };
        ctx.WorkflowStates.AddRange(firstState, secondState, terminal);

        var property = new Property { PropertyId = Guid.NewGuid(), PropertyReferenceNumber = "P1", SGCode = "SG1" };
        ctx.Properties.Add(property);

        var fileMaster = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            RegistrationNumber = "WARMS1",
            PropertyId = property.PropertyId,
            SurveyorGeneralCode = "SG1",
            PrimaryCatchment = "A",
            QuaternaryCatchment = "A21A",
            FarmName = "Doornhoek",
            FarmNumber = 1,
            RegistrationDivision = "JR",
            FarmPortion = "0",
            FileCreatedDate = DateOnly.FromDateTime(DateTime.Today),
        };
        ctx.FileMasters.Add(fileMaster);
        await ctx.SaveChangesAsync();

        return (ctx, fileMaster.FileMasterId, firstState.WorkflowStateId);
    }

    [Fact]
    public async Task StartWorkflowAsync_creates_instance_at_first_state_and_first_step_record()
    {
        var (ctx, fmId, firstStateId) = await SetupAsync();
        var svc = new WorkflowService(ctx, Array.Empty<dwa_ver_val.Services.Workflow.ITransitionGuard>(), new TestAuditService());

        var instance = await svc.StartWorkflowAsync(fmId);

        Assert.Equal(firstStateId, instance.CurrentWorkflowStateId);
        Assert.Equal("Active", instance.Status);
        var step = await ctx.WorkflowStepRecords.SingleAsync(s => s.WorkflowInstanceId == instance.WorkflowInstanceId);
        Assert.Equal("InProgress", step.StepStatus);
        Assert.Equal(firstStateId, step.WorkflowStateId);
    }

    [Fact]
    public async Task AdvanceAsync_moves_to_next_state_and_completes_previous_step()
    {
        var (ctx, fmId, _) = await SetupAsync();
        var svc = new WorkflowService(ctx, Array.Empty<dwa_ver_val.Services.Workflow.ITransitionGuard>(), new TestAuditService());
        await svc.StartWorkflowAsync(fmId);

        var instance = await svc.AdvanceAsync(fmId, userId: null, notes: null);

        var next = await ctx.WorkflowStates.SingleAsync(s => s.StateName == "CP1_B");
        Assert.Equal(next.WorkflowStateId, instance.CurrentWorkflowStateId);

        var steps = await ctx.WorkflowStepRecords.Where(s => s.WorkflowInstanceId == instance.WorkflowInstanceId).OrderBy(s => s.StartedDate).ToListAsync();
        Assert.Equal(2, steps.Count);
        Assert.Equal("Completed", steps[0].StepStatus);
        Assert.NotNull(steps[0].CompletedDate);
        Assert.Equal("InProgress", steps[1].StepStatus);
    }

    [Fact]
    public async Task AdvanceAsync_at_terminal_state_throws()
    {
        var (ctx, fmId, _) = await SetupAsync();
        var svc = new WorkflowService(ctx, Array.Empty<dwa_ver_val.Services.Workflow.ITransitionGuard>(), new TestAuditService());
        await svc.StartWorkflowAsync(fmId);
        await svc.AdvanceAsync(fmId, null, null); // -> CP1_B
        await svc.AdvanceAsync(fmId, null, null); // -> Closed (terminal)

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AdvanceAsync(fmId, null, null));
    }

    [Fact]
    public async Task TransitionToAsync_moves_to_named_state_and_records_step()
    {
        var (ctx, fmId, _) = await SetupAsync();
        var svc = new WorkflowService(ctx, Array.Empty<dwa_ver_val.Services.Workflow.ITransitionGuard>(), new TestAuditService());
        await svc.StartWorkflowAsync(fmId);

        var instance = await svc.TransitionToAsync(fmId, "Closed", userId: null, notes: "demo");

        var closed = await ctx.WorkflowStates.SingleAsync(s => s.StateName == "Closed");
        Assert.Equal(closed.WorkflowStateId, instance.CurrentWorkflowStateId);
    }

    // ── BUG-014: CanIssueLetterAsync prerequisite-state guard ──────────────

    private static async Task<(ApplicationDBContext ctx, WorkflowService svc, Guid fmId)> SetupLetterStatesAsync(string currentStateName)
    {
        var ctx = TestDbContextFactory.Create();

        // Seed the workflow states relevant to the letter prerequisite map.
        var states = new[]
        {
            ("CP9_SFRACalculated", 15),
            ("S35_Letter1Issued", 19),
            ("S35_Letter1Responded", 20),
            ("S33_2_ReadyForDeclaration", 34),
        };
        WorkflowState? current = null;
        foreach (var (name, order) in states)
        {
            var s = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = name, Phase = "Verification", DisplayOrder = order, IsTerminal = false };
            ctx.WorkflowStates.Add(s);
            if (name == currentStateName) current = s;
        }

        var property = new Property { PropertyId = Guid.NewGuid(), PropertyReferenceNumber = "P1", SGCode = "SG1" };
        ctx.Properties.Add(property);
        var fm = new FileMaster
        {
            FileMasterId = Guid.NewGuid(), RegistrationNumber = "WARMS1", PropertyId = property.PropertyId,
            SurveyorGeneralCode = "SG1", PrimaryCatchment = "A", QuaternaryCatchment = "A21A",
            FarmName = "Doornhoek", FarmNumber = 1, RegistrationDivision = "JR", FarmPortion = "0",
            FileCreatedDate = DateOnly.FromDateTime(DateTime.Today),
        };
        ctx.FileMasters.Add(fm);
        await ctx.SaveChangesAsync();

        var instance = new WorkflowInstance
        {
            WorkflowInstanceId = Guid.NewGuid(), FileMasterId = fm.FileMasterId,
            CurrentWorkflowStateId = current!.WorkflowStateId, Status = "Active", CreatedDate = DateTime.UtcNow,
        };
        ctx.WorkflowInstances.Add(instance);
        fm.WorkflowInstanceId = instance.WorkflowInstanceId;
        await ctx.SaveChangesAsync();

        var svc = new WorkflowService(ctx, Array.Empty<dwa_ver_val.Services.Workflow.ITransitionGuard>(), new TestAuditService());
        return (ctx, svc, fm.FileMasterId);
    }

    [Theory]
    [InlineData("CP9_SFRACalculated", "S35_L1")]
    [InlineData("S35_Letter1Issued", "S35_L1A")]
    [InlineData("S35_Letter1Responded", "S35_L3")]
    [InlineData("S33_2_ReadyForDeclaration", "S33_2_Decl")]
    public async Task CanIssueLetterAsync_allows_when_current_state_is_a_valid_prerequisite(string currentState, string letterCode)
    {
        var (_, svc, fmId) = await SetupLetterStatesAsync(currentState);

        var result = await svc.CanIssueLetterAsync(fmId, letterCode);

        Assert.True(result.Allowed);
        Assert.Null(result.Reason);
    }

    [Theory]
    // S35_L1 must NOT be issuable from an early/mid-process state (e.g. still at CP9 is OK, but
    // from Letter1Issued it would orphan a second Letter 1).
    [InlineData("S35_Letter1Issued", "S35_L1")]
    // S33(2) declaration must NOT be issuable from a generic CP state.
    [InlineData("CP9_SFRACalculated", "S33_2_Decl")]
    // S35 Letter 3 must NOT be issuable straight from CP9 (needs a response first).
    [InlineData("CP9_SFRACalculated", "S35_L3")]
    public async Task CanIssueLetterAsync_denies_when_current_state_is_not_a_prerequisite(string currentState, string letterCode)
    {
        var (_, svc, fmId) = await SetupLetterStatesAsync(currentState);

        var result = await svc.CanIssueLetterAsync(fmId, letterCode);

        Assert.False(result.Allowed);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public async Task CanIssueLetterAsync_denies_unknown_letter_code()
    {
        var (_, svc, fmId) = await SetupLetterStatesAsync("CP9_SFRACalculated");

        var result = await svc.CanIssueLetterAsync(fmId, "NOT_A_REAL_CODE");

        Assert.False(result.Allowed);
        Assert.NotNull(result.Reason);
    }
}
