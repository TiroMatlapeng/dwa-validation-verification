using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class FileMaster
{
    public Guid FileMasterId { get; set; }

    [Display(Name = "WARMS Registration Number")]
    public required string RegistrationNumber { get; set; }

    [Display(Name = "V&V Case Number")]
    public string? CaseNumber { get; set; } // Auto-generated V&V case number (business rule TBD)

    [Display(Name = "Property")]
    public Guid PropertyId { get; set; }
    public Property? Property { get; set; }

    [Display(Name = "Organisational Unit")]
    public Guid? OrgUnitId { get; set; }
    public OrganisationalUnit? OrgUnit { get; set; }

    [Display(Name = "SG Code")]
    public required string SurveyorGeneralCode { get; set; }

    [Display(Name = "Primary Catchment")]
    public required string PrimaryCatchment { get; set; }

    [Display(Name = "Quaternary Catchment")]
    public required string QuaternaryCatchment { get; set; } // Legacy — use CatchmentAreaId FK instead

    [Display(Name = "Catchment Area")]
    public Guid? CatchmentAreaId { get; set; }
    public CatchmentArea? CatchmentArea { get; set; }

    [Display(Name = "Farm Name")]
    public required string FarmName { get; set; }

    [Display(Name = "Farm Number")]
    public required int FarmNumber { get; set; }

    [Display(Name = "Registration Division")]
    public required string RegistrationDivision { get; set; }

    [Display(Name = "Farm Portion")]
    public required string FarmPortion { get; set; }

    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Property Index")]
    public string? PropertyIndex { get; set; }

    [Display(Name = "WARMS Applicant")]
    public string? WarmsApplicant { get; set; }

    [Display(Name = "File Number")]
    public string? FileNumber { get; set; }

    [Display(Name = "File Created Date")]
    public DateOnly FileCreatedDate { get; set; }

    [Display(Name = "File Status")]
    public string? FileStatus { get; set; }

    [Display(Name = "Registered for Taking Water")]
    public bool RegisteredForTakingWater { get; set; }

    [Display(Name = "Registered for Storing")]
    public bool RegisteredForStoring { get; set; }

    [Display(Name = "Registered for Forestation")]
    public bool RegisteredForForestation { get; set; }

    [Display(Name = "Batch Description")]
    public string? BatchDescription { get; set; }

    [Display(Name = "Validation Status")]
    public string? ValidationStatusName { get; set; }

    [Display(Name = "Validator")]
    public Guid? ValidatorId { get; set; }
    public ApplicationUser? Validator { get; set; }

    [Display(Name = "Data Capturer")]
    public Guid? CapturePersonId { get; set; }
    public ApplicationUser? CapturePerson { get; set; }

    [Display(Name = "Entitlement")]
    public Guid? EntitlementId { get; set; }
    public Entitlement? Entitlement { get; set; }

    // Assessment track: determines which control points are mandatory
    [Display(Name = "Assessment Track")]
    public string? AssessmentTrack { get; set; } // "S35_Verification", "S33_2_Declaration", "S33_3_Declaration"

    // Workflow
    public Guid? WorkflowInstanceId { get; set; }

    // Navigation collections
    public ICollection<Authorisation> Authorisations { get; set; } = new List<Authorisation>();
    public ICollection<LetterIssuance> LetterIssuances { get; set; } = new List<LetterIssuance>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<CaseComment> CaseComments { get; set; } = new List<CaseComment>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<Mapbook> Mapbooks { get; set; } = new List<Mapbook>();
}
