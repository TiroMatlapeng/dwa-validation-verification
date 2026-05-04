using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.Areas.ExternalPortal.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress, Display(Name = "Email address")]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password), Display(Name = "Password")]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}
