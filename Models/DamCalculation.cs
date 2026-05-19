using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class DamCalculation
{
    public Guid DamCalculationId { get; set; }
    public DateOnly CalculationDate {get; set;}
    public required Property Property { get; set; }
    public Guid PropertyId { get; set; }
    public Guid? SateliteImageId { get; set; }
    public SateliteImage? SateliteImage { get; set; }
    public required DateOnly SateliteSurveyDate { get; set; }
    public string? DamNumber { get; set; }

    // Calculation method selector
    public string? CalculationMethod { get; set; }  // "Method1" | "Method2"

    // Method 1 (Wall Length) inputs
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? WallLength { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? Fetch { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? RiverDistance { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? ContourDifference { get; set; }

    // Method 2 (Area) inputs
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? DamArea { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? DamDepth { get; set; }

    // Shared
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? ShapeFactor { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public required decimal DamCapacity { get; set; }
    [Display(Name = "Last Calculated")]
    public DateTime? LastCalculatedAt { get; set; }
    public required River River{ get; set; }
    public Guid RiverId { get; set; }
    public DamCalculationStatus DamCalculationStatus { get; set;}
}
