public class PublicUser
{
    public Guid PublicUserId { get; set; }
    public required string EmailAddress { get; set; }
    public required string PasswordHash { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? IdentityNumber { get; set; }
    public string? BusinessRegistrationNumber { get; set; }
    public string? PhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool MfaEnabled { get; set; }
    public required string Status { get; set; } // Pending, Active, Suspended, Deactivated
    public DateTime RegistrationDate { get; set; }
    public ICollection<PublicUserProperty> PublicUserProperties { get; set; } = new List<PublicUserProperty>();
}
