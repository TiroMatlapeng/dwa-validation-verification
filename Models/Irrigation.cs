using System.ComponentModel.DataAnnotations.Schema;

public class Irrigation
{
    public Guid IrrigationId { get; set;}
    public required string IrrigationName { get; set;}
    public required Property Property { get; set; }
    public Guid PropertyId { get; set; }
    public DateOnly WaterDate { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal WaterVolume  { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal WaterLandArea { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal WaterCropArea { get; set; }
    public WaterSourceType WaterSourceType { get; set; }



}