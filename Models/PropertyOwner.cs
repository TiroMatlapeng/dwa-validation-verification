using System.ComponentModel.DataAnnotations;

public class PropertyOwner
{
    public Guid OwnerId { get; set; }

    [Display(Name = "First Name")]
    public required string FirstName { get; set; }

    [Display(Name = "Last Name")]
    public required string LastName { get; set; }

    [Display(Name = "Date of Birth")]
    public DateOnly? DateOfBirth { get; set; }

    [Display(Name = "Customer Type")]
    public Guid? CustomerTypeId { get; set; }
    public CustomerType? CustomerType { get; set; }

    [Display(Name = "Title")]
    public string? Title { get; set; }

    [Display(Name = "Identity Document Number")]
    public string? IdentityDocumentNumber { get; set; }

    [Display(Name = "Email Address")]
    public string? EmailAddress { get; set; }

    public Guid? AddressId { get; set; }
    public Address? Address { get; set; }

    [Display(Name = "Gender")]
    public string? Gender { get; set; }

    [Display(Name = "Historically Disadvantaged Individual (HDI)")]
    public bool IsHDI { get; set; } // Historically Disadvantaged Individual (Black people and women)

    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }

    public ICollection<PropertyOwnership> PropertyOwnerships { get; set; } = new List<PropertyOwnership>();
}
