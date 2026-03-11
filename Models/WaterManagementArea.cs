public class WaterManagementArea
{
    public Guid WmaId { get; set; }
    public required string WmaName { get; set; }
    public required string WmaCode { get; set; }
    public Guid ProvinceId { get; set; }
    public Province? Province { get; set; }
    public ICollection<OrganisationalUnit> OrganisationalUnits { get; set; } = new List<OrganisationalUnit>();
}
