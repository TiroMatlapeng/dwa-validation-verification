using Microsoft.EntityFrameworkCore;

public class WorkflowService : IWorkflowService
{
    private readonly ApplicationDBContext _context;

    public WorkflowService(ApplicationDBContext context)
    {
        _context = context;
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
        return instance;
    }

    public async Task<WorkflowInstance> AdvanceAsync(Guid fileMasterId, Guid? userId, string? notes)
    {
        var instance = await LoadInstanceAsync(fileMasterId);
        var currentState = await _context.WorkflowStates.FindAsync(instance.CurrentWorkflowStateId)
            ?? throw new InvalidOperationException("Current state not found.");

        if (currentState.IsTerminal)
            throw new InvalidOperationException($"Case is at terminal state '{currentState.StateName}'.");

        var nextState = await _context.WorkflowStates
            .Where(s => s.DisplayOrder > currentState.DisplayOrder)
            .OrderBy(s => s.DisplayOrder)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("No further states available.");

        return await MoveToStateAsync(instance, nextState, userId, notes);
    }

    public async Task<WorkflowInstance> TransitionToAsync(Guid fileMasterId, string targetStateName, Guid? userId, string? notes)
    {
        var instance = await LoadInstanceAsync(fileMasterId);
        var target = await _context.WorkflowStates.SingleOrDefaultAsync(s => s.StateName == targetStateName)
            ?? throw new InvalidOperationException($"Workflow state '{targetStateName}' not found.");
        return await MoveToStateAsync(instance, target, userId, notes);
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

    private async Task<WorkflowInstance> LoadInstanceAsync(Guid fileMasterId)
    {
        return await _context.WorkflowInstances
            .FirstOrDefaultAsync(w => w.FileMasterId == fileMasterId)
            ?? throw new InvalidOperationException($"No workflow instance for FileMaster {fileMasterId}.");
    }

    private async Task<WorkflowInstance> MoveToStateAsync(WorkflowInstance instance, WorkflowState target, Guid? userId, string? notes)
    {
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
        return instance;
    }
}
