namespace dwa_ver_val.Services.Letters;

public interface ILetterService
{
    /// <summary>Render preview bytes (unsigned) for an on-the-fly PDF view.</summary>
    Task<byte[]> RenderPreviewAsync(Guid fileMasterId, string letterCode);

    /// <summary>Render final PDF, persist to blob store, create LetterIssuance row with blob path + SHA-256 hash, emit audit.</summary>
    Task<LetterIssuance> IssueAsync(Guid fileMasterId, string letterCode, IssueLetterRequest req);
}

public record IssueLetterRequest(
    string RecipientName,
    string? RecipientAddress,
    string IssueMethod,
    DateOnly IssueDate,
    DateOnly? DueDate,
    Guid? ServedByOfficialId,
    string? AdditionalNotes,
    Guid SignedByUserId,
    string SignedByDisplayName,
    string SignedByTitle,
    string SignedByOrgUnit,
    decimal? LawfulVolumeM3 = null,
    decimal? UnlawfulVolumeM3 = null,
    string? IrrigationBoardName = null);
