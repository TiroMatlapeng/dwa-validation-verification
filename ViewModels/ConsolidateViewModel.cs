using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.ViewModels;

/// <summary>
/// Captures the consolidation form: a list of source properties to mark as Consolidated
/// and the new-property fields (SGCode, PropertyReferenceNumber, optional size override).
/// </summary>
public class ConsolidateViewModel
{
    /// <summary>Active properties in the user's scope, used to render the table.</summary>
    public IReadOnlyList<Property> AvailableProperties { get; set; } = Array.Empty<Property>();

    /// <summary>IDs of source properties selected for consolidation.</summary>
    public Guid[] Sources { get; set; } = Array.Empty<Guid>();

    [Required]
    [Display(Name = "SG Code")]
    public string? SGCode { get; set; }

    [Display(Name = "Property Reference Number")]
    public string? PropertyReferenceNumber { get; set; }

    /// <summary>Optional override of the new property's size. Defaults to the sum of source sizes when null.</summary>
    [Display(Name = "Property Size (ha) — optional override")]
    public decimal? OverridePropertySize { get; set; }
}
