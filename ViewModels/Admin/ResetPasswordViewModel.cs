using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.ViewModels.Admin;

public class ResetPasswordViewModel
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(8), Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword)), Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
