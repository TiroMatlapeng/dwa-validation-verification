namespace dwa_ver_val.ViewModels.Admin;

public class UserListItemViewModel
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string FullName { get; set; }
    public required string EmployeeNumber { get; set; }
    public required string Role { get; set; }
    public string? OrgUnitName { get; set; }
    public bool IsActive { get; set; }
}
