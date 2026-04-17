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
        var svc = new WorkflowService(ctx);

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
        var svc = new WorkflowService(ctx);
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
        var svc = new WorkflowService(ctx);
        await svc.StartWorkflowAsync(fmId);
        await svc.AdvanceAsync(fmId, null, null); // -> CP1_B
        await svc.AdvanceAsync(fmId, null, null); // -> Closed (terminal)

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AdvanceAsync(fmId, null, null));
    }

    [Fact]
    public async Task TransitionToAsync_moves_to_named_state_and_records_step()
    {
        var (ctx, fmId, _) = await SetupAsync();
        var svc = new WorkflowService(ctx);
        await svc.StartWorkflowAsync(fmId);

        var instance = await svc.TransitionToAsync(fmId, "Closed", userId: null, notes: "demo");

        var closed = await ctx.WorkflowStates.SingleAsync(s => s.StateName == "Closed");
        Assert.Equal(closed.WorkflowStateId, instance.CurrentWorkflowStateId);
    }
}
