public class WorkflowInstance
{
    public Guid WorkflowInstanceId { get; set; }
    public Guid FileMasterId { get; set; }
    public FileMaster? FileMaster { get; set; }
    public Guid CurrentWorkflowStateId { get; set; }
    public WorkflowState? CurrentWorkflowState { get; set; }
    public required string Status { get; set; } // Active, Paused, Completed, Cancelled
    public Guid? AssignedToId { get; set; }
    public ApplicationUser? AssignedTo { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public ICollection<WorkflowStepRecord> StepRecords { get; set; } = new List<WorkflowStepRecord>();
}
