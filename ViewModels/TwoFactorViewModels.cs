using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.ViewModels;

public class TwoFactorLoginViewModel
{
    [Required, StringLength(7, MinimumLength = 6), DataType(DataType.Text)]
    [Display(Name = "Authenticator code")]
    public string TwoFactorCode { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public class RecoveryCodeLoginViewModel
{
    [Required, DataType(DataType.Text)]
    [Display(Name = "Recovery code")]
    public string RecoveryCode { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class SecurityIndexViewModel
{
    public bool TwoFactorEnabled { get; set; }
    public int RecoveryCodesLeft { get; set; }
}

public class EnableAuthenticatorViewModel
{
    /// <summary>Base32 authenticator key, displayed for manual entry.</summary>
    public string SharedKey { get; set; } = string.Empty;

    /// <summary>QR code PNG as a data: URI for inline rendering.</summary>
    public string QrCodeDataUri { get; set; } = string.Empty;

    [Required, StringLength(7, MinimumLength = 6), DataType(DataType.Text)]
    [Display(Name = "Verification code")]
    public string Code { get; set; } = string.Empty;
}

public class RecoveryCodesViewModel
{
    public IReadOnlyList<string> Codes { get; set; } = Array.Empty<string>();
}
