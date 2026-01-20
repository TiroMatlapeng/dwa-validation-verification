public class PropertyOwner
{
    public Guid OwnerId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required DateOnly DateOfBirth  { get; set; }
    public required CustomerType CustomerType { get; set; }
    public required CustomerTitle CustomerTitle { get; set; }
    public required int IdentityDocumentNumber { get; set; }
    public required string EmailAddress { get; set; }
    public Guid AddressId { get; set; } 
    public Address? Address  { get; set; }
    public required Gender OwnerGender  { get; set; }
    public ICollection<PropertyOwnership> PropertyOwnerships { get; set; } = new List<PropertyOwnership>();

}