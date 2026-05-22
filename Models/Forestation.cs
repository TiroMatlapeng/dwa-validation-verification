using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Forestation
{
    public Guid ForestationId { get; set; }

    [Display(Name = "Property")]
    public Guid PropertyId { get; set; }
    public required Property Property { get; set;}

    [Display(Name = "Period")]
    public Guid? PeriodId { get; set; }
    public Period? Period { get; set; }

    [Display(Name = "Within GWCA")]
    public bool? WithinGWCA { get; set; }

    [Display(Name = "Qualifying Period SFRA Hectares (ha)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? QualifyPeriodSFRAHectares { get; set; }

    [Display(Name = "Current Period SFRA Hectares (ha)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? CurrentPeriodSFRAHectares { get; set; }

    [Display(Name = "Qualifying Period Volume (m³/a)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal QualifyPeriodVolume { get; set; }

    [Display(Name = "Species / Genus")]
    public string? Specie { get; set; }

    [Display(Name = "Water Resource")]
    public string? WaterResource { get; set; }

    [Display(Name = "Registered Hectares (ha)")]
    [Column(TypeName = "decimal(18, 2)")]
    public required decimal RegisteredHectares { get; set; }

    [Display(Name = "Registered Volume (m³/a)")]
    [Column(TypeName = "decimal(18, 2)")]
    public required decimal RegisteredVolume { get; set; }

    [Display(Name = "User Feedback — Entitlement Type")]
    public string? UserFeedbackEntitlementType { get; set; }

    [Display(Name = "User Feedback — Entitlement Reference")]
    public string? UserFeedbackEntitlementReference { get; set; }

    [Display(Name = "User Feedback — Entitlement Hectares (ha)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? UserFeedbackEntitlementHectares { get; set; }

    [Display(Name = "Comment on Feedback")]
    public string? CommentOnFeedback { get; set; }

    [Display(Name = "ELU Hectares (ha)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal ELUHectares { get; set; }

    [Display(Name = "ELU Volume (m³/a)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal ELUVolume { get; set; }

    [Display(Name = "Unlawful Hectares (ha)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnlawfulHectares { get; set; }

    [Display(Name = "Unlawful Volume (m³/a)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnlawfulVolume {get; set; }

    [Display(Name = "Last Calculated")]
    public DateTime? LastCalculatedAt { get; set; }

    [Display(Name = "Lawful Hectares (ha)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal LawfulHectares { get; set;}

    [Display(Name = "Lawful Volume (m³/a)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal LawfulVolume {get; set;}

    [Display(Name = "Unit for Volume Calculation")]
    public string?  UnitForVolumeCalculation { get; set; }

    [Display(Name = "Pre-1972 Hectares (ha)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal Pre1972Hectares { get; set; }

    [Display(Name = "Pre-1972 Volume (m³/a)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal Pre1972Volume { get; set; }

    [Display(Name = "SFRA Permit Number")]
    public string? SFRAPermitNumber { get; set; }

    [Display(Name = "SFRA Permit Hectares (ha)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal SFRAPermitHectares { get; set;}

    [Display(Name = "Comments on Data")]
    public string? CommentsOnData { get; set; }
}
