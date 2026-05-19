/// <summary>
/// Form posted by the PAJAChecklist.cshtml view. Four free-text sections that must all
/// be populated before <see cref="PAJAChecklist.IsComplete"/> returns true (which in turn
/// is required by LetterService before Letter 3 / S35(4) ELU Certificate can be issued).
/// </summary>
public class PAJAChecklistForm
{
    public string? FactualBasis { get; set; }
    public string? LegalBasis { get; set; }
    public string? UserInputConsideration { get; set; }
    public string? FinalReasoning { get; set; }
}
