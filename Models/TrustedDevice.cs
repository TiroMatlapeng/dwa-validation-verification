public class TrustedDevice
{
    public Guid TrustedDeviceId { get; set; }
    public Guid PublicUserId { get; set; }
    public required string DeviceTokenHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? UserAgent { get; set; }
    public PublicUser? PublicUser { get; set; }
}
