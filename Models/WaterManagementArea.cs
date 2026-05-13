using System.ComponentModel.DataAnnotations;

public class WaterManagementArea
{
    public Guid WmaId { get; set; }

    [Display(Name = "WMA Name")]
    public required string WmaName { get; set; }

    [Display(Name = "WMA Code")]
    public required string WmaCode { get; set; }

    [Display(Name = "Province")]
    public Guid ProvinceId { get; set; }
    public Province? Province { get; set; }
    public ICollection<OrganisationalUnit> OrganisationalUnits { get; set; } = new List<OrganisationalUnit>();
}
