namespace dwa_ver_val.Services.Letters;

/// <summary>
/// Strongly-typed letter-rendering context. See docs/contracts/letter-context.md for the contract.
/// </summary>
public record LetterContext(
    string ReferenceNumber,
    DateOnly IssueDate,
    DateOnly? DueDate,
    string CaseNumber,
    string FarmName,
    string PropertyReference,
    string RecipientName,
    string? RecipientAddress,
    string? IrrigationBoardName,
    string SignatoryName,
    string SignatoryTitle,
    string SignatoryOrgUnit,
    decimal? LawfulVolumeM3,
    decimal? UnlawfulVolumeM3,
    string? AdditionalNotes);
