using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.ViewModels;

public class LoginViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }

    /// <summary>True when AzureAd configuration is present — show the "Sign in with Microsoft" button.</summary>
    public bool EntraEnabled { get; set; }
}
