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

    /// <summary>
    /// BUG-014: pre-issuance guard. Returns whether a letter identified by
    /// <paramref name="letterCode"/> (e.g. "S35_L1", "S33_2_Decl") may be issued from the
    /// case's CURRENT workflow state, BEFORE any LetterIssuance row is written. Prevents an
    /// orphaned letter record being created when the subsequent workflow transition would be
    /// rejected because the case is not in the correct prerequisite state.
    /// </summary>
    Task<LetterIssuanceCheck> CanIssueLetterAsync(Guid fileMasterId, string letterCode);
}

/// <summary>Result of <see cref="IWorkflowService.CanIssueLetterAsync"/>.</summary>
public record LetterIssuanceCheck(bool Allowed, string? Reason)
{
    public static readonly LetterIssuanceCheck Ok = new(true, null);
    public static LetterIssuanceCheck Deny(string reason) => new(false, reason);
}
