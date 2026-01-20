using System.ComponentModel.DataAnnotations;

public class GovernmentWaterScheme
{
    [Key]
    public Guid WaterSchemeId { get; set; }
    public required string WaterSchemeName { get; set; } 
}