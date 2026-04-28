using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.ViewModels;

public class ForgotPasswordViewModel
{
    [Required, EmailAddress, Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}

public class ForgotPasswordResultViewModel
{
    public required string Email { get; set; }

    /// <summary>
    /// In a future iteration this is sent via email. While no email service is wired we surface
    /// the link directly so the demo flow still completes.
    /// </summary>
    public string? ResetLink { get; set; }
}

public class ResetPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(8), Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword)), Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
