using System.ComponentModel.DataAnnotations.Schema;

public class Document
{
    public Guid DocumentId { get; set; }
    public Guid? FileMasterId { get; set; }
    public FileMaster? FileMaster { get; set; }
    public required string DocumentType { get; set; } // Letter, TitleDeed, GISMap, SG, Upload
    public required string FileName { get; set; }
    public required string BlobPath { get; set; }
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public ApplicationUser? UploadedByUser { get; set; }
    public Guid? UploadedByPublicUserId { get; set; }
    public PublicUser? UploadedByPublicUser { get; set; }
    public DateTime UploadDate { get; set; }
    public string? VirusScanStatus { get; set; } // Pending, Clean, Infected
    public string? DocumentHash { get; set; } // SHA-256
}
