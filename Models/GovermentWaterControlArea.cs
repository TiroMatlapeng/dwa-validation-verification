public class GovernmentWaterControlArea
{
    public Guid WaterControlAreaId { get; set; }
    public required string GovernmentWaterControlAreaName { get; set; }
    public string? GovernmentGazetteReference { get; set; }
    public DateOnly? ProclamationDate { get; set; }
    public Address? WaterControlAddress { get; set; }
    public string? WaterControlPhoneNumber { get; set; }

    public ICollection<GwcaProclamationRule> ProclamationRules { get; set; } = new List<GwcaProclamationRule>();
}