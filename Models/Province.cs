public class Province
{
    public Guid ProvinceId { get; set; }
    public required string ProvinceName { get; set; }
    public required string ProvinceCode { get; set; }
    public ICollection<WaterManagementArea> WaterManagementAreas { get; set; } = new List<WaterManagementArea>();
}
