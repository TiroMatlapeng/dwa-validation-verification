using System.ComponentModel.DataAnnotations;

public class OrganisationalUnit
{
    public Guid OrgUnitId { get; set; }

    [Display(Name = "Name")]
    public required string Name { get; set; }

    [Display(Name = "Type")]
    public required string Type { get; set; } // National, Provincial, Regional, CMA, Catchment

    [Display(Name = "Province")]
    public Guid? ProvinceId { get; set; }
    public Province? Province { get; set; }

    [Display(Name = "Water Management Area")]
    public Guid? WmaId { get; set; }
    public WaterManagementArea? WaterManagementArea { get; set; }

    [Display(Name = "Catchment Area")]
    public Guid? CatchmentAreaId { get; set; }
    public CatchmentArea? CatchmentArea { get; set; }

    [Display(Name = "Parent Office")]
    public Guid? ParentOrgUnitId { get; set; }
    public OrganisationalUnit? ParentOrgUnit { get; set; }
    public ICollection<OrganisationalUnit> ChildOrgUnits { get; set; } = new List<OrganisationalUnit>();
    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
}
