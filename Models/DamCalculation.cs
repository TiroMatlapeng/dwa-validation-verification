using System.ComponentModel.DataAnnotations.Schema;

public class DamCalculation
{
    public Guid DamCalculationId { get; set; }
    public DateOnly CalculationDate {get; set;} 
    public required Property Property { get; set; }
    public Guid PropertyId { get; set; }
    public required string SateliteQualifyPeriod { get; set; }
    public required DateOnly SateliteSurveyDate { get; set; }
    public string? DamNumber { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public required decimal DamCapacity { get; set; }
    public required River River{ get; set; }
    public Guid RiverId { get; set; }
    public DamCalculationStatus DamCalculationStatus { get; set;}

}