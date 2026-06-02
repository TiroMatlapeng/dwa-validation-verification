# Reporting & Analytics (WP6 core) — Design

**Date:** 2026-06-02
**Status:** Approved (design) — pending written-spec review
**Author:** V&V engineering (with Claude)
**Scope note:** This spec covers the **core** of WP6 — Standard Reports (6.1),
Dashboard enhancement (6.3), Exports (6.4), and large-dataset performance (6.5).
The **Ad-hoc Query Builder (6.2)** is deferred to its own spec.

## 1. Problem & Intent

The V&V system must give DWS/CMA managers and officers progress, compliance, and
workload visibility across ~500,000 property records, scoped by organisational unit
(province → WMA → catchment). Today only a basic 5-KPI home dashboard exists
(`HomeController.Index` via `ViewBag`); there is no `ReportingService`, no charting,
and no Excel/CSV export. QuestPDF is already in the project (used for letters).

The governing requirement is the V&V Guide **Appendix C Project Control Reports**, as
captured in `docs/DATA-FLOW-AND-WORKFLOW-DESIGN.md` §14.2–14.3.

## 2. Scope

**In scope**
- `ReportingService` abstraction with one strongly-typed method per report.
- The six Appendix C standard reports.
- Role-based dashboards with filters (date range, WMA, catchment, status, officer).
- Exports: Excel (ClosedXML), PDF (QuestPDF), CSV (hand-rolled).
- Performance: on-demand aggregation + targeted indexes + short-lived caching +
  streamed/paginated exports.
- D3.js visualization layer (self-hosted), DWS brand palette.
- Tests for aggregation correctness, scoping, exports, and authorization.

**Out of scope (deferred)**
- Ad-hoc query builder (6.2) — separate spec.
- Star-schema data mart + ETL + open-source BI — see §10 future evolution.
- Scheduled / emailed report delivery (Appendix C frequencies) — see §10.
- Per-user saved dashboard layouts — see §10.

## 3. Decisions (locked)

| # | Decision |
|---|----------|
| R1 | Spec the WP6 core now; defer the ad-hoc query builder. |
| R2 | Performance: **on-demand** tuned `GROUP BY` queries + targeted indexes + short-lived `IMemoryCache`; detail rows paginated; exports streamed. Add pre-computed summaries only if a specific report proves slow. |
| R3 | Dashboards: **role-based defaults + filters**, org-scoped. No per-user saved layouts this iteration. |
| R4 | Visualization: **D3.js** (open source, BSD), self-hosted in `wwwroot/lib`, DWS palette. A reusable chart-helper module controls per-chart effort. |
| R5 | Exports: PDF via QuestPDF (reuse), Excel via **ClosedXML** (new MIT dependency), CSV hand-rolled (no new dependency). |
| R6 | Every report and dashboard query is org-scoped via `IScopedCaseQuery`. |
| R7 | Later-stage BI is **open source** (bespoke D3 analytics portal, optionally Superset/Metabase) — **not** Power BI. Commercial rationale in §10. |

## 4. Architecture

```
ReportsController / Dashboard            <- thin HTTP boundary, [Authorize], chooses format
    -> ReportingService                  <- ALL aggregation; org-scoped; takes ReportFilter
        -> ApplicationDBContext          <- tuned GROUP BY against indexed columns
        -> IMemoryCache                  <- short-lived, keyed by report+filter+scope
    -> IReportExporter (Excel|Pdf|Csv)   <- renders a tabular ReportTable to a stream
Razor views + D3.js (wwwroot/lib/d3)     <- charts via reusable chart-helper module
```

- **`ReportingService` is the swap seam.** When the data-mart arrives (§10), only the
  service's data source changes; controllers, exporters, and views are untouched.
- Each report method returns a strongly-typed view model that includes a generic
  `ReportTable` (column metadata + rows) used by the exporters, plus any
  chart-specific series for the D3 layer.

### 4.1 Shared `ReportFilter`

```csharp
public record ReportFilter(
    DateOnly? DateFrom, DateOnly? DateTo,
    Guid? WaterManagementAreaId, Guid? CatchmentAreaId,
    string? ValidationStatus, Guid? OfficerUserId,
    int Page = 1, int PageSize = 50);
```

Applied uniformly, then intersected with the caller's org scope via `IScopedCaseQuery`
(a RegionalManager cannot widen beyond their WMA by passing a different `WmaId`).

## 5. The Six Standard Reports (Appendix C §14.2)

| # | Report | Grain / Content | Frequency | RBAC |
|---|--------|-----------------|-----------|------|
| 1 | **Catchment Progress** | per catchment: total records, status breakdown per control point, completion % | Weekly | ReadOnly+ |
| 2 | **Letter Tracking** | letters issued, responses received, overdue, return-to-sender | Weekly | ReadOnly+ |
| 3 | **Validation Summary** | properties validated, ELU volumes determined (by catchment) | Monthly | ReadOnly+ |
| 4 | **User Activity** | actions per officer, cases completed | Monthly | RegionalManager+ |
| 5 | **Public Portal Usage** | registrations, logins, documents signed | Monthly | RegionalManager+ |
| 6 | **Integration Health** | sync status, errors, pending updates | Daily | NationalManager+ |

**Data sources per report**
1. `FileMaster` joined to `Property`→`CatchmentArea`, grouped by catchment and
   `WorkflowInstance.CurrentWorkflowState`; completion % from states past CP9/CP11.
2. `LetterIssuance` (+ `LetterType`): counts by issued/response/overdue
   (`DueDate < today && ResponseDate == null`) — generalises the existing home KPIs.
3. `FileMaster` + linked `Entitlement` volumes, grouped by catchment.
4. `AuditLog` grouped by `ApplicationUserId`/`UserName` + completed-case counts.
5. `PublicUser`, portal `AuditLog` events, `DigitalSignature`/`SignatureRequest`.
6. `AuditLog` integration actions (`IntegrationSent`/`IntegrationReceived`). **Built as
   a shell now; largely empty until `IntegrationService`/eWULAAS lands** (WARMS is
   inbound-only; eWULAAS push not yet implemented). Flagged clearly in the UI.

## 6. Exports

`IReportExporter` renders a generic `ReportTable` to a stream:

```csharp
public interface IReportExporter
{
    string Format { get; }          // "xlsx" | "pdf" | "csv"
    string ContentType { get; }
    Task WriteAsync(ReportTable table, Stream output, CancellationToken ct);
}
```

- **CsvExporter** — hand-rolled, RFC-4180 quoting; streamed row-by-row.
- **ExcelExporter** — ClosedXML; header styling, frozen header row, auto-fit.
- **PdfExporter** — QuestPDF; A4 landscape, DWS header/footer, paginated table; reuses
  the letter PDF infrastructure conventions.

Each report action accepts `?format=html|xlsx|pdf|csv`. Non-HTML formats stream the
full filtered (scope-limited) result set; HTML paginates. Exports are resolved from DI
by `Format`, so adding a format later is additive.

## 7. Dashboards (role-based + filters)

Extend the internal dashboard into role-tailored defaults, all org-scoped, all
filterable (date / WMA / catchment / status):

- **NationalManager / SystemAdmin** — national rollups across all WMAs; integration health.
- **RegionalManager** — their WMA: catchment progress, letter tracking, officer workload.
- **Validator / Capturer** — their own caseload, overdue tasks, pending portal actions.
- **ReadOnly** — read-only view of the above scoped set.

Charts via the **D3.js** helper module (§8). The existing 5 home KPIs are folded in
as the baseline tiles. No per-user saved layouts this iteration.

## 8. Visualization layer — D3.js (open source)

- **D3 v7**, self-hosted under `wwwroot/lib/d3/` (no CDN — government deployment).
- A reusable ES module `wwwroot/js/reporting/charts.js` exposes a small, documented
  API: `kpiTile`, `barChart`, `lineChart`, `donutChart`, and `choropleth`
  (catchment/WMA geospatial — the reason D3 over a higher-level chart lib). Each takes
  a container selector + a typed data series + DWS-palette options.
- **DWS brand palette only** — colours sourced from the shared palette, no generic
  defaults (per project convention).
- Views pass server-built series to the helpers as JSON; no business logic in JS.

## 9. Performance (the 500k decision)

- **On-demand aggregation**: all report queries are `GROUP BY` projections (counts/sums),
  never load entities into memory; aggregates are cheap at 500k with the right indexes.
- **Targeted indexes** (one migration, `AddReportingIndexes`):
  - `FileMaster(ValidationStatusName)`, `FileMaster(WorkflowInstanceId)`,
  - `LetterIssuance(DueDate)`, `LetterIssuance(ResponseDate)`, `LetterIssuance(LetterTypeId)`,
  - `Property(CatchmentAreaId)`, `Property(WaterManagementAreaId)`,
  - `AuditLog(ApplicationUserId, Timestamp)`, `AuditLog(Action, Timestamp)`.
  (Confirm exact columns against current schema during planning.)
- **Caching**: `IMemoryCache`, short TTL (e.g. 60–120 s), key = report + serialized
  filter + scope signature. Dashboards cache per role+scope.
- **Detail rows** paginated server-side (`ReportFilter.Page/PageSize`); **exports**
  streamed, never fully buffered in memory.
- Pre-computed summaries are **not** built now; added only if a specific report is
  measured slow.

## 10. Future Evolution (documented, not built)

### 10.1 Reporting data mart (star schema)
A small Kimball star schema in a separate reporting schema/database, fed by scheduled
ETL from the OLTP. Because all aggregation is behind `ReportingService`, only the
service's data source changes when the mart arrives.

- **Facts:** `FactCaseProgress` (grain: one FileMaster per snapshot date — enables true
  burndown/trend the OLTP cannot), `FactLetterIssuance`, `FactEluDetermination` (volumes).
- **Dimensions:** `DimDate`, `DimProperty`, `DimCatchment`, `DimWMA`, `DimProvince`,
  `DimOrganisationalUnit`, `DimOfficer`, `DimWorkflowState`, `DimValidationStatus`,
  `DimLetterType`, `DimAssessmentTrack`. SCD-Type-2 on `DimProperty` (CONSOLIDATED/
  SUBDIVIDED lineage) and officer assignment.
- **ETL:** scheduled incremental load (hosted service / SQL Agent / Azure Data Factory).
- Maps 1:1 onto the six Appendix C reports; supports daily/weekly/monthly snapshots and
  cross-source analytics (Access migration WP3, future eWULAAS).
- Build as its own spec, with the database-architect agent.

### 10.2 BI strategy — open source, not Power BI
Self-service BI on the mart uses an **open-source** path: a **bespoke D3.js analytics
portal** (consistent with §8), optionally backed by **Apache Superset or Metabase**
for ad-hoc exploration. A mart + open-source BI may **reduce or replace** the need for
the deferred ad-hoc query builder (6.2).

**Commercial rationale:** avoiding Power BI removes recurring per-seat licensing the
client would otherwise pay Microsoft at ~500k-record scale, keeps the visualization
layer as **our IP**, and converts into a **billable build + ongoing support/maintenance
retainer** — recurring revenue retained in-house. The accepted trade-off is higher
upfront development effort versus a drag-and-drop proprietary tool, which is reasonable
for a bespoke statutory system already under our maintenance.

### 10.3 Other deferred items
- Scheduled / emailed report delivery on the Appendix C cadences (needs a background
  scheduler; `NotificationService` + email sender already exist to deliver).
- Per-user configurable dashboard widgets/layouts.

## 11. Testing

- **`ReportingService`**: aggregation correctness for each report against seeded data
  (known counts/sums); empty-data and single-record edge cases.
- **Org-scoping**: a RegionalManager sees only their WMA; passing another WMA's id does
  not widen results; NationalManager sees all.
- **Exporters**: each `IReportExporter` produces well-formed output for a sample
  `ReportTable` (CSV quoting, XLSX opens, PDF renders); empty table handled.
- **`ReportsController`**: authorization per report (User Activity blocked below
  RegionalManager; Integration Health below NationalManager); format selection;
  pagination bounds.
- **Caching**: identical filter+scope hits cache; differing scope does not collide.

## 12. Risks & Edge Cases

- **Integration Health is mostly empty** until `IntegrationService`/eWULAAS exists —
  build the shell, label it clearly, avoid implying live sync data.
- **Cache vs scope leakage**: the cache key MUST include the scope signature so two
  users in different WMAs never share a cached result. Covered by a test.
- **Export size**: a national unfiltered export could be very large — streamed output
  plus a sane max (or required filter) for non-HTML formats; document the limit rather
  than silently truncating.
- **Index cost**: new indexes add write overhead on a write-heavy import; confirm
  against the WP3 migration/import plan during implementation.
- **D3 per-chart effort**: mitigated by the shared chart-helper module; if a chart
  needs bespoke work beyond the helpers, scope it explicitly rather than inlining ad-hoc JS.
- **DateOnly vs DateTime** mismatches between `LetterIssuance.DueDate` and `AuditLog.Timestamp`
  filtering — normalise in `ReportFilter` application.
