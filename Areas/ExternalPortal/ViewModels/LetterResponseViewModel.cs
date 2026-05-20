using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.Areas.ExternalPortal.ViewModels;

public class LetterResponseViewModel
{
    public Guid FileMasterId { get; set; }
    public Guid? LetterIssuanceId { get; set; }

    [Required(ErrorMessage = "Response text is required.")]
    [MinLength(10, ErrorMessage = "Please provide at least 10 characters.")]
    [MaxLength(4000)]
    [Display(Name = "Your Response")]
    public string ResponseText { get; set; } = "";
}
