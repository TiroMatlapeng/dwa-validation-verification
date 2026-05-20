namespace dwa_ver_val.Areas.ExternalPortal.ViewModels;

public class CaseSummaryViewModel
{
    public Guid FileMasterId { get; init; }
    public string FarmName { get; init; } = "";
    public string PropertyReference { get; init; } = "";
    public string SGCode { get; init; } = "";
    public string? WorkflowState { get; init; }
    public int UnreadNotifications { get; init; }
}

public class CaseDetailViewModel
{
    public FileMaster FileMaster { get; init; } = null!;
    public IReadOnlyList<LetterIssuance> Letters { get; init; } = [];
    public IReadOnlyList<CaseComment> Comments { get; init; } = [];
    public IReadOnlyList<Document> Documents { get; init; } = [];
    public IReadOnlyList<Objection> Objections { get; init; } = [];
}
