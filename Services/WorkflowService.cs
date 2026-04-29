using dwa_ver_val.Services.Audit;
using dwa_ver_val.Services.Workflow;
using Microsoft.EntityFrameworkCore;

public class WorkflowService : IWorkflowService
{
    private const string S33_2_TerminalStateName = "S33_2_DeclarationIssued";
    private static readonly string[] CpsSkippedOnS33_2 = { "CP5", "CP6", "CP7", "CP8", "CP9" };

    private readonly ApplicationDBContext _context;
    private readonly IEnumerable<ITransitionGuard> _guards;
    private readonly IAuditService _audit;

    public WorkflowService(
        ApplicationDBContext context,
        IEnumerable<ITransitionGuard> guards,
        IAuditService audit)
    {
        _context = context;
        _guards = guards;
        _audit = audit;
    }

    public async Task<WorkflowInstance> StartWorkflowAsync(Guid fileMasterId)
    {
        var fileMaster = await _context.FileMasters.FindAsync(fileMasterId)
            ?? throw new InvalidOperationException($"FileMaster {fileMasterId} not found.");

        if (fileMaster.WorkflowInstanceId.HasValue)
            throw new InvalidOperationException("Workflow already started for this case.");

        var firstState = await _context.WorkflowStates
            .OrderBy(s => s.DisplayOrder)
            .FirstAsync();

        var instance = new WorkflowInstance
        {
            WorkflowInstanceId = Guid.NewGuid(),
            FileMasterId = fileMasterId,
            CurrentWorkflowStateId = firstState.WorkflowStateId,
            Status = "Active",
            CreatedDate = DateTime.UtcNow,
        };
        _context.WorkflowInstances.Add(instance);

        _context.WorkflowStepRecords.Add(new WorkflowStepRecord
        {
            WorkflowStepRecordId = Guid.NewGuid(),
            WorkflowInstanceId = instance.WorkflowInstanceId,
            WorkflowStateId = firstState.WorkflowStateId,
            StepStatus = "InProgress",
            StartedDate = DateTime.UtcNow,
        });

        fileMaster.WorkflowInstanceId = instance.WorkflowInstanceId;
        await _context.SaveChangesAsync();

        await _audit.LogAsync(new AuditEvent(
            EntityType: nameof(FileMaster),
            EntityId: fileMasterId.ToString(),
            Action: "WorkflowStarted",
            ToValue: firstState.StateName));
        return instance;
    }

    public async Task<WorkflowInstance> AdvanceAsync(Guid fileMasterId, Guid? userId, string? notes)
    {
        var instance = await LoadInstanceAsync(fileMasterId);
        var fileMaster = await _context.FileMasters.FindAsync(fileMasterId)
            ?? throw new InvalidOperationException($"FileMaster {fileMasterId} not found.");
        var currentState = await _context.WorkflowStates.FindAsync(instance.CurrentWorkflowStateId)
            ?? throw new InvalidOperationException("Current state not found.");

        if (currentState.IsTerminal)
            throw new InvalidOperationException($"Case is at terminal state '{currentState.StateName}'.");

        var nextState = await ResolveNextStateAsync(fileMaster, currentState)
            ?? throw new InvalidOperationException("No further states available.");

        return await MoveToStateAsync(instance, fileMaster, currentState, nextState, userId, notes);
    }

    public async Task<WorkflowInstance> TransitionToAsync(Guid fileMasterId, string targetStateName, Guid? userId, string? notes)
    {
        var instance = await LoadInstanceAsync(fileMasterId);
        var fileMaster = await _context.FileMasters.FindAsync(fileMasterId)
            ?? throw new InvalidOperationException($"FileMaster {fileMasterId} not found.");
        var currentState = await _context.WorkflowStates.FindAsync(instance.CurrentWorkflowStateId)
            ?? throw new InvalidOperationException("Current state not found.");
        var target = await _context.WorkflowStates.SingleOrDefaultAsync(s => s.StateName == targetStateName)
            ?? throw new InvalidOperationException($"Workflow state '{targetStateName}' not found.");
        return await MoveToStateAsync(instance, fileMaster, currentState, target, userId, notes);
    }

    public async Task<IReadOnlyList<WorkflowStepRecord>> GetHistoryAsync(Guid workflowInstanceId)
    {
        return await _context.WorkflowStepRecords
            .Include(s => s.WorkflowState)
            .Where(s => s.WorkflowInstanceId == workflowInstanceId)
            .OrderBy(s => s.StartedDate)
            .ToListAsync();
    }

    public async Task<WorkflowInstance?> GetInstanceForFileAsync(Guid fileMasterId)
    {
        return await _context.WorkflowInstances
            .Include(w => w.CurrentWorkflowState)
            .FirstOrDefaultAsync(w => w.FileMasterId == fileMasterId);
    }

    /// <summary>
    /// Resolves the next workflow state, honouring AssessmentTrack:
    /// on S33_2 track, transitions that would land in CP5–CP9 are redirected to
    /// the S33_2 terminal declaration state.
    /// </summary>
    private async Task<WorkflowState?> ResolveNextStateAsync(FileMaster fileMaster, WorkflowState currentState)
    {
        var defaultNext = await _context.WorkflowStates
            .Where(s => s.DisplayOrder > currentState.DisplayOrder)
            .OrderBy(s => s.DisplayOrder)
            .FirstOrDefaultAsync();
        if (defaultNext is null) return null;

        if (string.Equals(fileMaster.AssessmentTrack, "S33_2_Declaration", StringComparison.OrdinalIgnoreCase)
            && CpsSkippedOnS33_2.Any(cp => defaultNext.StateName.StartsWith(cp, StringComparison.OrdinalIgnoreCase)))
        {
            var declaration = await _context.WorkflowStates.SingleOrDefaultAsync(s => s.StateName == S33_2_TerminalStateName);
            if (declaration is not null) return declaration;
        }

        return defaultNext;
    }

    private async Task<WorkflowInstance> MoveToStateAsync(
        WorkflowInstance instance,
        FileMaster fileMaster,
        WorkflowState currentState,
        WorkflowState target,
        Guid? userId,
        string? notes)
    {
        // Evaluate guards in order; first denial blocks the transition.
        var guardCtx = new GuardContext(fileMaster, currentState, target);
        foreach (var guard in _guards)
        {
            var result = await guard.CheckAsync(guardCtx);
            if (!result.Allowed)
                throw new InvalidOperationException(result.Reason ?? "Transition blocked by a guard.");
        }

        var currentStep = await _context.WorkflowStepRecords
            .Where(s => s.WorkflowInstanceId == instance.WorkflowInstanceId && s.StepStatus == "InProgress")
            .OrderByDescending(s => s.StartedDate)
            .FirstOrDefaultAsync();

        if (currentStep != null)
        {
            currentStep.StepStatus = "Completed";
            currentStep.CompletedDate = DateTime.UtcNow;
            currentStep.CompletedById = userId;
            currentStep.Notes = notes;
        }

        instance.CurrentWorkflowStateId = target.WorkflowStateId;
        if (target.IsTerminal)
        {
            instance.Status = "Completed";
            instance.CompletedDate = DateTime.UtcNow;
        }

        _context.WorkflowStepRecords.Add(new WorkflowStepRecord
        {
            WorkflowStepRecordId = Guid.NewGuid(),
            WorkflowInstanceId = instance.WorkflowInstanceId,
            WorkflowStateId = target.WorkflowStateId,
            StepStatus = target.IsTerminal ? "Completed" : "InProgress",
            StartedDate = DateTime.UtcNow,
            CompletedDate = target.IsTerminal ? DateTime.UtcNow : null,
            CompletedById = target.IsTerminal ? userId : null,
        });

        await _context.SaveChangesAsync();

        await _audit.LogAsync(new AuditEvent(
            EntityType: nameof(FileMaster),
            EntityId: instance.FileMasterId.ToString(),
            Action: target.IsTerminal ? "WorkflowCompleted" : "WorkflowAdvanced",
            UserId: userId,
            FromValue: currentState.StateName,
            ToValue: target.StateName,
            Reason: notes));

        return instance;
    }

    private async Task<WorkflowInstance> LoadInstanceAsync(Guid fileMasterId)
    {
        return await _context.WorkflowInstances
            .FirstOrDefaultAsync(w => w.FileMasterId == fileMasterId)
            ?? throw new InvalidOperationException($"No workflow instance for FileMaster {fileMasterId}.");
    }
}
