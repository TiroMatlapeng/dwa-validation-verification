public interface IWorkflowService
{
    Task<WorkflowInstance> StartWorkflowAsync(Guid fileMasterId);
    Task<WorkflowInstance> AdvanceAsync(Guid fileMasterId, Guid? userId, string? notes);
    Task<WorkflowInstance> TransitionToAsync(Guid fileMasterId, string targetStateName, Guid? userId, string? notes);
    Task<IReadOnlyList<WorkflowStepRecord>> GetHistoryAsync(Guid workflowInstanceId);
    Task<WorkflowInstance?> GetInstanceForFileAsync(Guid fileMasterId);
}
