using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.ViewModels;

/// <summary>
/// Captures the parent property being subdivided plus a list of child rows
/// the user fills out (one per new property to be created).
/// </summary>
public class SubdivideViewModel
{
    public Property? ParentProperty { get; set; }

    public List<SubdivideChildRow> Children { get; set; } = new();
}

/// <summary>
/// One child property row in the subdivide form. SGCode + PropertySize are required;
/// rows where SGCode is blank are treated as "skipped" and ignored on submit.
/// </summary>
public class SubdivideChildRow
{
    [Display(Name = "SG Code")]
    public string? SGCode { get; set; }

    [Display(Name = "Property Reference Number")]
    public string? PropertyReferenceNumber { get; set; }

    [Display(Name = "Property Size (ha)")]
    [Range(0.0001, double.MaxValue, ErrorMessage = "Property size must be greater than zero.")]
    public decimal PropertySize { get; set; }
}
