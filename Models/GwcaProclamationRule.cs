using System.ComponentModel.DataAnnotations.Schema;

public class GwcaProclamationRule
{
    public Guid RuleId { get; set; }
    public Guid WaterControlAreaId { get; set; }
    public GovernmentWaterControlArea? WaterControlArea { get; set; }

    public required string RuleCode { get; set; }        // e.g. "MAX_HECTARES", "MAX_VOLUME_PER_HA", "MAX_IRRIGABLE_PCT"
    public required string RuleDescription { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? NumericLimit { get; set; }            // e.g. 30 for max 30 ha, 9900 for m³/ha

    public string? Unit { get; set; }                    // "ha", "m3/ha", "pct", "m3"
    public string? GovernmentGazetteReference { get; set; }
    public bool IsActive { get; set; } = true;
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}
