using System.Security.Claims;

namespace dwa_ver_val.Services.Reporting;

/// <summary>
/// Produces org-scoped aggregate reports. The single seam for reporting data access —
/// a future star-schema mart would change only this implementation.
/// </summary>
public interface IReportingService
{
    Task<ReportTable> CatchmentProgressAsync(ReportFilter filter, ClaimsPrincipal user, CancellationToken ct);
    Task<ReportTable> LetterTrackingAsync(ReportFilter filter, ClaimsPrincipal user, CancellationToken ct);
    Task<ReportTable> ValidationSummaryAsync(ReportFilter filter, ClaimsPrincipal user, CancellationToken ct);

    // National oversight reports (NationalManager+; not WMA-row-scoped — AuditLog/PublicUser have no WMA FK).
    Task<ReportTable> UserActivityAsync(ReportFilter filter, CancellationToken ct);
    Task<ReportTable> PublicPortalUsageAsync(ReportFilter filter, CancellationToken ct);
    Task<ReportTable> IntegrationHealthAsync(ReportFilter filter, CancellationToken ct);
}
