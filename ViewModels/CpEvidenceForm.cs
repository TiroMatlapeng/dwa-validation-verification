/// <summary>
/// Form posted by the inline per-CP evidence form on _WorkflowPanel.cshtml.
/// Captures the evidence flag/date/venue/attendance the matching guard expects
/// before advancement to the next control point is allowed.
/// </summary>
public class CpEvidenceForm
{
    public bool? SpatialInfoConfirmed { get; set; }
    public bool? WarmsReviewed { get; set; }
    public bool? AdditionalInfoReviewed { get; set; }
    public bool? PrePublicReviewApproved { get; set; }
    public DateTime? StakeholderWorkshopDate { get; set; }
    public string? StakeholderWorkshopVenue { get; set; }
    public int? StakeholderWorkshopAttendance { get; set; }
}
