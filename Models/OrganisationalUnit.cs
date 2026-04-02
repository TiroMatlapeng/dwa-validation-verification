public class OrganisationalUnit
{
    public Guid OrgUnitId { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; } // National, Provincial, Regional, CMA, Catchment
    public Guid? ProvinceId { get; set; }
    public Province? Province { get; set; }
    public Guid? WmaId { get; set; }
    public WaterManagementArea? WaterManagementArea { get; set; }
    public Guid? CatchmentAreaId { get; set; }
    public CatchmentArea? CatchmentArea { get; set; }
    public Guid? ParentOrgUnitId { get; set; }
    public OrganisationalUnit? ParentOrgUnit { get; set; }
    public ICollection<OrganisationalUnit> ChildOrgUnits { get; set; } = new List<OrganisationalUnit>();
    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
}
