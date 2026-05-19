using System.ComponentModel.DataAnnotations;

public class DamCalculationViewModel
{
    public Guid DamCalculationId { get; set; }

    [Required]
    public Guid PropertyId { get; set; }

    [Required]
    [Display(Name = "River")]
    public Guid RiverId { get; set; }

    public Guid? SateliteImageId { get; set; }

    [Required]
    [Display(Name = "Calculation Date")]
    public DateOnly CalculationDate { get; set; }

    [Required]
    [Display(Name = "Satellite Survey Date")]
    public DateOnly SateliteSurveyDate { get; set; }

    [Display(Name = "Dam Number")]
    public string? DamNumber { get; set; }

    [Required]
    [Display(Name = "Dam Capacity (m³)")]
    public decimal DamCapacity { get; set; }

    [Required]
    [Display(Name = "Status")]
    public DamCalculationStatus DamCalculationStatus { get; set; }

    // Populated for Index display
    public string? RiverName { get; set; }

    // Appendix D calculation inputs
    [Display(Name = "Calculation Method")]
    public string? CalculationMethod { get; set; }

    [Display(Name = "Wall Length (m)")]
    public decimal? WallLength { get; set; }

    [Display(Name = "Fetch (m)")]
    public decimal? Fetch { get; set; }

    [Display(Name = "River Distance R1 (m)")]
    public decimal? RiverDistance { get; set; }

    [Display(Name = "Contour Difference C1 (m)")]
    public decimal? ContourDifference { get; set; }

    [Display(Name = "Dam Area (ha)")]
    public decimal? DamArea { get; set; }

    [Display(Name = "Dam Depth (m)")]
    public decimal? DamDepth { get; set; }

    [Display(Name = "Shape Factor")]
    public decimal? ShapeFactor { get; set; }
}
