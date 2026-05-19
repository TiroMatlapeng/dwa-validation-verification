using System.ComponentModel.DataAnnotations.Schema;

public class SfraSpeciesRate
{
    public Guid SfraSpeciesRateId { get; set; }
    public required string SpeciesName { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public required decimal RateM3PerHaPerAnnum { get; set; }

    public string? Notes { get; set; }
}
