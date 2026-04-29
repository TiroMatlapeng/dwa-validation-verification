using System.ComponentModel.DataAnnotations.Schema;

public class LetterIssuance
{
    public Guid LetterIssuanceId { get; set; }
    public Guid FileMasterId { get; set; }
    public FileMaster? FileMaster { get; set; }
    public Guid? PropertyOwnerId { get; set; }
    public PropertyOwner? PropertyOwner { get; set; }
    public Guid LetterTypeId { get; set; }
    public LetterType? LetterType { get; set; }
    // S33(2) specific — links to the irrigation board for scheduled area declarations
    public Guid? IrrigationBoardId { get; set; }
    public IrrigationBoard? IrrigationBoard { get; set; }
    public bool IncludesDormantVolume { get; set; }   // S33(2): dormant but paid-for volumes count as ELU
    public DateOnly? GeneratedDate { get; set; }
    public DateOnly? SignedDate { get; set; }
    public Guid? SignedById { get; set; }
    public ApplicationUser? SignedBy { get; set; }
    public Guid? DigitalSignatureId { get; set; }
    public DigitalSignature? DigitalSignature { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public string? IssueMethod { get; set; } // RegisteredPost, Email, InPerson
    public DateOnly? DueDate { get; set; }
    public string? ResponseStatus { get; set; } // Pending, Agreed, NotAgreed, NoResponse, RTS
    public DateOnly? ResponseDate { get; set; }
    public bool? AgreedWithFindings { get; set; }
    public string? ResponseNotes { get; set; }
    public bool ReturnedToSender { get; set; }
    public string? BatchNumber { get; set; }
    public Guid? DocumentId { get; set; }
    public Document? Document { get; set; }
    public Guid? ReissuedFromId { get; set; }
    public LetterIssuance? ReissuedFrom { get; set; }
    public bool AvailableInPortal { get; set; }
    public DateTime? PortalFirstViewedDate { get; set; }
    public DateTime? PortalAcknowledgedDate { get; set; }
    public Guid? PortalAcknowledgedByPublicUserId { get; set; }
    public string? ServingOfficialName { get; set; }
    public DateOnly? PhysicalDeliveryDate { get; set; }

    // Plan 4 additions — generated PDF + signature metadata
    public string? BlobPath { get; set; }                 // Path/URL into IBlobStore; null until rendered
    public string? SignatureHash { get; set; }            // SHA-256 of the PDF bytes at sign time
    public Guid? ServedByOfficialId { get; set; }         // For Letter 1: required when IssueMethod == "InPerson"
    public ApplicationUser? ServedByOfficial { get; set; }
}
