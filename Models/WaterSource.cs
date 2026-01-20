public class WaterSource
{
    public Guid WaterSourceId { get; set; }
    public required string WaterSourceName { get; set; }
    public WaterSourceType WaterSourceType { get; set; }
    
}