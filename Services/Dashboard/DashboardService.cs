using System.Security.Claims;
using dwa_ver_val.Services.Reporting;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Services.Dashboard;

public class DashboardService : IDashboardService
{
    private static readonly string[] PhaseOrder = { "Inception", "Validation", "Verification" };

    private readonly ApplicationDBContext _db;
    private readonly IScopedCaseQuery _scope;
    private readonly IReportingService _reporting;

    public DashboardService(ApplicationDBContext db, IScopedCaseQuery scope, IReportingService reporting)
    {
        _db = db; _scope = scope; _reporting = reporting;
    }

    public async Task<DashboardViewModel> GetAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cases = _scope.FilterFileMasters(_db.FileMasters.AsNoTracking(), user);
        var caseIds = cases.Select(f => f.FileMasterId);

        var vm = new DashboardViewModel
        {
            ScopeLabel = (user.IsInRole(DwsRoles.SystemAdmin) || user.IsInRole(DwsRoles.NationalManager))
                ? "National Overview" : "Regional Overview",
            TotalProperties = await _scope.FilterProperties(_db.Properties.AsNoTracking(), user).CountAsync(ct),
            CompletedCases = await cases.CountAsync(f => f.ValidationStatusName == "Completed", ct),
            InProcessCases = await cases.CountAsync(f => f.ValidationStatusName == "In Process", ct),
            OverdueLetters = await _db.LetterIssuances.AsNoTracking()
                .CountAsync(l => caseIds.Contains(l.FileMasterId)
                    && l.DueDate != null && l.DueDate < today && l.ResponseDate == null, ct),
            LettersPending = await _db.LetterIssuances.AsNoTracking()
                .CountAsync(l => caseIds.Contains(l.FileMasterId)
                    && l.DueDate != null && l.ResponseDate == null, ct),
        };

        // Validation-status distribution (donut)
        var statusCounts = await cases
            .GroupBy(f => f.ValidationStatusName ?? "Unknown")
            .Select(g => new ChartPoint(g.Key, g.Count()))
            .ToListAsync(ct);
        vm.ValidationStatusChart = statusCounts.OrderByDescending(p => p.Value).ToList();

        // Cases by V&V phase (bar): phase comes from the case's WorkflowInstance; cases with
        // no instance are "Not Started".
        var phaseCounts = await _db.WorkflowInstances.AsNoTracking()
            .Where(wi => caseIds.Contains(wi.FileMasterId))
            .GroupBy(wi => wi.CurrentWorkflowState!.Phase)
            .Select(g => new { Phase = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var total = await cases.CountAsync(ct);
        var withInstance = phaseCounts.Sum(x => x.Count);
        var phaseSeries = new List<ChartPoint> { new("Not Started", Math.Max(0, total - withInstance)) };
        foreach (var phase in PhaseOrder)
            phaseSeries.Add(new ChartPoint(phase, phaseCounts.FirstOrDefault(x => x.Phase == phase)?.Count ?? 0));
        vm.PhaseChart = phaseSeries;

        // Real letter tracking table (reused)
        vm.LetterTracking = await _reporting.LetterTrackingAsync(new ReportFilter(), user, ct);

        // My assigned tasks
        var uidClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(uidClaim, out var uid))
        {
            vm.MyTasks = await _db.WorkflowInstances.AsNoTracking()
                .Where(wi => wi.AssignedToId == uid && caseIds.Contains(wi.FileMasterId))
                .OrderBy(wi => wi.CreatedDate)
                .Select(wi => new DashboardTask(
                    wi.FileMasterId,
                    wi.FileMaster!.CaseNumber ?? wi.FileMaster.RegistrationNumber,
                    wi.CurrentWorkflowState!.StateName))
                .Take(10)
                .ToListAsync(ct);
        }

        return vm;
    }
}
