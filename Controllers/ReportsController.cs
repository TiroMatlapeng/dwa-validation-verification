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
        => RenderAsync(() => _reporting.CatchmentProgressAsync(filter, User, ct), format, ct);

    public Task<IActionResult> LetterTracking(ReportFilter filter, string? format, CancellationToken ct)
        => RenderAsync(() => _reporting.LetterTrackingAsync(filter, User, ct), format, ct);

    public Task<IActionResult> ValidationSummary(ReportFilter filter, string? format, CancellationToken ct)
        => RenderAsync(() => _reporting.ValidationSummaryAsync(filter, User, ct), format, ct);

    private async Task<IActionResult> RenderAsync(Func<Task<ReportTable>> build, string? format, CancellationToken ct)
    {
        var table = await build();

        if (string.IsNullOrWhiteSpace(format))
            return View("Report", table);

        var exporter = _exporters.FirstOrDefault(e => e.Format.Equals(format, StringComparison.OrdinalIgnoreCase));
        if (exporter is null)
            return View("Report", table); // unknown format → HTML

        using var ms = new MemoryStream();
        await exporter.WriteAsync(table, ms, ct);
        var fileName = SafeName(table.Title) + exporter.FileExtension;
        return File(ms.ToArray(), exporter.ContentType, fileName);
    }

    private static string SafeName(string title) =>
        new string(title.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)).ToArray())
            .Replace(' ', '_');
}
