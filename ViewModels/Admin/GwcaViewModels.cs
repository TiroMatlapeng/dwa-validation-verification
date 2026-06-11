using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.ViewModels.Admin;

/// <summary>Row in the GWCA admin list.</summary>
public class GwcaListItemViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GazetteReference { get; set; }
    public DateOnly? ProclamationDate { get; set; }
    public int ActiveRuleCount { get; set; }
}

/// <summary>Create/Edit form for a GovernmentWaterControlArea (the GWCA fields only).</summary>
public class GwcaFormViewModel
{
    public Guid Id { get; set; }

    [Required, Display(Name = "GWCA Name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Government Gazette Reference")]
    public string? GazetteReference { get; set; }

    [Display(Name = "Proclamation Date")]
    [DataType(DataType.Date)]
    public DateOnly? ProclamationDate { get; set; }

    [Display(Name = "Contact Phone Number")]
    public string? PhoneNumber { get; set; }
}

/// <summary>
/// The GWCA Edit/Detail page: the GWCA fields plus its proclamation rules, and a
/// blank "add rule" form. Rules are edited and deactivated/deleted inline.
/// </summary>
public class GwcaDetailViewModel
{
    public GwcaFormViewModel Gwca { get; set; } = new();
    public List<GwcaProclamationRule> Rules { get; set; } = new();
    public GwcaRuleFormViewModel NewRule { get; set; } = new();
}

/// <summary>Add/Edit form for a single GwcaProclamationRule.</summary>
public class GwcaRuleFormViewModel
{
    public Guid RuleId { get; set; }
    public Guid WaterControlAreaId { get; set; }

    [Required, Display(Name = "Rule Code")]
    public string RuleCode { get; set; } = string.Empty; // e.g. MAX_HECTARES

    [Required, Display(Name = "Description")]
    public string RuleDescription { get; set; } = string.Empty;

    [Display(Name = "Numeric Limit")]
    public decimal? NumericLimit { get; set; }

    [Display(Name = "Unit")]
    public string? Unit { get; set; } // ha, m3/ha, pct, m3

    [Display(Name = "Gazette Reference")]
    public string? GazetteReference { get; set; }

    [Display(Name = "Effective From")]
    [DataType(DataType.Date)]
    public DateOnly? EffectiveFrom { get; set; }

    [Display(Name = "Effective To")]
    [DataType(DataType.Date)]
    public DateOnly? EffectiveTo { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
