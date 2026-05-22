using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class FieldAndCrop
{
    public Guid FieldAndCropId { get; set; }
    public required Property Property { get; set; }

    [Display(Name = "Property")]
    public required Guid PropertyId { get; set; }
    public required Period Period{ get; set; }

    [Display(Name = "Period")]
    public Guid PeriodId { get; set; }

    [Display(Name = "Field Number")]
    public string? FieldNumber { get; set; }

    [Display(Name = "Field Area (ha)")]
    [Column(TypeName = "decimal(18, 2)")]
    public required decimal FieldArea { get; set; }
    public required Crop Crop { get; set; }

    [Display(Name = "Plant Date")]
    public DateOnly? PlantDate { get; set; }

    [Display(Name = "Rotation Factor")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal RotationFactor { get; set; }
    public IrrigationSystem? IrrigationSystem { get; set; }
    public required WaterSource WaterSource { get; set; }

    [Display(Name = "Crop Area (ha)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal CropArea { get; set; }

    [Display(Name = "SAPWAT Result (mm/ha/a)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal SAPWATCalculationResult { get; set; }

    [Display(Name = "Last Calculated")]
    public DateTime? LastCalculatedAt { get; set; }
}