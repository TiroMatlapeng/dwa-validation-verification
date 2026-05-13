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
        CurrentState is { } s && (s.StateName == "CP9_SFRACalculated"
                                   || s.StateName.StartsWith("S35_")
                                   || s.StateName.StartsWith("S33_"));

    /// <summary>
    /// Actions surfaced by the _LettersPanel for the current workflow state. Both happy and
    /// unhappy paths are exposed where they exist (e.g. Letter 1 issued → either record a
    /// response, OR issue Letter 1A as the S53(1) directive when the recipient stays silent).
    /// </summary>
    public List<string> AvailableLetterActions
    {
        get
        {
            if (CurrentState == null) return new();

            // CP9 forks by AssessmentTrack: standard S35 verification → Letter 1; S33(3)
            // individual-application → either declaration (a) or (b). S33(2) Kader Asmal
            // is handled by track-skip in WorkflowService and does not surface here.
            if (CurrentState.StateName == "CP9_SFRACalculated")
            {
                return string.Equals(FileMaster.AssessmentTrack, "S33_3_Declaration", StringComparison.OrdinalIgnoreCase)
                    ? new() { "IssueS33_3a", "IssueS33_3b" }
                    : new() { "IssueLetter1" };
            }

            return CurrentState.StateName switch
            {
                "S35_Letter1Issued"        => new() { "MarkLetter1Responded", "IssueLetter1A" },
                "S35_Letter1AIssued"       => new() { "MarkLetter1AResponded" },
                "S35_Letter1Responded"     => new() { "IssueLetter2", "IssueLetter3" },
                "S35_Letter2Issued"        => new() { "MarkLetter2Responded", "IssueLetter2A" },
                "S35_Letter2AIssued"       => new() { "MarkLetter2AResponded" },
                "S35_Letter2Responded"     => new() { "IssueLetter3" },
                "S35_Letter3Issued"        => new() { "MarkELUConfirmed", "MarkUnlawfulUseFound" },
                "S35_ELUConfirmed"         => new() { "CloseCase" },
                "S35_UnlawfulUseFound"     => new() { "IssueLetter4A" },
                "S35_Letter4AIssued"       => new() { "IssueLetter4_5" },
                "S35_Letter4And5Issued"    => new() { "CloseCase" },
                "S33_2_DeclarationIssued"  => new() { "CloseCase" },
                "S33_3_DeclarationIssued"  => new() { "CloseCase" },
                _                          => new()
            };
        }
    }
}
