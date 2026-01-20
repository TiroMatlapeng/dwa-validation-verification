using System.ComponentModel.DataAnnotations.Schema;

public class FieldAndCrop
{
    public Guid FieldAndCropId { get; set; }
    public required Property Property { get; set; }
    public required Guid PropertyId { get; set; }
    public required Period Period{ get; set; }
    public Guid PeriodId { get; set; }
    public string? FieldNumber { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public required decimal FieldArea { get; set; }
    public required Crop Crop { get; set; }
    public DateOnly? PlantDate { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal RotationFactor { get; set; }
    public IrrigationSystem? IrrigationSystem { get; set; }
    public required WaterSource WaterSource { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal CropArea { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal SAPWATCalculationResult { get; set; }

}