using dwa_ver_val.Services.Reporting;
using dwa_ver_val.Services.Reporting.Export;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dwa_ver_val.Controllers;

[Authorize(Policy = DwsPolicies.CanRead)]
public class ReportsController : Controller
{
    private readonly IReportingService _reporting;
    private readonly IReadOnlyList<IReportExporter> _exporters;

    public ReportsController(IReportingService reporting, IEnumerable<IReportExporter> exporters)
    {
        _reporting = reporting;
        _exporters = exporters.ToList();
    }

    public IActionResult Index() => View();

    public Task<IActionResult> CatchmentProgress(ReportFilter filter, string? format, CancellationToken ct)
        => RenderAsync(filter, () => _reporting.CatchmentProgressAsync(filter, User, ct), format, ct);

    public Task<IActionResult> LetterTracking(ReportFilter filter, string? format, CancellationToken ct)
        => RenderAsync(filter, () => _reporting.LetterTrackingAsync(filter, User, ct), format, ct);

    public Task<IActionResult> ValidationSummary(ReportFilter filter, string? format, CancellationToken ct)
        => RenderAsync(filter, () => _reporting.ValidationSummaryAsync(filter, User, ct), format, ct);

    [Authorize(Policy = DwsPolicies.CanViewNationalReports)]
    public Task<IActionResult> UserActivity(ReportFilter filter, string? format, CancellationToken ct)
        => RenderAsync(filter, () => _reporting.UserActivityAsync(filter, ct), format, ct);

    [Authorize(Policy = DwsPolicies.CanViewNationalReports)]
    public Task<IActionResult> PublicPortalUsage(ReportFilter filter, string? format, CancellationToken ct)
        => RenderAsync(filter, () => _reporting.PublicPortalUsageAsync(filter, ct), format, ct);

    [Authorize(Policy = DwsPolicies.CanViewNationalReports)]
    public Task<IActionResult> IntegrationHealth(ReportFilter filter, string? format, CancellationToken ct)
        => RenderAsync(filter, () => _reporting.IntegrationHealthAsync(filter, ct), format, ct);

    private async Task<IActionResult> RenderAsync(ReportFilter filter, Func<Task<ReportTable>> build, string? format, CancellationToken ct)
    {
        var table = await build();

        var exporter = string.IsNullOrWhiteSpace(format)
            ? null
            : _exporters.FirstOrDefault(e => e.Format.Equals(format, StringComparison.OrdinalIgnoreCase));

        if (exporter is null)
        {
            ViewData["Filter"] = filter;     // so export links can carry the active filter
            return View("Report", table);
        }

        using var ms = new MemoryStream();
        await exporter.WriteAsync(table, ms, ct);
        var fileName = SafeName(table.Title) + exporter.FileExtension;
        return File(ms.ToArray(), exporter.ContentType, fileName);
    }

    private static string SafeName(string title) =>
        new string(title.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)).ToArray())
            .Replace(' ', '_');
}
