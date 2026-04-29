using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    [Display(Name = "First Name")]
    public required string FirstName { get; set; }

    [Display(Name = "Last Name")]
    public required string LastName { get; set; }

    [Display(Name = "Employee Number")]
    public required string EmployeeNumber { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Organisational Unit")]
    public Guid? OrgUnitId { get; set; }
    public OrganisationalUnit? OrgUnit { get; set; }
}
