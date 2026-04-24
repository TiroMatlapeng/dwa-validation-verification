using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.ViewModels.Admin;

public class EditUserViewModel
{
    public Guid Id { get; set; }

    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required, Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required, Display(Name = "Employee Number")]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, Display(Name = "Role")]
    public string Role { get; set; } = string.Empty;

    [Display(Name = "Organisational Unit")]
    public Guid? OrgUnitId { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; }

    public IEnumerable<string> AvailableRoles { get; set; } = Array.Empty<string>();
    public IEnumerable<(Guid Id, string Name)> AvailableOrgUnits { get; set; } = Array.Empty<(Guid, string)>();
}
