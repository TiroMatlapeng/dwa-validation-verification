namespace dwa_ver_val.Services.Reporting;

/// <summary>
/// Common filter for all reports. Applied then intersected with the caller's org scope
/// via IScopedCaseQuery (a user cannot widen beyond their WMA by passing a different id).
/// </summary>
public record ReportFilter(
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    Guid? WaterManagementAreaId = null,
    Guid? CatchmentAreaId = null,
    string? ValidationStatus = null,
    Guid? OfficerUserId = null, // reserved for the deferred User Activity report (Plan A2); not yet applied to any query
    int Page = 1,
    int PageSize = 50);
