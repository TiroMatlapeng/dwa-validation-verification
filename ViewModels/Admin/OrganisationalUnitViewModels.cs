using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.ViewModels.Admin;

/// <summary>Row in the OrganisationalUnits admin list.</summary>
public class OrgUnitListItemViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? ProvinceName { get; set; }
    public string? WmaName { get; set; }
    public string? CatchmentName { get; set; }
    public string? ParentName { get; set; }
    public int UserCount { get; set; }
}

/// <summary>Create/Edit form for an OrganisationalUnit.</summary>
public class OrgUnitFormViewModel
{
    public Guid Id { get; set; }

    [Required, Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [Required, Display(Name = "Type")]
    public string Type { get; set; } = string.Empty; // National, Provincial, Regional, CMA, Catchment

    [Display(Name = "Province")]
    public Guid? ProvinceId { get; set; }

    [Display(Name = "Water Management Area")]
    public Guid? WmaId { get; set; }

    [Display(Name = "Catchment Area")]
    public Guid? CatchmentAreaId { get; set; }

    [Display(Name = "Parent Office")]
    public Guid? ParentOrgUnitId { get; set; }

    // Dropdown data
    public IEnumerable<string> AvailableTypes { get; set; } = Array.Empty<string>();
    public IEnumerable<LookupOption> AvailableProvinces { get; set; } = Array.Empty<LookupOption>();
    public IEnumerable<LookupOption> AvailableWmas { get; set; } = Array.Empty<LookupOption>();
    public IEnumerable<LookupOption> AvailableCatchments { get; set; } = Array.Empty<LookupOption>();
    public IEnumerable<LookupOption> AvailableParents { get; set; } = Array.Empty<LookupOption>();
}

/// <summary>Generic id/name option for admin dropdowns.</summary>
public class LookupOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
