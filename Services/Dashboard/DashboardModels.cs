namespace dwa_ver_val.Services.Dashboard;

/// <summary>One labelled value for a D3 chart series.</summary>
public record ChartPoint(string Label, int Value);

/// <summary>A case assigned to the current user, surfaced on the dashboard.</summary>
public record DashboardTask(Guid FileMasterId, string CaseReference, string CurrentState);

/// <summary>Everything the home dashboard renders, already org-scoped to the caller.</summary>
public class DashboardViewModel
{
    public string ScopeLabel { get; set; } = "National Overview";

    // KPI tiles
    public int TotalProperties { get; set; }
    public int CompletedCases { get; set; }
    public int InProcessCases { get; set; }
    public int OverdueLetters { get; set; }
    public int LettersPending { get; set; }

    // Charts
    public IReadOnlyList<ChartPoint> PhaseChart { get; set; } = new List<ChartPoint>();
    public IReadOnlyList<ChartPoint> ValidationStatusChart { get; set; } = new List<ChartPoint>();

    // Real letter tracking table (reused from ReportingService)
    public dwa_ver_val.Services.Reporting.ReportTable? LetterTracking { get; set; }

    // Current user's assigned cases
    public IReadOnlyList<DashboardTask> MyTasks { get; set; } = new List<DashboardTask>();

    public int CompletedOrInProcessTotal() => CompletedCases + InProcessCases;
}
