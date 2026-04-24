public class FileMasterDetailsViewModel
{
    public required FileMaster FileMaster { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public List<WorkflowState> AllStates { get; set; } = new();
    public List<WorkflowStepRecord> History { get; set; } = new();
    public List<LetterIssuance> Letters { get; set; } = new();
    public List<AuditLog> AuditTrail { get; set; } = new();

    public WorkflowState? CurrentState => WorkflowInstance?.CurrentWorkflowState;

    public bool IsReadyForLetters =>
        CurrentState is { } s && (s.StateName == "CP9_SFRACalculated" || s.StateName.StartsWith("S35_"));

    public List<string> AvailableLetterActions
    {
        get
        {
            if (CurrentState == null) return new();
            return CurrentState.StateName switch
            {
                "CP9_SFRACalculated"         => new() { "IssueLetter1" },
                "S35_Letter1Issued"          => new() { "MarkLetter1Responded" },
                "S35_Letter1Responded"       => new() { "IssueLetter2", "IssueLetter3" },
                "S35_Letter2Issued"          => new() { "MarkLetter2Responded" },
                "S35_Letter2Responded"       => new() { "IssueLetter3" },
                "S35_Letter3Issued"          => new() { "MarkELUConfirmed" },
                "S35_ELUConfirmed"           => new() { "CloseCase" },
                _                            => new()
            };
        }
    }
}
