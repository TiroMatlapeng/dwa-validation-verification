using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class LawfulnessAssessmentResult
{
    public Guid LawfulnessAssessmentResultId { get; set; }
    public Guid FileMasterId { get; set; }
    public FileMaster? FileMaster { get; set; }

    public required string LegalFramework { get; set; } // "General" | "GWCA"
    public Guid? GwcaId { get; set; }
    public GovernmentWaterControlArea? Gwca { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalIrrigatedAreaHa { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalIrrigationDemandM3 { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal LawfulIrrigationM3 { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnlawfulIrrigationM3 { get; set; }
    public string? IrrigationLimitApplied { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalDamCapacityM3 { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal LawfulStorageM3 { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnlawfulStorageM3 { get; set; }
    public string? StorageLimitApplied { get; set; }

    [Display(Name = "Assessed")]
    public DateTime AssessedAt { get; set; }
}
