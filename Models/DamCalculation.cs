using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class DamCalculation
{
    public Guid DamCalculationId { get; set; }

    [Display(Name = "Calculation Date")]
    public DateOnly CalculationDate {get; set;}
    public required Property Property { get; set; }

    [Display(Name = "Property")]
    public Guid PropertyId { get; set; }

    [Display(Name = "Satellite Image")]
    public Guid? SateliteImageId { get; set; }
    public SateliteImage? SateliteImage { get; set; }

    [Display(Name = "Satellite Survey Date")]
    public required DateOnly SateliteSurveyDate { get; set; }

    [Display(Name = "Dam Number")]
    public string? DamNumber { get; set; }

    [Display(Name = "Calculation Method")]
    public string? CalculationMethod { get; set; }  // "Method1" | "Method2"

    // Method 1 (Wall Length) inputs
    [Display(Name = "Wall Length (m)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? WallLength { get; set; }

    [Display(Name = "Fetch (m)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? Fetch { get; set; }

    [Display(Name = "River Distance R1 (m)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? RiverDistance { get; set; }

    [Display(Name = "Contour Difference C1 (m)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? ContourDifference { get; set; }

    // Method 2 (Area) inputs
    [Display(Name = "Dam Area (ha)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? DamArea { get; set; }

    [Display(Name = "Dam Depth (m)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? DamDepth { get; set; }

    // Shared
    [Display(Name = "Shape Factor")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? ShapeFactor { get; set; }

    [Display(Name = "Dam Capacity (m³)")]
    [Column(TypeName = "decimal(18, 2)")]
    public required decimal DamCapacity { get; set; }

    [Display(Name = "Last Calculated")]
    public DateTime? LastCalculatedAt { get; set; }
    public required River River{ get; set; }

    [Display(Name = "River")]
    public Guid RiverId { get; set; }

    [Display(Name = "Status")]
    public DamCalculationStatus DamCalculationStatus { get; set;}
}
