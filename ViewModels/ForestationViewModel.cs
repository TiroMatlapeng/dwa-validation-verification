using System.ComponentModel.DataAnnotations;

public class ForestationViewModel
{
    public Guid ForestationId { get; set; }

    [Required]
    public Guid PropertyId { get; set; }

    public Guid? PeriodId { get; set; }

    [Display(Name = "Within GWCA")]
    public bool? WithinGWCA { get; set; }

    [Display(Name = "Species / Genus")]
    public string? Specie { get; set; }

    [Display(Name = "Water Resource")]
    public string? WaterResource { get; set; }

    [Display(Name = "Qualifying Period SFRA Hectares (ha)")]
    public decimal? QualifyPeriodSFRAHectares { get; set; }

    [Display(Name = "Current Period SFRA Hectares (ha)")]
    public decimal? CurrentPeriodSFRAHectares { get; set; }

    [Display(Name = "Qualifying Period Volume (m³/a)")]
    public decimal QualifyPeriodVolume { get; set; }

    [Required]
    [Display(Name = "Registered Hectares (ha)")]
    public decimal RegisteredHectares { get; set; }

    [Required]
    [Display(Name = "Registered Volume (m³/a)")]
    public decimal RegisteredVolume { get; set; }

    [Display(Name = "Pre-1972 Hectares (ha)")]
    public decimal Pre1972Hectares { get; set; }

    [Display(Name = "Pre-1972 Volume (m³/a)")]
    public decimal Pre1972Volume { get; set; }

    [Display(Name = "SFRA Permit Number")]
    public string? SFRAPermitNumber { get; set; }

    [Display(Name = "SFRA Permit Hectares (ha)")]
    public decimal SFRAPermitHectares { get; set; }

    [Display(Name = "ELU Hectares (ha)")]
    public decimal ELUHectares { get; set; }

    [Display(Name = "ELU Volume (m³/a)")]
    public decimal ELUVolume { get; set; }

    [Display(Name = "Lawful Hectares (ha)")]
    public decimal LawfulHectares { get; set; }

    [Display(Name = "Lawful Volume (m³/a)")]
    public decimal LawfulVolume { get; set; }

    [Display(Name = "Unlawful Hectares (ha)")]
    public decimal UnlawfulHectares { get; set; }

    [Display(Name = "Unlawful Volume (m³/a)")]
    public decimal UnlawfulVolume { get; set; }

    [Display(Name = "Unit for Volume Calculation")]
    public string? UnitForVolumeCalculation { get; set; }

    [Display(Name = "User Feedback — Entitlement Type")]
    public string? UserFeedbackEntitlementType { get; set; }

    [Display(Name = "User Feedback — Entitlement Reference")]
    public string? UserFeedbackEntitlementReference { get; set; }

    [Display(Name = "User Feedback — Entitlement Hectares (ha)")]
    public decimal? UserFeedbackEntitlementHectares { get; set; }

    [Display(Name = "Comment on Feedback")]
    public string? CommentOnFeedback { get; set; }

    [Display(Name = "Comments on Data")]
    public string? CommentsOnData { get; set; }

    // Populated for Index display
    public string? PeriodName { get; set; }
}
