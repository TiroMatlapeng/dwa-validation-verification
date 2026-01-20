public class GovernmentWaterControlArea
{
    public Guid WaterControlAreaId { get; set; }
    public required string GovernmentWaterControlAreaName { get; set; }
    public Address? WaterControlAddress { get; set; }
    public string? WaterControlPhoneNumber { get; set; }
}