using System.ComponentModel.DataAnnotations;

public class FieldAndCropViewModel
{
    public Guid FieldAndCropId { get; set; }

    [Required]
    public Guid PropertyId { get; set; }

    [Required]
    public Guid PeriodId { get; set; }

    [Required]
    public Guid CropId { get; set; }

    public Guid? IrrigationSystemId { get; set; }

    [Required]
    public Guid WaterSourceId { get; set; }

    [Display(Name = "Field Number")]
    public string? FieldNumber { get; set; }

    [Required]
    [Display(Name = "Field Area (ha)")]
    public decimal FieldArea { get; set; }

    [Display(Name = "Plant Date")]
    public DateOnly? PlantDate { get; set; }

    [Display(Name = "Rotation Factor")]
    [Range(0, 1, ErrorMessage = "Rotation factor must be between 0 and 1")]
    public decimal RotationFactor { get; set; }

    [Display(Name = "Crop Area (ha)")]
    public decimal CropArea { get; set; }

    [Display(Name = "SAPWAT Result (mm/ha/a)")]
    public decimal SAPWATCalculationResult { get; set; }

    // Populated for Index display
    public string? CropName { get; set; }
    public string? WaterSourceName { get; set; }
    public string? PeriodName { get; set; }
}
