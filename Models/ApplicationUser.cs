using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser
{
    public Guid ApplicationUserId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string EmployeeNumber { get; set; }
    public Guid? OrgUnitId { get; set; }
    public OrganisationalUnit? OrgUnit { get; set; }
}
