public class WorkflowStepRecord
{
    public Guid WorkflowStepRecordId { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public Guid WorkflowStateId { get; set; }
    public WorkflowState? WorkflowState { get; set; }
    public required string StepStatus { get; set; } // Pending, InProgress, Completed, Skipped
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid? CompletedById { get; set; }
    public ApplicationUser? CompletedBy { get; set; }
    public string? Notes { get; set; }
    public string? ValidationErrors { get; set; } // JSON
}
