public class SmsOtp
{
    public Guid SmsOtpId { get; set; }
    public Guid PublicUserId { get; set; }
    public required string CodeHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Used { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public PublicUser? PublicUser { get; set; }
}
