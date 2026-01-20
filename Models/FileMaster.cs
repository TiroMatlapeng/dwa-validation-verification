using System.ComponentModel.DataAnnotations.Schema;

public class FileMaster
{
public Guid Id { get; set; }
public required string RegistrationNumber { get; set; }
public Guid PropertyAddressId { get; set; }
public required PropertyAddress PropertyAddress { get; set; }
public required string SurveyorGeneralCode { get; set; }
public required string PrimaryCatchment { get; set; }
public required string QuaternaryCatchment { get; set; }
public required string FarmName { get; set; }
public required int FarmNumber { get; set; }
public required string RegistrationDivision { get; set; }
public required string FarmPortion { get; set; }
public string? Notes { get; set; }
[Column(TypeName = "decimal(9, 6)")]
public decimal? Latitude { get; set; }
[Column(TypeName = "decimal(9, 6)")]
public decimal? Longitude { get; set; }
public string? NameUpdate { get; set; }
public string? PropertyIndex { get; set; }
public string? RegistrationStatusPrePublicParticipation { get; set; }
public string? RegistrationStatusPostPublicParticipation { get; set; }
public string? WarmsApplicant { get; set; }
public string? FileNumber { get; set; }
public DateOnly FileCreatedDate { get; set; }
public string? FileStatus { get; set; }
public string? RequirementDescription { get; set; }
public string? WARMSPrintsReceived { get; set; }
public string? Group { get; set; }
public string? LegalTypeGroup { get; set; }
public string? RiparianFarm { get; set; }
public bool? RegisteredForTakingWater { get; set; }
public bool? RegiteredForStoring { get; set; }
public bool? RegisteredForForestation { get; set; }
public string? BatchDescription { get; set; }
public string? LatestLetterTypeIssued { get; set; }
public required ValidationStatus GetValidationStatus { get; set; }
public required ApplicationUser ValidationPerson { get; set; }
public required DateOnly ValidationStartDate { get; set; }
public string? ValidationDescription { get; set; }
public ApplicationUser? CapturePerson { get; set; }

public int MyProperty { get; set; }
}
