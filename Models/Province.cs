using System.ComponentModel.DataAnnotations;

public class Province
{
    public Guid ProvinceId { get; set; }

    [Display(Name = "Province Name")]
    public required string ProvinceName { get; set; }

    [Display(Name = "Province Code")]
    public required string ProvinceCode { get; set; }
    public ICollection<WaterManagementArea> WaterManagementAreas { get; set; } = new List<WaterManagementArea>();
}
