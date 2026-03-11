public class SignatureRequest
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }
    public Guid? ApplicationUserId { get; set; }
    public ApplicationUser? ApplicationUser { get; set; }
    public Guid? PublicUserId { get; set; }
    public PublicUser? PublicUser { get; set; }
    public string? Reason { get; set; }
    public required string Status { get; set; } // Pending, Completed, Declined, Expired
    public DateTime RequestedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid? DigitalSignatureId { get; set; }
    public DigitalSignature? DigitalSignature { get; set; }
}
