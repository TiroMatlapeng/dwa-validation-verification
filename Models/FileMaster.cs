using System.ComponentModel.DataAnnotations.Schema;

public class FileMaster
{
    public Guid FileMasterId { get; set; }
    public required string RegistrationNumber { get; set; }
    public Guid PropertyId { get; set; }
    public Property? Property { get; set; }
    public Guid? OrgUnitId { get; set; }
    public OrganisationalUnit? OrgUnit { get; set; }
    public required string SurveyorGeneralCode { get; set; }
    public required string PrimaryCatchment { get; set; }
    public required string QuaternaryCatchment { get; set; }
    public required string FarmName { get; set; }
    public required int FarmNumber { get; set; }
    public required string RegistrationDivision { get; set; }
    public required string FarmPortion { get; set; }
    public string? Notes { get; set; }
    public string? PropertyIndex { get; set; }
    public string? WarmsApplicant { get; set; }
    public string? FileNumber { get; set; }
    public DateOnly FileCreatedDate { get; set; }
    public string? FileStatus { get; set; }
    public bool RegisteredForTakingWater { get; set; }
    public bool RegisteredForStoring { get; set; }
    public bool RegisteredForForestation { get; set; }
    public string? BatchDescription { get; set; }
    public string? ValidationStatusName { get; set; }
    public Guid? ValidatorId { get; set; }
    public ApplicationUser? Validator { get; set; }
    public Guid? CapturePersonId { get; set; }
    public ApplicationUser? CapturePerson { get; set; }
    public Guid? EntitlementId { get; set; }
    public Entitlement? Entitlement { get; set; }

    // Workflow
    public Guid? WorkflowInstanceId { get; set; }

    // Navigation collections
    public ICollection<Authorisation> Authorisations { get; set; } = new List<Authorisation>();
    public ICollection<LetterIssuance> LetterIssuances { get; set; } = new List<LetterIssuance>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<CaseComment> CaseComments { get; set; } = new List<CaseComment>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
