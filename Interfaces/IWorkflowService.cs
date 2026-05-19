public interface IWorkflowService
{
    Task<WorkflowInstance> StartWorkflowAsync(Guid fileMasterId);
    Task<WorkflowInstance> AdvanceAsync(Guid fileMasterId, Guid? userId, string? notes);
    Task<WorkflowInstance> TransitionToAsync(Guid fileMasterId, string targetStateName, Guid? userId, string? notes);
    Task<IReadOnlyList<WorkflowStepRecord>> GetHistoryAsync(Guid workflowInstanceId);
    Task<WorkflowInstance?> GetInstanceForFileAsync(Guid fileMasterId);

    /// <summary>
    /// Returns the human-readable reasons preventing the case from advancing to the
    /// next workflow state. Empty list = no blockers. Used by the FileMaster details
    /// view to render an inline "Cannot advance yet" panel instead of forcing the
    /// user to click Advance and parse a thrown exception.
    /// </summary>
    Task<List<string>> GetBlockingReasonsAsync(Guid fileMasterId, Guid? userId);
}
