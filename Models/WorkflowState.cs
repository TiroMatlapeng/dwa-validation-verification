public class WorkflowState
{
    public Guid WorkflowStateId { get; set; }
    public required string StateName { get; set; }
    public required string Phase { get; set; } // Inception, Verification, Validation
    public int DisplayOrder { get; set; }
    public bool IsTerminal { get; set; }
}
