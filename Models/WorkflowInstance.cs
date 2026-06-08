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

    /// <summary>
    /// SQL Server rowversion concurrency token. EF Core checks this on every UPDATE;
    /// a stale token (another session already committed) causes <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>.
    /// Store-generated — never set this in application code.
    /// On EF InMemory the value stays as the empty initialiser; InMemory ignores concurrency tokens.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
