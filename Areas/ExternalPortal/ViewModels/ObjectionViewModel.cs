using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.Areas.ExternalPortal.ViewModels;

public class ObjectionViewModel
{
    public Guid FileMasterId { get; set; }

    [Required(ErrorMessage = "Grounds for objection are required.")]
    [MinLength(20, ErrorMessage = "Please provide at least 20 characters explaining your grounds.")]
    [MaxLength(4000)]
    [Display(Name = "Grounds for Objection / Appeal")]
    public string Grounds { get; set; } = "";
}
