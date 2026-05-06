using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.Areas.ExternalPortal.ViewModels;

public class RegisterViewModel
{
    [Required, EmailAddress, Display(Name = "Email address")]
    public string Email { get; set; } = "";

    [Required, MinLength(12), DataType(DataType.Password), Display(Name = "Password")]
    public string Password { get; set; } = "";

    [Required, DataType(DataType.Password), Display(Name = "Confirm password"),
     Compare(nameof(Password), ErrorMessage = "The two passwords don't match.")]
    public string ConfirmPassword { get; set; } = "";

    [Required, Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required, Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [Required, StringLength(13, MinimumLength = 13, ErrorMessage = "Enter a 13-digit South African ID number."),
     Display(Name = "South African ID number")]
    public string IdentityNumber { get; set; } = "";

    [Display(Name = "Phone number (optional)")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "I am a Historically Disadvantaged Individual (HDI)")]
    public bool IsHDI { get; set; }

    [Display(Name = "I consent to processing of my HDI status (race / gender) for prioritisation purposes (POPIA Section 26)")]
    public bool HdiConsent { get; set; }

    [Display(Name = "I accept the Terms of Use")]
    public bool AcceptTerms { get; set; }
}
