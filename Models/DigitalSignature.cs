public class DigitalSignature
{
    public Guid SignatureId { get; set; }
    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }
    public Guid? ApplicationUserId { get; set; }
    public ApplicationUser? ApplicationUser { get; set; }
    public Guid? PublicUserId { get; set; }
    public PublicUser? PublicUser { get; set; }
    public byte[]? SignatureImage { get; set; }
    public string? SignatureHash { get; set; } // SHA-256
    public DateTime SignedAt { get; set; }
    public string? IPAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Reason { get; set; }
    public string? DocumentHashAtSigning { get; set; }
}
