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
}
