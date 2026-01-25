using System.ComponentModel.DataAnnotations.Schema;

public class Forestation
{
    public Guid ForestationId { get; set; }
    public required Property Property { get; set;}
    public bool? WithinGWCA { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? QualifyPeriodSFRAHectares { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? CurrentPeriodSFRAHectares { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal QualifyPeriodVolume { get; set; }
    public string? Specie { get; set; }
    public string? WaterResource { get; set; } 
    [Column(TypeName = "decimal(18, 2)")]
    public required decimal RegisteredHectares { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public required decimal RegisteredVolume { get; set; }
    public string? UserFeedbackEntitlementType { get; set; }
    public string? UserFeedbackEntitlementReference { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? UserFeedbackEntitlementHectares { get; set; }
    public string? CommentOnFeedback { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal ELUHectares { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal ELUVolume { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnlawfulHectares { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnlawfulVolume {get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal LawfulHectares { get; set;}
    [Column(TypeName = "decimal(18, 2)")]
    public decimal LawfulVolume {get; set;}
    public string?  UnitForVolumeCalculation { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal Pre1972Hectares { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal Pre1972Volume { get; set; }
    public string? SFRAPermitNumber { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal SFRAPermitHectares { get; set;}
    public string? CommentsOnData { get; set; }


}