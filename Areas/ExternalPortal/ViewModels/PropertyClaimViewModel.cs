using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.Areas.ExternalPortal.ViewModels;

public class PropertyClaimViewModel
{
    [Required(ErrorMessage = "Property code is required.")]
    [Display(Name = "Property Code (SGCode or Reference Number)")]
    public string PropertyCode { get; set; } = "";
}
