public class PublicUserRecoveryCode
{
    public Guid Id { get; set; }
    public Guid PublicUserId { get; set; }
    public PublicUser? PublicUser { get; set; }
    public required string CodeHash { get; set; }
    public bool Used { get; set; }
    public DateTime? UsedDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ExpiresDate { get; set; }
}
