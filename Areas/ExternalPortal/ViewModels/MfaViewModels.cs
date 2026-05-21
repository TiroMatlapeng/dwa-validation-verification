using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.Areas.ExternalPortal.ViewModels;

public class MfaSelectMethodViewModel
{
    [Required]
    public string MfaMethod { get; set; } = "";
}

public class MfaEnrolTotpViewModel
{
    public string QrCodeBase64 { get; set; } = "";
    [Required, StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = "";
}

public class MfaEnrolSmsViewModel
{
    [Required, Phone]
    public string PhoneNumber { get; set; } = "";
}

public class MfaVerifySmsEnrolmentViewModel
{
    [Required, StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = "";
}

public class MfaVerifyViewModel
{
    public string MfaMethod { get; set; } = "";
    [Required, StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = "";
    public bool TrustDevice { get; set; }
    public string? ReturnUrl { get; set; }
}
