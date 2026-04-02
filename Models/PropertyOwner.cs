public class PropertyOwner
{
    public Guid OwnerId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Guid? CustomerTypeId { get; set; }
    public CustomerType? CustomerType { get; set; }
    public string? Title { get; set; }
    public string? IdentityDocumentNumber { get; set; }
    public string? EmailAddress { get; set; }
    public Guid? AddressId { get; set; }
    public Address? Address { get; set; }
    public string? Gender { get; set; }
    public bool IsHDI { get; set; } // Historically Disadvantaged Individual (Black people and women)
    public string? PhoneNumber { get; set; }
    public ICollection<PropertyOwnership> PropertyOwnerships { get; set; } = new List<PropertyOwnership>();
}
