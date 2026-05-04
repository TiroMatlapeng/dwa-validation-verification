using dwa_ver_val.Models.Enums;

public class PublicUserProperty
{
    public Guid Id { get; set; }
    public Guid PublicUserId { get; set; }
    public PublicUser? PublicUser { get; set; }
    public Guid PropertyId { get; set; }
    public Property? Property { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public ApplicationUser? ApprovedByUser { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public required PropertyClaimStatus Status { get; set; }
    public required PropertyClaimEvidenceType EvidenceType { get; set; }
    public Guid? EvidenceDocumentId { get; set; }
    public Document? EvidenceDocument { get; set; }
    public DateTime RequestedDate { get; set; }
    public string? RejectionReason { get; set; }
}
