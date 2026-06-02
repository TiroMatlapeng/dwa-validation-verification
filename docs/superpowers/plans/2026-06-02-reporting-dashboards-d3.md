# Role Dashboards + D3.js Visualization (Plan B) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current mock home dashboard with a real, org-scoped dashboard rendered with self-hosted **D3.js** (no Chart.js, no CDN), reusing Plan A's `ReportingService` and `IScopedCaseQuery`.

**Architecture:** A `DashboardService` produces an org-scoped `DashboardViewModel` (KPIs + two chart series + my-assigned-tasks), and reuses `IReportingService.LetterTrackingAsync` for the letter table. `HomeController.Index` calls it. The view renders KPI tiles + a D3 bar chart (cases by V&V phase) + a D3 donut (validation-status distribution), plus the real letter table and the current user's assigned tasks. D3 v7 is self-hosted under `wwwroot/lib/d3/`; a `wwwroot/js/reporting/charts.js` helper (global `DwsCharts`) draws charts in the DWS brand palette.

**Tech Stack:** ASP.NET Core 10 MVC, EF Core 10, D3.js v7 (self-hosted), xUnit + EF InMemory.

**Spec:** `docs/superpowers/specs/2026-06-02-reporting-and-analytics-design.md` (§7 dashboards, §8 D3 viz).

**Depends on:** Plan A (branch `feature/reporting-foundation`) — `IReportingService`, `IScopedCaseQuery`, `ReportFilter`, `ReportTable`. This plan is built on a branch stacked off `feature/reporting-foundation`.

**Scope (Plan B):** org-scoped KPIs, V&V-phase bar chart, validation-status donut, real letter table (reused), my-assigned-tasks, D3 self-host + helper, remove Chart.js/CDN + all mock dashboard data. **Out of scope:** geospatial choropleth (needs WMA/catchment GeoJSON — future), per-user saved layouts, dashboards for reports 4–6 (Plan A2).

---

## File Structure

**Create**
- `Services/Dashboard/DashboardModels.cs` — `DashboardViewModel`, `ChartPoint`, `DashboardTask`.
- `Services/Dashboard/IDashboardService.cs` + `DashboardService.cs`.
- `wwwroot/lib/d3/d3.min.js` — self-hosted D3 v7 (downloaded, committed).
- `wwwroot/js/reporting/charts.js` — `DwsCharts.barChart` / `DwsCharts.donutChart`.
- `Tests/Services/Dashboard/DashboardServiceTests.cs`.

**Modify**
- `Program.cs` — register `IDashboardService`.
- `Controllers/HomeController.cs` — `Index` uses `DashboardService`.
- `Views/Home/Index.cshtml` — real data + D3 charts; remove Chart.js CDN + mock panels.

---

## Task 1: DashboardService + models + DI

**Files:**
- Create: `Services/Dashboard/DashboardModels.cs`, `Services/Dashboard/IDashboardService.cs`, `Services/Dashboard/DashboardService.cs`
- Modify: `Program.cs`
- Test: `Tests/Services/Dashboard/DashboardServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Tests/Services/Dashboard/DashboardServiceTests.cs`:

```csharp
using System.Security.Claims;
using dwa_ver_val.Services.Dashboard;
using dwa_ver_val.Services.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace dwa_ver_val.Tests.Services.Dashboard;

public class DashboardServiceTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static DashboardService Svc(ApplicationDBContext db)
    {
        var scope = new ScopedCaseQuery(db);
        var reporting = new ReportingService(db, scope, new MemoryCache(new MemoryCacheOptions()));
        return new DashboardService(db, scope, reporting);
    }

    private static ClaimsPrincipal NationalManager(Guid uid) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, uid.ToString()),
            new Claim(ClaimTypes.Role, DwsRoles.NationalManager)
        }, "Test"));

    private static ClaimsPrincipal RegionalManager(Guid uid, Guid wmaId) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, uid.ToString()),
            new Claim(ClaimTypes.Role, DwsRoles.RegionalManager),
            new Claim("wmaId", wmaId.ToString())
        }, "Test"));

    private static Property Prop(ApplicationDBContext db, Guid wmaId)
    {
        var p = new Property { PropertyId = Guid.NewGuid(), PropertyReferenceNumber = "P", SGCode = "SG", WmaId = wmaId };
        db.Properties.Add(p);
        return p;
    }

    private static FileMaster Case(ApplicationDBContext db, Guid propertyId, string status) => new()
    {
        FileMasterId = Guid.NewGuid(), PropertyId = propertyId, ValidationStatusName = status,
        RegistrationNumber = "WARMS-1", SurveyorGeneralCode = "SG", PrimaryCatchment = "A21",
        QuaternaryCatchment = "A21A", FarmName = "F", FarmNumber = 1,
        RegistrationDivision = "TD", FarmPortion = "0", FileCreatedDate = new DateOnly(2026, 1, 1)
    };

    [Fact]
    public async Task Kpis_And_ValidationStatusChart_AreScopedAndCounted()
    {
        using var db = NewDb();
        var wmaA = Guid.NewGuid(); var wmaB = Guid.NewGuid();
        var pA = Prop(db, wmaA); var pB = Prop(db, wmaB);
        db.FileMasters.Add(Case(db, pA.PropertyId, "Completed"));
        db.FileMasters.Add(Case(db, pA.PropertyId, "In Process"));
        db.FileMasters.Add(Case(db, pB.PropertyId, "Completed")); // other WMA
        await db.SaveChangesAsync();

        var vm = await Svc(db).GetAsync(RegionalManager(Guid.NewGuid(), wmaA), CancellationToken.None);

        Assert.Equal(2, vm.CompletedOrInProcessTotal()); // only WMA-A's two cases
        Assert.Equal(1, vm.CompletedCases);
        Assert.Equal(1, vm.InProcessCases);
        Assert.Equal(1, vm.TotalProperties); // only WMA-A property
        Assert.Contains(vm.ValidationStatusChart, p => p.Label == "Completed" && p.Value == 1);
        Assert.Contains(vm.ValidationStatusChart, p => p.Label == "In Process" && p.Value == 1);
    }

    [Fact]
    public async Task PhaseChart_CountsCasesByWorkflowPhase_AndNotStarted()
    {
        using var db = NewDb();
        var wma = Guid.NewGuid();
        var p = Prop(db, wma);
        var validationState = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP3_WARMSEvaluation", Phase = "Validation", DisplayOrder = 9 };
        db.WorkflowStates.Add(validationState);

        var withInstance = Case(db, p.PropertyId, "In Process");
        var notStarted = Case(db, p.PropertyId, "Not Commenced");
        db.FileMasters.AddRange(withInstance, notStarted);
        db.WorkflowInstances.Add(new WorkflowInstance
        {
            WorkflowInstanceId = Guid.NewGuid(), FileMasterId = withInstance.FileMasterId,
            CurrentWorkflowStateId = validationState.WorkflowStateId, CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var vm = await Svc(db).GetAsync(NationalManager(Guid.NewGuid()), CancellationToken.None);

        Assert.Contains(vm.PhaseChart, x => x.Label == "Validation" && x.Value == 1);
        Assert.Contains(vm.PhaseChart, x => x.Label == "Not Started" && x.Value == 1);
    }

    [Fact]
    public async Task MyTasks_OnlyReturnsCasesAssignedToCurrentUser()
    {
        using var db = NewDb();
        var wma = Guid.NewGuid();
        var p = Prop(db, wma);
        var me = Guid.NewGuid(); var other = Guid.NewGuid();
        var state = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP5_GISAnalysis", Phase = "Validation", DisplayOrder = 11 };
        db.WorkflowStates.Add(state);

        var mine = Case(db, p.PropertyId, "In Process");
        var theirs = Case(db, p.PropertyId, "In Process");
        db.FileMasters.AddRange(mine, theirs);
        db.WorkflowInstances.AddRange(
            new WorkflowInstance { WorkflowInstanceId = Guid.NewGuid(), FileMasterId = mine.FileMasterId, CurrentWorkflowStateId = state.WorkflowStateId, AssignedToId = me, CreatedDate = DateTime.UtcNow },
            new WorkflowInstance { WorkflowInstanceId = Guid.NewGuid(), FileMasterId = theirs.FileMasterId, CurrentWorkflowStateId = state.WorkflowStateId, AssignedToId = other, CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var vm = await Svc(db).GetAsync(NationalManager(me), CancellationToken.None);

        var task = Assert.Single(vm.MyTasks);
        Assert.Equal("WARMS-1", task.CaseReference);
        Assert.Equal("CP5_GISAnalysis", task.CurrentState);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~DashboardServiceTests`
Expected: FAIL — `DashboardService` does not exist.

- [ ] **Step 3: Implement the models**

Create `Services/Dashboard/DashboardModels.cs`:

```csharp
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
```

- [ ] **Step 4: Implement the service interface + service**

Create `Services/Dashboard/IDashboardService.cs`:

```csharp
using System.Security.Claims;

namespace dwa_ver_val.Services.Dashboard;

public interface IDashboardService
{
    Task<DashboardViewModel> GetAsync(ClaimsPrincipal user, CancellationToken ct);
}
```

Create `Services/Dashboard/DashboardService.cs`:

```csharp
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
        var phaseSeries = new List<ChartPoint> { new("Not Started", total - withInstance) };
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
```

- [ ] **Step 5: Register the service in DI**

In `Program.cs`, next to the reporting registrations, add:

```csharp
builder.Services.AddScoped<dwa_ver_val.Services.Dashboard.IDashboardService, dwa_ver_val.Services.Dashboard.DashboardService>();
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~DashboardServiceTests`
Expected: PASS (3 tests). Then `dotnet build` → Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add Services/Dashboard/ Program.cs Tests/Services/Dashboard/DashboardServiceTests.cs
git commit -m "feat(dashboard): org-scoped DashboardService (KPIs, phase + status charts, my tasks)"
```

---

## Task 2: Self-host D3 + chart helper

**Files:**
- Create: `wwwroot/lib/d3/d3.min.js`, `wwwroot/js/reporting/charts.js`

- [ ] **Step 1: Download D3 v7 into the lib folder**

Run:
```bash
mkdir -p wwwroot/lib/d3
curl -sSL https://cdn.jsdelivr.net/npm/d3@7/dist/d3.min.js -o wwwroot/lib/d3/d3.min.js
```
Verify the file is non-trivial and is the UMD build (defines a global `d3`):
```bash
wc -c wwwroot/lib/d3/d3.min.js   # expect > 250000 bytes
head -c 200 wwwroot/lib/d3/d3.min.js
```
Expected: a large minified file. If the download is empty or an HTML error page, STOP and report BLOCKED (no network). (NuGet restore works in this environment, so network should be available.)

- [ ] **Step 2: Create the chart helper**

Create `wwwroot/js/reporting/charts.js`:

```javascript
// DWS dashboard charts built on D3 v7 (global `d3`). Exposes window.DwsCharts.
// Brand palette mirrors wwwroot/css/dws.css :root tokens — no generic/Tailwind colours.
(function () {
    "use strict";
    var PALETTE = ["#003d7a", "#0066b3", "#00838f", "#2e7d32", "#c49000", "#c62828", "#7db8e0", "#8892a0"];

    // data: [{ Label, Value }, ...]
    function barChart(selector, data, opts) {
        opts = opts || {};
        var el = document.querySelector(selector);
        if (!el || !window.d3) return;
        el.innerHTML = "";
        var width = el.clientWidth || 480, height = el.clientHeight || 220;
        var m = { top: 10, right: 10, bottom: 40, left: 40 };
        var svg = d3.select(el).append("svg").attr("width", width).attr("height", height);

        var x = d3.scaleBand().domain(data.map(function (d) { return d.Label; }))
            .range([m.left, width - m.right]).padding(0.25);
        var y = d3.scaleLinear().domain([0, d3.max(data, function (d) { return d.Value; }) || 1]).nice()
            .range([height - m.bottom, m.top]);

        svg.append("g").attr("transform", "translate(0," + (height - m.bottom) + ")")
            .call(d3.axisBottom(x)).selectAll("text").style("font-size", "11px");
        svg.append("g").attr("transform", "translate(" + m.left + ",0)").call(d3.axisLeft(y).ticks(5));

        svg.selectAll("rect.bar").data(data).enter().append("rect").attr("class", "bar")
            .attr("x", function (d) { return x(d.Label); })
            .attr("y", function (d) { return y(d.Value); })
            .attr("width", x.bandwidth())
            .attr("height", function (d) { return (height - m.bottom) - y(d.Value); })
            .attr("rx", 3)
            .attr("fill", function (d, i) { return (opts.colors || PALETTE)[i % (opts.colors || PALETTE).length]; });

        svg.selectAll("text.val").data(data).enter().append("text").attr("class", "val")
            .attr("x", function (d) { return x(d.Label) + x.bandwidth() / 2; })
            .attr("y", function (d) { return y(d.Value) - 4; })
            .attr("text-anchor", "middle").style("font-size", "11px").style("fill", "#1a1f2b")
            .text(function (d) { return d.Value; });
    }

    // data: [{ Label, Value }, ...]
    function donutChart(selector, data, opts) {
        opts = opts || {};
        var el = document.querySelector(selector);
        if (!el || !window.d3) return;
        el.innerHTML = "";
        var width = el.clientWidth || 320, height = el.clientHeight || 240;
        var radius = Math.min(width, height) / 2 - 10;
        var svg = d3.select(el).append("svg").attr("width", width).attr("height", height)
            .append("g").attr("transform", "translate(" + width / 2 + "," + height / 2 + ")");

        var pie = d3.pie().value(function (d) { return d.Value; }).sort(null);
        var arc = d3.arc().innerRadius(radius * 0.55).outerRadius(radius);
        var color = function (i) { return (opts.colors || PALETTE)[i % (opts.colors || PALETTE).length]; };

        svg.selectAll("path").data(pie(data)).enter().append("path")
            .attr("d", arc).attr("fill", function (d, i) { return color(i); })
            .attr("stroke", "#fff").style("stroke-width", "2px");

        var legend = d3.select(el).append("div").style("font-size", "11px").style("margin-top", "6px");
        data.forEach(function (d, i) {
            legend.append("span").style("display", "inline-block").style("margin-right", "12px")
                .html("<span style='display:inline-block;width:10px;height:10px;background:" + color(i) +
                    ";margin-right:4px;'></span>" + d.Label + " (" + d.Value + ")");
        });
    }

    window.DwsCharts = { barChart: barChart, donutChart: donutChart };
})();
```

- [ ] **Step 3: Commit**

```bash
git add wwwroot/lib/d3/d3.min.js wwwroot/js/reporting/charts.js
git commit -m "feat(dashboard): self-host D3 v7 + DWS-palette chart helper"
```

---

## Task 3: Rewrite the home dashboard view + controller

**Files:**
- Modify: `Controllers/HomeController.cs`
- Modify: `Views/Home/Index.cshtml`

- [ ] **Step 1: Update the controller to use DashboardService**

In `Controllers/HomeController.cs`, replace the `ApplicationDBContext` dependency usage in `Index` with `IDashboardService`. Update the constructor to inject `IDashboardService` (keep `ILogger`; you may drop the `ApplicationDBContext` field if it is no longer used elsewhere in the controller — check first). Replace the `Index` action body:

```csharp
    private readonly IDashboardService _dashboard;

    public HomeController(ILogger<HomeController> logger, IDashboardService dashboard)
    {
        _logger = logger;
        _dashboard = dashboard;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var vm = await _dashboard.GetAsync(User, ct);
        return View(vm);
    }
```
(Add `using dwa_ver_val.Services.Dashboard;`. Leave `Privacy` and `Error` actions unchanged. If `ApplicationDBContext` is no longer referenced, remove its field and constructor param.)

- [ ] **Step 2: Rewrite the dashboard view**

Replace the entire contents of `Views/Home/Index.cshtml` with:

```cshtml
@model dwa_ver_val.Services.Dashboard.DashboardViewModel
@using System.Text.Json
@using dwa_ver_val.Services.Documents
@{
    ViewData["Title"] = "Dashboard";
}

<div class="flex-between mb-16">
    <div>
        <div class="page-title">Dashboard</div>
        <div class="page-subtitle">@Model.ScopeLabel &mdash; @DateTime.Now.ToString("dd MMMM yyyy")</div>
    </div>
</div>

<!-- KPI Row (org-scoped) -->
<div class="kpi-row">
    <div class="kpi-card">
        <div class="kpi-label">Total Properties</div>
        <div class="kpi-value">@Model.TotalProperties</div>
        <div class="kpi-sub">in scope</div>
    </div>
    <div class="kpi-card success">
        <div class="kpi-label">Completed</div>
        <div class="kpi-value">@Model.CompletedCases</div>
        <div class="kpi-sub">validations completed</div>
    </div>
    <div class="kpi-card">
        <div class="kpi-label">In Process</div>
        <div class="kpi-value">@Model.InProcessCases</div>
        <div class="kpi-sub">cases in process</div>
    </div>
    <div class="kpi-card warn">
        <div class="kpi-label">Overdue Letters</div>
        <div class="kpi-value">@Model.OverdueLetters</div>
        <div class="kpi-sub">past due, no response</div>
    </div>
    <div class="kpi-card alert-kpi">
        <div class="kpi-label">Letters Pending</div>
        <div class="kpi-value">@Model.LettersPending</div>
        <div class="kpi-sub">awaiting response</div>
    </div>
</div>

<!-- Charts -->
<div class="panel-grid">
    <div class="card">
        <div class="card-header"><span class="card-title">Cases by V&amp;V Phase</span></div>
        <div id="phaseChart" style="height: 220px;"></div>
    </div>
    <div class="card">
        <div class="card-header"><span class="card-title">Validation Status</span></div>
        <div id="statusChart" style="height: 240px;"></div>
    </div>
</div>

<!-- Letter tracking (real) + My tasks -->
<div class="panel-grid">
    <div class="card">
        <div class="card-header"><span class="card-title">Letter Tracking</span></div>
        @if (Model.LetterTracking is { } lt && lt.Rows.Count > 0)
        {
            <table>
                <thead>
                    <tr>@foreach (var c in lt.Columns) { <th>@c.Header</th> }</tr>
                </thead>
                <tbody>
                    @foreach (var row in lt.Rows)
                    {
                        <tr>@foreach (var cell in row) { <td>@cell</td> }</tr>
                    }
                </tbody>
            </table>
        }
        else
        {
            <p style="color:#666;">No letters issued in scope yet.</p>
        }
    </div>
    <div class="card">
        <div class="card-header"><span class="card-title">My Assigned Cases</span>
            <span class="badge badge-blue">@Model.MyTasks.Count</span>
        </div>
        @if (Model.MyTasks.Count > 0)
        {
            <table>
                <thead><tr><th>Case</th><th>Current Step</th></tr></thead>
                <tbody>
                    @foreach (var t in Model.MyTasks)
                    {
                        <tr>
                            <td><a asp-controller="FileMaster" asp-action="Details" asp-route-id="@t.FileMasterId">@t.CaseReference</a></td>
                            <td>@t.CurrentState</td>
                        </tr>
                    }
                </tbody>
            </table>
        }
        else
        {
            <p style="color:#666;">No cases currently assigned to you.</p>
        }
    </div>
</div>

@section Scripts {
    <script src="~/lib/d3/d3.min.js"></script>
    <script src="~/js/reporting/charts.js" asp-append-version="true"></script>
    <script>
        (function () {
            var phase = @Html.Raw(JsonSerializer.Serialize(Model.PhaseChart));
            var status = @Html.Raw(JsonSerializer.Serialize(Model.ValidationStatusChart));
            if (window.DwsCharts) {
                DwsCharts.barChart('#phaseChart', phase);
                DwsCharts.donutChart('#statusChart', status);
            }
        })();
    </script>
}
```

Note: `JsonSerializer.Serialize` emits the records with PascalCase property names (`Label`, `Value`) which the chart helper reads. Confirm the serialized JSON uses `Label`/`Value` (System.Text.Json default keeps property casing as declared for records → `Label`,`Value`). If the app has configured a camelCase JSON policy for MVC, that does NOT affect `JsonSerializer.Serialize` with default options here, so `Label`/`Value` are correct.

- [ ] **Step 3: Build + verify no Chart.js/CDN remains**

Run: `dotnet build` → Build succeeded.
Run: `grep -rn "chart.js\|cdn.jsdelivr\|cdnjs\|new Chart(" Views/Home/Index.cshtml` → expect NO matches (all Chart.js/CDN removed).

- [ ] **Step 4: Commit**

```bash
git add Controllers/HomeController.cs Views/Home/Index.cshtml
git commit -m "feat(dashboard): real org-scoped home dashboard with D3 charts; drop Chart.js/CDN + mock data"
```

---

## Task 4: Verification

- [ ] **Step 1: Build**

Run: `dotnet build` → 0 errors.

- [ ] **Step 2: Full regression**

Run: `dotnet test` → all pass (existing + the 3 new DashboardService tests). SQL Server must be running for the integration tests.

- [ ] **Step 3: Manual check (app running, needs SQL Server)**

Run `dotnet run`, log in, land on the Dashboard. Confirm:
- KPI tiles show real numbers (not the old mock 1,204 etc.).
- The "Cases by V&V Phase" bar chart and "Validation Status" donut render via D3 in DWS colours.
- The Letter Tracking table shows real data (or the empty state).
- "My Assigned Cases" lists only cases assigned to the logged-in user; links open the case.
- Log in as a RegionalManager and confirm KPI/chart numbers reflect only their WMA (org-scoping), and as NationalManager that they reflect everything.
- View source / network tab: D3 loads from `/lib/d3/d3.min.js` (self-hosted, no CDN request).

- [ ] **Step 4: Final commit (if any tweaks)**

```bash
git add -A
git commit -m "test(dashboard): real D3 dashboard verified"
```

---

## Self-Review Notes (author)

- **Spec coverage:** §7 role-aware org-scoped dashboard (KPIs + filters-by-scope; explicit per-WMA via IScopedCaseQuery; no per-user saved layouts — deferred), §8 D3 self-hosted helper in DWS palette (bar + donut; choropleth deferred — needs GeoJSON). Reuses ReportingService for the letter table (the single seam).
- **Org-scoping:** every KPI/chart/my-tasks query goes through `IScopedCaseQuery` (cases) or scoped property/letter sub-queries keyed on scoped case ids — a RegionalManager sees only their WMA. Tested.
- **EF translation note:** the phase query groups `WorkflowInstances` (filtered by scoped case-id subquery) on `CurrentWorkflowState.Phase` — a single GROUP BY over an INNER JOIN; "Not Started" is computed in memory from `total - withInstance` (no left-join-group needed). MyTasks navigates `wi.FileMaster`/`wi.CurrentWorkflowState` — standard joins. Validate against live SQL Server during Task 4 / validation (InMemory can't prove translation).
- **No mock data / no CDN:** Task 3 removes all fabricated dashboard content and the Chart.js CDN; charts are real and D3 is self-hosted.
- **Type consistency:** `ChartPoint(Label, Value)`, `DashboardTask(FileMasterId, CaseReference, CurrentState)`, `DashboardViewModel`, `IDashboardService.GetAsync(ClaimsPrincipal, CancellationToken)` used identically across service, controller, view, tests.
- **Deferred:** choropleth map, per-user layouts, dashboards for reports 4–6 (Plan A2).
