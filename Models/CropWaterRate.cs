using System.ComponentModel.DataAnnotations.Schema;

public class CropWaterRate
{
    public Guid CropWaterRateId { get; set; }
    public Guid CropId { get; set; }
    public Crop? Crop { get; set; }

    // Nullable: null means "applies to all irrigation systems for this crop"
    public Guid? IrrigationSystemId { get; set; }
    public IrrigationSystem? IrrigationSystem { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public required decimal RatePerHaPerAnnum { get; set; }

    public string? Source { get; set; }
}
