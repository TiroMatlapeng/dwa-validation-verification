using System.ComponentModel.DataAnnotations;

/// <summary>
/// PAJA compliance checklist (PRD CP19) — must be complete before Letter 3 (ELU certificate)
/// can be issued. 1:1 with FileMaster; unique index enforced in OnModelCreating.
/// </summary>
public class PAJAChecklist
{
    public Guid PAJAChecklistId { get; set; }
    public Guid FileMasterId { get; set; }
    public FileMaster? FileMaster { get; set; }

    [Display(Name = "Factual Basis")]
    public string? FactualBasis { get; set; }

    [Display(Name = "Legal Basis")]
    public string? LegalBasis { get; set; }

    [Display(Name = "Consideration of User Input")]
    public string? UserInputConsideration { get; set; }

    [Display(Name = "Final Reasoning")]
    public string? FinalReasoning { get; set; }

    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedById { get; set; }
    public ApplicationUser? CompletedBy { get; set; }

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(FactualBasis) &&
        !string.IsNullOrWhiteSpace(LegalBasis) &&
        !string.IsNullOrWhiteSpace(UserInputConsideration) &&
        !string.IsNullOrWhiteSpace(FinalReasoning) &&
        CompletedAt.HasValue;
}
