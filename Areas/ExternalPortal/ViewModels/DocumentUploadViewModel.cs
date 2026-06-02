using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using dwa_ver_val.Services.Documents;

namespace dwa_ver_val.Areas.ExternalPortal.ViewModels;

public class DocumentUploadViewModel
{
    public Guid FileMasterId { get; set; }

    [Required(ErrorMessage = "Please select a document type.")]
    [Display(Name = "Document Type")]
    public string DocumentType { get; set; } = DocumentTypes.TitleDeed;

    [Required(ErrorMessage = "Please select a file.")]
    public IFormFile? File { get; set; }
}
