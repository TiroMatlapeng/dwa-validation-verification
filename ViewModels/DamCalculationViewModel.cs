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
}
