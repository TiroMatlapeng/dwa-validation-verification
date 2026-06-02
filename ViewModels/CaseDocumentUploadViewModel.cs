using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

public class CaseDocumentUploadViewModel
{
    public Guid FileMasterId { get; set; }

    [Required(ErrorMessage = "Please select a document type.")]
    [Display(Name = "Document Type")]
    public string DocumentType { get; set; } = "TitleDeedReport";

    [Display(Name = "Control Point (optional)")]
    public Guid? WorkflowStateId { get; set; }

    [Required(ErrorMessage = "Please select a file.")]
    public IFormFile? File { get; set; }
}
