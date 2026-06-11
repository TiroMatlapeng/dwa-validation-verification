public class FileMasterDetailsViewModel
{
    public required FileMaster FileMaster { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public List<WorkflowState> AllStates { get; set; } = new();
    public List<WorkflowStepRecord> History { get; set; } = new();
    public List<LetterIssuance> Letters { get; set; } = new();
    public List<AuditLog> AuditTrail { get; set; } = new();

    // Internal staff documents attached to this case + the Appendix A requirement checklist.
    public List<Document> CaseDocuments { get; set; } = new();
    public IReadOnlyList<dwa_ver_val.Services.Workflow.Guards.DocumentRequirementStatus> DocumentRequirementStatuses { get; set; }
        = new List<dwa_ver_val.Services.Workflow.Guards.DocumentRequirementStatus>();

    // Workflow gap-fill (PRD CP12/CP13/CP19) — inline guard feedback + PAJA checklist.
    public List<string> BlockingReasons { get; set; } = new();
    public PAJAChecklist? PAJAChecklist { get; set; }
    public LawfulnessAssessmentResult? LawfulnessAssessmentResult { get; set; }

    public WorkflowState? CurrentState => WorkflowInstance?.CurrentWorkflowState;

    // The letter/declaration sub-process launches from CP_StakeholderWorkshop (the last
    // control point before the statutory letters). CP9_SFRACalculated is deliberately NOT
    // letter-ready: a case must still advance CP9 → CP11 → CP_PrePublicReview →
    // CP_StakeholderWorkshop (file compilation, then the Regional Manager pre-public review,
    // then the stakeholder workshop) before any letter is issued. Treating CP9 as letter-ready
    // hid the "Advance to Next CP" button at CP9 and stranded the case, leaving CP11 and the
    // review/workshop states unreachable through the UI. (CanIssueLetterAsync still tolerates
    // CP9 as an S35_L1/S33_3 prerequisite for legacy cases issued before the gap-fill.)
    public bool IsReadyForLetters =>
        CurrentState is { } s && (s.StateName == "CP_StakeholderWorkshop"
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

            // CP_StakeholderWorkshop (and the legacy CP9_SFRACalculated for cases that
            // pre-date the gap-fill) forks by AssessmentTrack: standard S35 verification
            // → Letter 1; S33(3) individual-application → either declaration (a) or (b).
            // S33(2) Kader Asmal surfaces at S33_2_ReadyForDeclaration → IssueS33_2 (see switch below).
            if (CurrentState.StateName is "CP9_SFRACalculated" or "CP_StakeholderWorkshop")
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
                "S33_2_ReadyForDeclaration" => new() { "IssueS33_2" },
                "S33_2_DeclarationIssued"  => new() { "CloseCase" },
                "S33_3_DeclarationIssued"  => new() { "CloseCase" },
                _                          => new()
            };
        }
    }
}
