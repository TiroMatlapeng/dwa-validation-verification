# Reporting Foundation + Standard Reports (Plan A) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the reporting backbone — a `ReportingService` producing org-scoped, cached aggregate reports rendered as HTML tables and exportable to Excel/PDF/CSV — and deliver the three cleanly-scopeable Appendix C reports (Catchment Progress, Letter Tracking, Validation Summary).

**Architecture:** All aggregation lives behind `IReportingService` (the seam for a future star-schema mart). Each report method returns a generic `ReportTable` (title + columns + pre-formatted string rows). A single generic Razor view renders any `ReportTable` with export buttons. `IReportExporter` implementations (CSV/Excel/PDF) serialize a `ReportTable` to a stream. Every query is org-scoped via `IScopedCaseQuery` and cached briefly in `IMemoryCache`.

**Tech Stack:** ASP.NET Core 10 MVC, EF Core 10, xUnit + EF InMemory, ClosedXML (new), QuestPDF (existing), IMemoryCache.

**Spec:** `docs/superpowers/specs/2026-06-02-reporting-and-analytics-design.md`

**Scope (Plan A):** foundation + exporters + 3 reports + controller + generic view + indexes + DI. **Out of scope:** reports 4–6 (User Activity, Public Portal Usage, Integration Health) — deferred to Plan A2 because `AuditLog`/`PublicUser` have no WMA FK and need a row-scoping decision; and the D3 dashboards (Plan B).

**Deviation from spec to confirm with the user:** the spec lists six reports with row-level org-scoping for all. `AuditLog`/`PublicUser` cannot be WMA-row-scoped without schema changes, so reports 4–6 are deferred (Plan A2), not built here.

---

## File Structure

**Create**
- `Services/Reporting/ReportFilter.cs` — query filter record.
- `Services/Reporting/ReportTable.cs` — `ReportColumn` + `ReportTable` (generic tabular result).
- `Services/Reporting/IReportingService.cs` + `ReportingService.cs` — aggregation, scoping, caching.
- `Services/Reporting/Export/IReportExporter.cs` — exporter contract.
- `Services/Reporting/Export/CsvReportExporter.cs`
- `Services/Reporting/Export/ExcelReportExporter.cs`
- `Services/Reporting/Export/PdfReportExporter.cs`
- `Controllers/ReportsController.cs`
- `Views/Reports/Index.cshtml` — report list + filter form.
- `Views/Reports/Report.cshtml` — generic table renderer + export buttons.
- Tests under `Tests/Services/Reporting/` and `Tests/Controllers/ReportsControllerTests.cs`.

**Modify**
- `dwa_ver_val.csproj` — add ClosedXML.
- `Program.cs` — `AddMemoryCache()`, register `IReportingService` + the three exporters.
- `Views/Shared/_Layout.cshtml` — add a "Reports" sidebar link.

---

## Task 1: Core report types

**Files:**
- Create: `Services/Reporting/ReportFilter.cs`, `Services/Reporting/ReportTable.cs`
- Test: `Tests/Services/Reporting/ReportTableTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Tests/Services/Reporting/ReportTableTests.cs`:

```csharp
using dwa_ver_val.Services.Reporting;
using Xunit;

namespace dwa_ver_val.Tests.Services.Reporting;

public class ReportTableTests
{
    [Fact]
    public void ReportTable_HoldsTitleColumnsAndRows()
    {
        var table = new ReportTable(
            "Catchment Progress",
            new[] { new ReportColumn("Catchment", false), new ReportColumn("Total", true) },
            new[] { new[] { "A21A", "5" }.AsReadOnly() });

        Assert.Equal("Catchment Progress", table.Title);
        Assert.Equal(2, table.Columns.Count);
        Assert.True(table.Columns[1].Numeric);
        Assert.Single(table.Rows);
        Assert.Equal("A21A", table.Rows[0][0]);
    }

    [Fact]
    public void ReportFilter_DefaultsArePermissive()
    {
        var f = new ReportFilter();
        Assert.Null(f.DateFrom);
        Assert.Null(f.WaterManagementAreaId);
        Assert.Equal(1, f.Page);
        Assert.Equal(50, f.PageSize);
    }
}
```

(`AsReadOnly()` on an array requires `using System.Collections.ObjectModel;` via `System.Linq`/array extension — if it doesn't resolve, wrap with `(IReadOnlyList<string>)new[]{...}` instead.)

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~ReportTableTests`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Implement the types**

Create `Services/Reporting/ReportFilter.cs`:

```csharp
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
    Guid? OfficerUserId = null,
    int Page = 1,
    int PageSize = 50);
```

Create `Services/Reporting/ReportTable.cs`:

```csharp
namespace dwa_ver_val.Services.Reporting;

/// <summary>A report column header. Numeric columns are right-aligned and formatted by exporters.</summary>
public record ReportColumn(string Header, bool Numeric = false);

/// <summary>
/// Generic, pre-rendered tabular report result. Rows are already formatted to strings by the
/// ReportingService so exporters and views stay dumb. This is the unit every exporter consumes.
/// </summary>
public record ReportTable(
    string Title,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows);
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~ReportTableTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add Services/Reporting/ReportFilter.cs Services/Reporting/ReportTable.cs Tests/Services/Reporting/ReportTableTests.cs
git commit -m "feat(reporting): core ReportFilter + ReportTable types"
```

---

## Task 2: CSV exporter

**Files:**
- Create: `Services/Reporting/Export/IReportExporter.cs`, `Services/Reporting/Export/CsvReportExporter.cs`
- Test: `Tests/Services/Reporting/CsvReportExporterTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Tests/Services/Reporting/CsvReportExporterTests.cs`:

```csharp
using System.Text;
using dwa_ver_val.Services.Reporting;
using dwa_ver_val.Services.Reporting.Export;
using Xunit;

namespace dwa_ver_val.Tests.Services.Reporting;

public class CsvReportExporterTests
{
    private static ReportTable Sample() => new(
        "Letter Tracking",
        new[] { new ReportColumn("Type"), new ReportColumn("Issued", true) },
        new[]
        {
            (IReadOnlyList<string>)new[] { "S35_L1", "3" },
            (IReadOnlyList<string>)new[] { "Has, comma", "1" },
        });

    [Fact]
    public async Task Writes_HeaderAndRows_WithRfc4180Quoting()
    {
        var sut = new CsvReportExporter();
        using var ms = new MemoryStream();
        await sut.WriteAsync(Sample(), ms, CancellationToken.None);
        var csv = Encoding.UTF8.GetString(ms.ToArray());

        Assert.Contains("Type,Issued", csv);
        Assert.Contains("S35_L1,3", csv);
        Assert.Contains("\"Has, comma\",1", csv); // comma value is quoted
    }

    [Fact]
    public void Metadata_IsCsv()
    {
        var sut = new CsvReportExporter();
        Assert.Equal("csv", sut.Format);
        Assert.Equal("text/csv", sut.ContentType);
        Assert.Equal(".csv", sut.FileExtension);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~CsvReportExporterTests`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Implement the contract + CSV exporter**

Create `Services/Reporting/Export/IReportExporter.cs`:

```csharp
namespace dwa_ver_val.Services.Reporting.Export;

/// <summary>Serializes a ReportTable to a stream in one format. Resolved by Format.</summary>
public interface IReportExporter
{
    string Format { get; }        // "csv" | "xlsx" | "pdf"
    string ContentType { get; }
    string FileExtension { get; } // ".csv" etc.
    Task WriteAsync(ReportTable table, Stream output, CancellationToken ct);
}
```

Create `Services/Reporting/Export/CsvReportExporter.cs`:

```csharp
using System.Text;

namespace dwa_ver_val.Services.Reporting.Export;

public class CsvReportExporter : IReportExporter
{
    public string Format => "csv";
    public string ContentType => "text/csv";
    public string FileExtension => ".csv";

    public async Task WriteAsync(ReportTable table, Stream output, CancellationToken ct)
    {
        // leaveOpen: caller owns the stream (controller wraps it in a FileStreamResult/returns bytes).
        await using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        await writer.WriteLineAsync(string.Join(",", table.Columns.Select(c => Quote(c.Header))));
        foreach (var row in table.Rows)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(",", row.Select(Quote)));
        }
        await writer.FlushAsync(ct);
    }

    // RFC 4180: quote if the value contains comma, quote, CR or LF; double embedded quotes.
    private static string Quote(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~CsvReportExporterTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add Services/Reporting/Export/IReportExporter.cs Services/Reporting/Export/CsvReportExporter.cs Tests/Services/Reporting/CsvReportExporterTests.cs
git commit -m "feat(reporting): IReportExporter + CSV exporter"
```

---

## Task 3: Excel exporter (ClosedXML)

**Files:**
- Modify: `dwa_ver_val.csproj` (add ClosedXML)
- Create: `Services/Reporting/Export/ExcelReportExporter.cs`
- Test: `Tests/Services/Reporting/ExcelReportExporterTests.cs`

- [ ] **Step 1: Add the ClosedXML package**

Run: `dotnet add dwa_ver_val.csproj package ClosedXML`
Expected: ClosedXML PackageReference added (a recent stable version, e.g. 0.104.x). Then run `dotnet restore`.

- [ ] **Step 2: Write the failing test**

Create `Tests/Services/Reporting/ExcelReportExporterTests.cs`:

```csharp
using ClosedXML.Excel;
using dwa_ver_val.Services.Reporting;
using dwa_ver_val.Services.Reporting.Export;
using Xunit;

namespace dwa_ver_val.Tests.Services.Reporting;

public class ExcelReportExporterTests
{
    private static ReportTable Sample() => new(
        "Catchment Progress",
        new[] { new ReportColumn("Catchment"), new ReportColumn("Total", true) },
        new[] { (IReadOnlyList<string>)new[] { "A21A", "5" } });

    [Fact]
    public async Task Writes_WorkbookWithHeaderAndRows()
    {
        var sut = new ExcelReportExporter();
        using var ms = new MemoryStream();
        await sut.WriteAsync(Sample(), ms, CancellationToken.None);
        ms.Position = 0;

        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet(1);
        Assert.Equal("Catchment", ws.Cell(1, 1).GetString());
        Assert.Equal("Total", ws.Cell(1, 2).GetString());
        Assert.Equal("A21A", ws.Cell(2, 1).GetString());
        Assert.Equal("5", ws.Cell(2, 2).GetString());
    }

    [Fact]
    public void Metadata_IsXlsx()
    {
        var sut = new ExcelReportExporter();
        Assert.Equal("xlsx", sut.Format);
        Assert.Equal(".xlsx", sut.FileExtension);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", sut.ContentType);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~ExcelReportExporterTests`
Expected: FAIL — `ExcelReportExporter` does not exist.

- [ ] **Step 4: Implement the Excel exporter**

Create `Services/Reporting/Export/ExcelReportExporter.cs`:

```csharp
using ClosedXML.Excel;

namespace dwa_ver_val.Services.Reporting.Export;

public class ExcelReportExporter : IReportExporter
{
    public string Format => "xlsx";
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string FileExtension => ".xlsx";

    public Task WriteAsync(ReportTable table, Stream output, CancellationToken ct)
    {
        using var wb = new XLWorkbook();
        // Excel sheet names: max 31 chars, no : \ / ? * [ ]
        var sheetName = Sanitize(table.Title);
        var ws = wb.Worksheets.Add(sheetName);

        for (var c = 0; c < table.Columns.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = table.Columns[c].Header;
            cell.Style.Font.Bold = true;
        }

        for (var r = 0; r < table.Rows.Count; r++)
            for (var c = 0; c < table.Rows[r].Count; c++)
                ws.Cell(r + 2, c + 1).Value = table.Rows[r][c];

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        wb.SaveAs(output);
        return Task.CompletedTask;
    }

    private static string Sanitize(string title)
    {
        var clean = new string(title.Where(ch => "\\/:?*[]".IndexOf(ch) < 0).ToArray());
        return clean.Length > 31 ? clean[..31] : (clean.Length == 0 ? "Report" : clean);
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~ExcelReportExporterTests`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add dwa_ver_val.csproj Services/Reporting/Export/ExcelReportExporter.cs Tests/Services/Reporting/ExcelReportExporterTests.cs
git commit -m "feat(reporting): Excel exporter via ClosedXML"
```

---

## Task 4: PDF exporter (QuestPDF)

**Files:**
- Create: `Services/Reporting/Export/PdfReportExporter.cs`
- Test: `Tests/Services/Reporting/PdfReportExporterTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Tests/Services/Reporting/PdfReportExporterTests.cs`:

```csharp
using dwa_ver_val.Services.Reporting;
using dwa_ver_val.Services.Reporting.Export;
using Xunit;

namespace dwa_ver_val.Tests.Services.Reporting;

public class PdfReportExporterTests
{
    private static ReportTable Sample() => new(
        "Validation Summary",
        new[] { new ReportColumn("Catchment"), new ReportColumn("Volume", true) },
        new[] { (IReadOnlyList<string>)new[] { "A21A", "1000.00" } });

    [Fact]
    public async Task Writes_NonEmptyPdf_WithPdfHeader()
    {
        var sut = new PdfReportExporter();
        using var ms = new MemoryStream();
        await sut.WriteAsync(Sample(), ms, CancellationToken.None);
        var bytes = ms.ToArray();

        Assert.True(bytes.Length > 0);
        // PDF files start with "%PDF"
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }

    [Fact]
    public void Metadata_IsPdf()
    {
        var sut = new PdfReportExporter();
        Assert.Equal("pdf", sut.Format);
        Assert.Equal("application/pdf", sut.ContentType);
        Assert.Equal(".pdf", sut.FileExtension);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~PdfReportExporterTests`
Expected: FAIL — `PdfReportExporter` does not exist.

- [ ] **Step 3: Implement the PDF exporter**

First check whether QuestPDF community licence is already configured at startup (the existing letter renderer sets `QuestPDF.Settings.License`). Read `Services/Letters/QuestPdfRenderer.cs`. If the licence is set once at app start, the exporter does not need to set it; but for unit tests (which don't run Program.cs) set it defensively in a static constructor.

Create `Services/Reporting/Export/PdfReportExporter.cs`:

```csharp
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace dwa_ver_val.Services.Reporting.Export;

public class PdfReportExporter : IReportExporter
{
    static PdfReportExporter()
    {
        // Safe to set repeatedly; ensures unit tests (which don't boot Program.cs) can render.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string Format => "pdf";
    public string ContentType => "application/pdf";
    public string FileExtension => ".pdf";

    public Task WriteAsync(ReportTable table, Stream output, CancellationToken ct)
    {
        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.Header().Text(table.Title).FontSize(16).SemiBold();
                page.Content().PaddingVertical(10).Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        foreach (var _ in table.Columns) cd.RelativeColumn();
                    });
                    foreach (var col in table.Columns)
                        t.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(col.Header).SemiBold();
                    foreach (var row in table.Rows)
                        foreach (var val in row)
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(val);
                });
                page.Footer().AlignRight().Text(x =>
                {
                    x.Span("Page "); x.CurrentPageNumber(); x.Span(" / "); x.TotalPages();
                });
            });
        }).GeneratePdf(output);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~PdfReportExporterTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add Services/Reporting/Export/PdfReportExporter.cs Tests/Services/Reporting/PdfReportExporterTests.cs
git commit -m "feat(reporting): PDF exporter via QuestPDF"
```

---

## Task 5: DI registration + memory cache

**Files:**
- Modify: `Program.cs`

- [ ] **Step 1: Register memory cache, reporting service, and exporters**

In `Program.cs`, in the service-registration region (near the other `AddScoped` calls around line 130), add:

```csharp
builder.Services.AddMemoryCache();
builder.Services.AddScoped<dwa_ver_val.Services.Reporting.IReportingService, dwa_ver_val.Services.Reporting.ReportingService>();
builder.Services.AddScoped<dwa_ver_val.Services.Reporting.Export.IReportExporter, dwa_ver_val.Services.Reporting.Export.CsvReportExporter>();
builder.Services.AddScoped<dwa_ver_val.Services.Reporting.Export.IReportExporter, dwa_ver_val.Services.Reporting.Export.ExcelReportExporter>();
builder.Services.AddScoped<dwa_ver_val.Services.Reporting.Export.IReportExporter, dwa_ver_val.Services.Reporting.Export.PdfReportExporter>();
```

(`IReportingService`/`ReportingService` are created in Task 6 — this registration will not compile until then. To keep the build green per-task, add ONLY the `AddMemoryCache()` and the three exporter registrations in this task, and add the `IReportingService` registration line as the final step of Task 6. Do that now: add `AddMemoryCache()` + the 3 exporter lines here.)

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add Program.cs
git commit -m "feat(reporting): register memory cache + report exporters"
```

---

## Task 6: ReportingService skeleton (scoping + filters + caching)

**Files:**
- Create: `Services/Reporting/IReportingService.cs`, `Services/Reporting/ReportingService.cs`
- Modify: `Program.cs` (add the `IReportingService` registration line from Task 5)
- Test: `Tests/Services/Reporting/ReportingServiceScopingTests.cs`

- [ ] **Step 1: Write the failing test (scoping + filter plumbing via the first report)**

This test is written against `CatchmentProgressAsync` (implemented in Task 7) — to keep Task 6 self-contained, the skeleton here includes a stub `CatchmentProgressAsync` that returns an empty table; Task 7 fills it in with real aggregation and its own assertions. For THIS task, only test the shared helpers indirectly. Create `Tests/Services/Reporting/ReportingServiceScopingTests.cs`:

```csharp
using System.Security.Claims;
using dwa_ver_val.Services.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace dwa_ver_val.Tests.Services.Reporting;

public class ReportingServiceScopingTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IMemoryCache Cache() => new MemoryCache(new MemoryCacheOptions());

    private static ClaimsPrincipal NationalManager() =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, DwsRoles.NationalManager) }, "Test"));

    [Fact]
    public async Task Service_Constructs_AndReturnsTableForNationalManager()
    {
        using var db = NewDb();
        var svc = new ReportingService(db, new ScopedCaseQuery(db), Cache());
        var table = await svc.CatchmentProgressAsync(new ReportFilter(), NationalManager(), CancellationToken.None);
        Assert.NotNull(table);
        Assert.Equal("Catchment Progress", table.Title);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~ReportingServiceScopingTests`
Expected: FAIL — `IReportingService`/`ReportingService` do not exist.

- [ ] **Step 3: Implement the interface + skeleton**

Create `Services/Reporting/IReportingService.cs`:

```csharp
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
}
```

Create `Services/Reporting/ReportingService.cs`:

```csharp
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace dwa_ver_val.Services.Reporting;

public class ReportingService : IReportingService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(90);

    private readonly ApplicationDBContext _db;
    private readonly IScopedCaseQuery _scope;
    private readonly IMemoryCache _cache;

    public ReportingService(ApplicationDBContext db, IScopedCaseQuery scope, IMemoryCache cache)
    {
        _db = db; _scope = scope; _cache = cache;
    }

    // Scope signature ensures two users in different WMAs never share a cached result.
    private static string ScopeKey(ClaimsPrincipal user) =>
        (user.FindFirst("wmaId")?.Value ?? "none")
        + "|" + (user.IsInRole(DwsRoles.SystemAdmin) || user.IsInRole(DwsRoles.NationalManager) ? "all" : "wma");

    private Task<ReportTable> CachedAsync(string report, ReportFilter f, ClaimsPrincipal user,
        Func<Task<ReportTable>> build)
    {
        var key = $"rpt:{report}:{ScopeKey(user)}:{f.DateFrom}:{f.DateTo}:{f.WaterManagementAreaId}:{f.CatchmentAreaId}:{f.ValidationStatus}:{f.OfficerUserId}";
        return _cache.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return build();
        })!;
    }

    // Org scope first, then the common filters. Caller can never widen beyond their scope.
    private IQueryable<FileMaster> ScopedCases(ReportFilter f, ClaimsPrincipal user)
    {
        var q = _scope.FilterFileMasters(_db.FileMasters.AsNoTracking(), user);
        if (f.WaterManagementAreaId is { } wma) q = q.Where(fm => fm.Property!.WmaId == wma);
        if (f.CatchmentAreaId is { } cat) q = q.Where(fm => fm.CatchmentAreaId == cat);
        if (!string.IsNullOrWhiteSpace(f.ValidationStatus)) q = q.Where(fm => fm.ValidationStatusName == f.ValidationStatus);
        if (f.DateFrom is { } from) q = q.Where(fm => fm.FileCreatedDate >= from);
        if (f.DateTo is { } to) q = q.Where(fm => fm.FileCreatedDate <= to);
        return q;
    }

    public Task<ReportTable> CatchmentProgressAsync(ReportFilter filter, ClaimsPrincipal user, CancellationToken ct)
        => CachedAsync("catchment", filter, user, async () =>
        {
            // Filled in Task 7. Skeleton returns the titled empty table.
            await Task.CompletedTask;
            return new ReportTable("Catchment Progress",
                new[] { new ReportColumn("Catchment") },
                Array.Empty<IReadOnlyList<string>>());
        });

    public Task<ReportTable> LetterTrackingAsync(ReportFilter filter, ClaimsPrincipal user, CancellationToken ct)
        => CachedAsync("letters", filter, user, async () =>
        {
            await Task.CompletedTask;
            return new ReportTable("Letter Tracking",
                new[] { new ReportColumn("Letter Type") },
                Array.Empty<IReadOnlyList<string>>());
        });

    public Task<ReportTable> ValidationSummaryAsync(ReportFilter filter, ClaimsPrincipal user, CancellationToken ct)
        => CachedAsync("validation", filter, user, async () =>
        {
            await Task.CompletedTask;
            return new ReportTable("Validation Summary",
                new[] { new ReportColumn("Catchment") },
                Array.Empty<IReadOnlyList<string>>());
        });
}
```

- [ ] **Step 4: Add the DI registration**

In `Program.cs`, add (next to the exporter registrations from Task 5):

```csharp
builder.Services.AddScoped<dwa_ver_val.Services.Reporting.IReportingService, dwa_ver_val.Services.Reporting.ReportingService>();
```

- [ ] **Step 5: Run the test + build**

Run: `dotnet test --filter FullyQualifiedName~ReportingServiceScopingTests`
Expected: PASS (1 test). Then `dotnet build` → Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add Services/Reporting/IReportingService.cs Services/Reporting/ReportingService.cs Program.cs Tests/Services/Reporting/ReportingServiceScopingTests.cs
git commit -m "feat(reporting): ReportingService skeleton with scoping + caching"
```

---

## Task 7: Catchment Progress report

**Files:**
- Modify: `Services/Reporting/ReportingService.cs` (`CatchmentProgressAsync`)
- Test: `Tests/Services/Reporting/CatchmentProgressTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Tests/Services/Reporting/CatchmentProgressTests.cs`:

```csharp
using System.Security.Claims;
using dwa_ver_val.Services.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace dwa_ver_val.Tests.Services.Reporting;

public class CatchmentProgressTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ReportingService Svc(ApplicationDBContext db) =>
        new(db, new ScopedCaseQuery(db), new MemoryCache(new MemoryCacheOptions()));

    private static ClaimsPrincipal NationalManager() =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, DwsRoles.NationalManager) }, "Test"));

    private static FileMaster Case(Guid catchmentId, string status) => new()
    {
        FileMasterId = Guid.NewGuid(), PropertyId = Guid.NewGuid(),
        CatchmentAreaId = catchmentId, ValidationStatusName = status,
        RegistrationNumber = "N/A", SurveyorGeneralCode = "N/A", PrimaryCatchment = "N/A",
        QuaternaryCatchment = "N/A", FarmName = "N/A", FarmNumber = 1,
        RegistrationDivision = "N/A", FarmPortion = "0",
        FileCreatedDate = new DateOnly(2026, 1, 1)
    };

    [Fact]
    public async Task GroupsByCatchment_WithCountsAndCompletionPct()
    {
        using var db = NewDb();
        var wma = new WaterManagementArea { WmaId = Guid.NewGuid(), WmaName = "Limpopo", WmaCode = "LIM", ProvinceId = Guid.NewGuid() };
        var cat = new CatchmentArea { CatchmentAreaId = Guid.NewGuid(), CatchmentName = "A21A", WmaId = wma.WmaId };
        db.WaterManagementAreas.Add(wma); db.CatchmentAreas.Add(cat);
        db.FileMasters.AddRange(
            Case(cat.CatchmentAreaId, "Completed"),
            Case(cat.CatchmentAreaId, "Completed"),
            Case(cat.CatchmentAreaId, "In Process"),
            Case(cat.CatchmentAreaId, "Not Commenced"));
        await db.SaveChangesAsync();

        var table = await Svc(db).CatchmentProgressAsync(new ReportFilter(), NationalManager(), CancellationToken.None);

        Assert.Equal("Catchment Progress", table.Title);
        var row = Assert.Single(table.Rows);
        Assert.Equal("A21A", row[0]);   // Catchment
        Assert.Equal("4", row[1]);      // Total
        Assert.Equal("2", row[2]);      // Completed
        Assert.Equal("1", row[3]);      // In Process
        Assert.Equal("50.0%", row[5]);  // Completion % (2/4)
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~CatchmentProgressTests`
Expected: FAIL — skeleton returns an empty table.

- [ ] **Step 3: Implement the aggregation**

In `Services/Reporting/ReportingService.cs`, replace the body of the `CatchmentProgressAsync` build lambda with:

```csharp
        => CachedAsync("catchment", filter, user, async () =>
        {
            var rows = await ScopedCases(filter, user)
                .GroupBy(fm => fm.CatchmentArea != null ? fm.CatchmentArea.CatchmentName : "(unassigned)")
                .Select(g => new
                {
                    Catchment = g.Key,
                    Total = g.Count(),
                    Completed = g.Count(x => x.ValidationStatusName == "Completed"),
                    InProcess = g.Count(x => x.ValidationStatusName == "In Process"),
                    NotCommenced = g.Count(x => x.ValidationStatusName == "Not Commenced"),
                })
                .OrderBy(x => x.Catchment)
                .ToListAsync(ct);

            var tableRows = rows
                .Select(r => (IReadOnlyList<string>)new[]
                {
                    r.Catchment,
                    r.Total.ToString(),
                    r.Completed.ToString(),
                    r.InProcess.ToString(),
                    r.NotCommenced.ToString(),
                    r.Total == 0 ? "0.0%" : (100.0 * r.Completed / r.Total).ToString("0.0") + "%",
                })
                .ToList();

            return new ReportTable("Catchment Progress",
                new[]
                {
                    new ReportColumn("Catchment"),
                    new ReportColumn("Total", true),
                    new ReportColumn("Completed", true),
                    new ReportColumn("In Process", true),
                    new ReportColumn("Not Commenced", true),
                    new ReportColumn("Completion %", true),
                },
                tableRows);
        });
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~CatchmentProgressTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Services/Reporting/ReportingService.cs Tests/Services/Reporting/CatchmentProgressTests.cs
git commit -m "feat(reporting): Catchment Progress report"
```

---

## Task 8: Letter Tracking report

**Files:**
- Modify: `Services/Reporting/ReportingService.cs` (`LetterTrackingAsync`)
- Test: `Tests/Services/Reporting/LetterTrackingTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Tests/Services/Reporting/LetterTrackingTests.cs`:

```csharp
using System.Security.Claims;
using dwa_ver_val.Services.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace dwa_ver_val.Tests.Services.Reporting;

public class LetterTrackingTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ReportingService Svc(ApplicationDBContext db) =>
        new(db, new ScopedCaseQuery(db), new MemoryCache(new MemoryCacheOptions()));

    private static ClaimsPrincipal NationalManager() =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, DwsRoles.NationalManager) }, "Test"));

    private static FileMaster Case() => new()
    {
        FileMasterId = Guid.NewGuid(), PropertyId = Guid.NewGuid(),
        RegistrationNumber = "N/A", SurveyorGeneralCode = "N/A", PrimaryCatchment = "N/A",
        QuaternaryCatchment = "N/A", FarmName = "N/A", FarmNumber = 1,
        RegistrationDivision = "N/A", FarmPortion = "0", FileCreatedDate = new DateOnly(2026, 1, 1)
    };

    [Fact]
    public async Task GroupsByLetterType_CountsIssuedResponsesOverdueRts()
    {
        using var db = NewDb();
        var fm = Case();
        db.FileMasters.Add(fm);
        var lt = new LetterType { LetterTypeId = Guid.NewGuid(), LetterName = "S35_L1", LetterDescription = "Letter 1", NWASection = "S35" };
        db.LetterTypes.Add(lt);

        // Issued + responded
        db.LetterIssuances.Add(new LetterIssuance { LetterIssuanceId = Guid.NewGuid(), FileMasterId = fm.FileMasterId, LetterTypeId = lt.LetterTypeId, IssuedDate = new DateOnly(2026,1,2), ResponseDate = new DateOnly(2026,1,20) });
        // Issued + overdue (due in the past, no response)
        db.LetterIssuances.Add(new LetterIssuance { LetterIssuanceId = Guid.NewGuid(), FileMasterId = fm.FileMasterId, LetterTypeId = lt.LetterTypeId, IssuedDate = new DateOnly(2020,1,2), DueDate = new DateOnly(2020,2,2) });
        // Issued + returned to sender
        db.LetterIssuances.Add(new LetterIssuance { LetterIssuanceId = Guid.NewGuid(), FileMasterId = fm.FileMasterId, LetterTypeId = lt.LetterTypeId, IssuedDate = new DateOnly(2026,1,2), ReturnedToSender = true });
        await db.SaveChangesAsync();

        var table = await Svc(db).LetterTrackingAsync(new ReportFilter(), NationalManager(), CancellationToken.None);

        var row = Assert.Single(table.Rows);
        Assert.Equal("S35_L1", row[0]); // Letter type
        Assert.Equal("3", row[1]);      // Issued
        Assert.Equal("1", row[2]);      // Responses
        Assert.Equal("1", row[3]);      // Overdue
        Assert.Equal("1", row[4]);      // Returned to sender
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~LetterTrackingTests`
Expected: FAIL — skeleton returns empty table.

- [ ] **Step 3: Implement the aggregation**

In `Services/Reporting/ReportingService.cs`, replace the `LetterTrackingAsync` build lambda body with:

```csharp
        => CachedAsync("letters", filter, user, async () =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var caseIds = ScopedCases(filter, user).Select(fm => fm.FileMasterId);

            var q = _db.LetterIssuances.AsNoTracking()
                .Where(l => caseIds.Contains(l.FileMasterId));
            if (filter.DateFrom is { } from) q = q.Where(l => l.IssuedDate >= from);
            if (filter.DateTo is { } to) q = q.Where(l => l.IssuedDate <= to);

            var rows = await q
                .GroupBy(l => l.LetterType!.LetterName)
                .Select(g => new
                {
                    Type = g.Key,
                    Issued = g.Count(x => x.IssuedDate != null),
                    Responses = g.Count(x => x.ResponseDate != null),
                    Overdue = g.Count(x => x.DueDate != null && x.DueDate < today && x.ResponseDate == null),
                    Rts = g.Count(x => x.ReturnedToSender),
                })
                .OrderBy(x => x.Type)
                .ToListAsync(ct);

            var tableRows = rows
                .Select(r => (IReadOnlyList<string>)new[]
                {
                    r.Type, r.Issued.ToString(), r.Responses.ToString(), r.Overdue.ToString(), r.Rts.ToString(),
                })
                .ToList();

            return new ReportTable("Letter Tracking",
                new[]
                {
                    new ReportColumn("Letter Type"),
                    new ReportColumn("Issued", true),
                    new ReportColumn("Responses", true),
                    new ReportColumn("Overdue", true),
                    new ReportColumn("Returned to Sender", true),
                },
                tableRows);
        });
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~LetterTrackingTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Services/Reporting/ReportingService.cs Tests/Services/Reporting/LetterTrackingTests.cs
git commit -m "feat(reporting): Letter Tracking report"
```

---

## Task 9: Validation Summary report

**Files:**
- Modify: `Services/Reporting/ReportingService.cs` (`ValidationSummaryAsync`)
- Test: `Tests/Services/Reporting/ValidationSummaryTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Tests/Services/Reporting/ValidationSummaryTests.cs`:

```csharp
using System.Security.Claims;
using dwa_ver_val.Services.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace dwa_ver_val.Tests.Services.Reporting;

public class ValidationSummaryTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ReportingService Svc(ApplicationDBContext db) =>
        new(db, new ScopedCaseQuery(db), new MemoryCache(new MemoryCacheOptions()));

    private static ClaimsPrincipal NationalManager() =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, DwsRoles.NationalManager) }, "Test"));

    [Fact]
    public async Task SumsEluVolumeAndCountsValidatedPropertiesPerCatchment()
    {
        using var db = NewDb();
        var cat = new CatchmentArea { CatchmentAreaId = Guid.NewGuid(), CatchmentName = "A21A", WmaId = Guid.NewGuid() };
        db.CatchmentAreas.Add(cat);
        var etype = new EntitlementType { EntitlementTypeId = Guid.NewGuid(), Name = "Irrigation" };
        db.EntitlementTypes.Add(etype);
        var e1 = new Entitlement { EntitlementId = Guid.NewGuid(), Name = "E1", Volume = 1000m, EntitlementTypeId = etype.EntitlementTypeId, EntitlementType = etype };
        var e2 = new Entitlement { EntitlementId = Guid.NewGuid(), Name = "E2", Volume = 500m, EntitlementTypeId = etype.EntitlementTypeId, EntitlementType = etype };
        db.Entitlements.AddRange(e1, e2);

        FileMaster Case(Guid eid) => new()
        {
            FileMasterId = Guid.NewGuid(), PropertyId = Guid.NewGuid(), CatchmentAreaId = cat.CatchmentAreaId,
            EntitlementId = eid, ValidationStatusName = "Completed",
            RegistrationNumber = "N/A", SurveyorGeneralCode = "N/A", PrimaryCatchment = "N/A",
            QuaternaryCatchment = "N/A", FarmName = "N/A", FarmNumber = 1,
            RegistrationDivision = "N/A", FarmPortion = "0", FileCreatedDate = new DateOnly(2026,1,1)
        };
        db.FileMasters.AddRange(Case(e1.EntitlementId), Case(e2.EntitlementId));
        await db.SaveChangesAsync();

        var table = await Svc(db).ValidationSummaryAsync(new ReportFilter(), NationalManager(), CancellationToken.None);

        var row = Assert.Single(table.Rows);
        Assert.Equal("A21A", row[0]);       // Catchment
        Assert.Equal("2", row[1]);          // Properties validated
        Assert.Equal("1500.00", row[2]);    // Total ELU volume
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~ValidationSummaryTests`
Expected: FAIL — skeleton returns empty table.

- [ ] **Step 3: Implement the aggregation**

In `Services/Reporting/ReportingService.cs`, replace the `ValidationSummaryAsync` build lambda body with:

```csharp
        => CachedAsync("validation", filter, user, async () =>
        {
            var rows = await ScopedCases(filter, user)
                .Where(fm => fm.EntitlementId != null)
                .GroupBy(fm => fm.CatchmentArea != null ? fm.CatchmentArea.CatchmentName : "(unassigned)")
                .Select(g => new
                {
                    Catchment = g.Key,
                    Properties = g.Count(),
                    Volume = g.Sum(x => x.Entitlement!.Volume),
                })
                .OrderBy(x => x.Catchment)
                .ToListAsync(ct);

            var tableRows = rows
                .Select(r => (IReadOnlyList<string>)new[]
                {
                    r.Catchment, r.Properties.ToString(), r.Volume.ToString("0.00"),
                })
                .ToList();

            return new ReportTable("Validation Summary",
                new[]
                {
                    new ReportColumn("Catchment"),
                    new ReportColumn("Properties Validated", true),
                    new ReportColumn("Total ELU Volume (m³)", true),
                },
                tableRows);
        });
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~ValidationSummaryTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Services/Reporting/ReportingService.cs Tests/Services/Reporting/ValidationSummaryTests.cs
git commit -m "feat(reporting): Validation Summary report"
```

---

## Task 10: ReportsController + views + nav

**Files:**
- Create: `Controllers/ReportsController.cs`, `Views/Reports/Index.cshtml`, `Views/Reports/Report.cshtml`
- Modify: `Views/Shared/_Layout.cshtml`
- Test: `Tests/Controllers/ReportsControllerTests.cs`

- [ ] **Step 1: Write the failing controller tests**

Create `Tests/Controllers/ReportsControllerTests.cs`:

```csharp
using System.Security.Claims;
using dwa_ver_val.Controllers;
using dwa_ver_val.Services.Reporting;
using dwa_ver_val.Services.Reporting.Export;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace dwa_ver_val.Tests.Controllers;

public class ReportsControllerTests
{
    private sealed class FakeReporting : IReportingService
    {
        public Task<ReportTable> CatchmentProgressAsync(ReportFilter f, ClaimsPrincipal u, CancellationToken ct)
            => Task.FromResult(new ReportTable("Catchment Progress",
                new[] { new ReportColumn("Catchment"), new ReportColumn("Total", true) },
                new[] { (IReadOnlyList<string>)new[] { "A21A", "5" } }));
        public Task<ReportTable> LetterTrackingAsync(ReportFilter f, ClaimsPrincipal u, CancellationToken ct)
            => Task.FromResult(new ReportTable("Letter Tracking", new[] { new ReportColumn("Letter Type") }, Array.Empty<IReadOnlyList<string>>()));
        public Task<ReportTable> ValidationSummaryAsync(ReportFilter f, ClaimsPrincipal u, CancellationToken ct)
            => Task.FromResult(new ReportTable("Validation Summary", new[] { new ReportColumn("Catchment") }, Array.Empty<IReadOnlyList<string>>()));
    }

    private static ReportsController Build()
    {
        var ctrl = new ReportsController(new FakeReporting(),
            new IReportExporter[] { new CsvReportExporter(), new ExcelReportExporter(), new PdfReportExporter() });
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, DwsRoles.NationalManager) }, "Test"))
            }
        };
        return ctrl;
    }

    [Fact]
    public async Task CatchmentProgress_Html_ReturnsViewWithTable()
    {
        var result = await Build().CatchmentProgress(new ReportFilter(), null, CancellationToken.None);
        var view = Assert.IsType<ViewResult>(result);
        var table = Assert.IsType<ReportTable>(view.Model);
        Assert.Equal("Catchment Progress", table.Title);
        Assert.Equal("Report", view.ViewName);
    }

    [Fact]
    public async Task CatchmentProgress_Csv_ReturnsFileResult()
    {
        var result = await Build().CatchmentProgress(new ReportFilter(), "csv", CancellationToken.None);
        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv", file.ContentType);
        Assert.EndsWith(".csv", file.FileDownloadName);
    }

    [Fact]
    public async Task CatchmentProgress_Xlsx_ReturnsExcelFile()
    {
        var result = await Build().CatchmentProgress(new ReportFilter(), "xlsx", CancellationToken.None);
        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
    }

    [Fact]
    public async Task UnknownFormat_FallsBackToHtmlView()
    {
        var result = await Build().CatchmentProgress(new ReportFilter(), "weird", CancellationToken.None);
        Assert.IsType<ViewResult>(result);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~ReportsControllerTests`
Expected: FAIL — `ReportsController` does not exist.

- [ ] **Step 3: Implement the controller**

Create `Controllers/ReportsController.cs`:

```csharp
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
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~ReportsControllerTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Create the generic report view**

Create `Views/Reports/Report.cshtml`:

```cshtml
@model dwa_ver_val.Services.Reporting.ReportTable
@{
    ViewData["Title"] = Model.Title;
    var action = (string?)ViewContext.RouteData.Values["action"];
}

<div class="card" style="max-width: 1100px;">
    <div class="form-section-title">@Model.Title</div>

    <div style="margin-bottom: 12px;">
        <a class="btn btn-secondary" asp-action="@action" asp-route-format="csv">Export CSV</a>
        <a class="btn btn-secondary" asp-action="@action" asp-route-format="xlsx">Export Excel</a>
        <a class="btn btn-secondary" asp-action="@action" asp-route-format="pdf">Export PDF</a>
    </div>

    <table class="table">
        <thead>
            <tr>
            @foreach (var col in Model.Columns)
            {
                <th style="@(col.Numeric ? "text-align:right;" : "")">@col.Header</th>
            }
            </tr>
        </thead>
        <tbody>
        @if (Model.Rows.Count == 0)
        {
            <tr><td colspan="@Model.Columns.Count" style="color:#666;">No data for the selected filters.</td></tr>
        }
        else
        {
            @foreach (var row in Model.Rows)
            {
                <tr>
                @for (var i = 0; i < row.Count; i++)
                {
                    <td style="@(i < Model.Columns.Count && Model.Columns[i].Numeric ? "text-align:right;" : "")">@row[i]</td>
                }
                </tr>
            }
        }
        </tbody>
    </table>
</div>
```

- [ ] **Step 6: Create the reports index view**

Create `Views/Reports/Index.cshtml`:

```cshtml
@{
    ViewData["Title"] = "Reports";
}

<div class="card" style="max-width: 700px;">
    <div class="form-section-title">Reports</div>
    <ul style="line-height: 2;">
        <li><a asp-action="CatchmentProgress">Catchment Progress</a> — records, status breakdown &amp; completion % per catchment</li>
        <li><a asp-action="LetterTracking">Letter Tracking</a> — letters issued, responses, overdue, returned-to-sender</li>
        <li><a asp-action="ValidationSummary">Validation Summary</a> — properties validated &amp; ELU volumes by catchment</li>
    </ul>
</div>
```

- [ ] **Step 7: Add the sidebar nav link**

In `Views/Shared/_Layout.cshtml`, in the "Operations" sidebar section (after the Validation link, around line 43), add:

```cshtml
            <a asp-controller="Reports" asp-action="Index"
               class="sidebar-item @(controllerName == "Reports" ? "active" : "")">
                <span class="sidebar-icon">&#9632;</span> Reports
            </a>
```

- [ ] **Step 8: Build + run full reporting tests**

Run: `dotnet build`
Expected: Build succeeded (Razor compiles).
Run: `dotnet test --filter FullyQualifiedName~Reporting` and `dotnet test --filter FullyQualifiedName~ReportsControllerTests`
Expected: all PASS.

- [ ] **Step 9: Commit**

```bash
git add Controllers/ReportsController.cs Views/Reports/ Views/Shared/_Layout.cshtml Tests/Controllers/ReportsControllerTests.cs
git commit -m "feat(reporting): ReportsController + generic report view + nav"
```

---

## Task 11: Reporting indexes migration

**Files:**
- Migration: generated under `Migrations/`

- [ ] **Step 1: Confirm which indexes are missing**

Read `DatabaseContexts/ApplicationDBContext.cs` and the latest model snapshot to check existing indexes. We want indexes supporting the report aggregations; ADD ONLY those not already present:
- `FileMaster(ValidationStatusName)`
- `LetterIssuance(IssuedDate)`
- `LetterIssuance(ResponseDate)`
- `LetterIssuance(DueDate)`
`Property(WmaId)`, `FileMaster(CatchmentAreaId)`, `LetterIssuance(LetterTypeId)`, and `LetterIssuance(FileMasterId)` are likely already indexed via existing FK configuration — do NOT duplicate. For any you add, configure in `OnModelCreating` with `HasIndex`, e.g.:

```csharp
        modelBuilder.Entity<FileMaster>().HasIndex(f => f.ValidationStatusName);
        modelBuilder.Entity<LetterIssuance>().HasIndex(l => l.IssuedDate);
        modelBuilder.Entity<LetterIssuance>().HasIndex(l => l.ResponseDate);
        modelBuilder.Entity<LetterIssuance>().HasIndex(l => l.DueDate);
```
Place these near the other entity configuration. Only include lines for indexes that do not already exist.

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: Build succeeded.

- [ ] **Step 3: Create the migration**

Run: `dotnet ef migrations add AddReportingIndexes`
Expected: a migration whose `Up()` only `CreateIndex` calls for the columns added in Step 1 — no table or column changes. Open it and confirm it contains only `CreateIndex`/`DropIndex` operations. If it contains unrelated changes, STOP and report BLOCKED.

- [ ] **Step 4: Commit**

```bash
git add DatabaseContexts/ApplicationDBContext.cs Migrations/
git commit -m "feat(reporting): add aggregation indexes for reports"
```

---

## Task 12: Full verification

- [ ] **Step 1: Build**

Run: `dotnet build`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 2: Run the full non-integration suite**

Run: `dotnet test --filter "FullyQualifiedName!~Integration"`
Expected: all pass (existing tests plus the new ReportTable/Csv/Excel/Pdf/ReportingService/CatchmentProgress/LetterTracking/ValidationSummary/ReportsController tests).

- [ ] **Step 3: Apply the migration (needs dev SQL Server)**

Run: `dotnet ef database update`
Expected: `AddReportingIndexes` applies cleanly.

- [ ] **Step 4: Manual check (needs the app running)**

Run `dotnet run`, log in, open Reports from the sidebar. For each report: confirm the HTML table renders, the filters narrow results, and CSV/Excel/PDF export buttons download a well-formed file. Confirm a RegionalManager sees only their WMA's rows (org-scoping) and a NationalManager sees all.

- [ ] **Step 5: Final commit (if any tweaks)**

```bash
git add -A
git commit -m "test(reporting): foundation + 3 standard reports verified"
```

---

## Self-Review Notes (author)

- **Spec coverage (Plan A subset):** ReportingService seam (Task 6), org-scoping + filters (Task 6, every report), caching with scope-keyed entries (Task 6), exports CSV/Excel/PDF (Tasks 2–4), generic table view + controller + export endpoints (Task 10), perf indexes (Task 11), and 3 of the 6 reports (Tasks 7–9). Dashboards (D3) and reports 4–6 are explicitly out of scope (Plan B / Plan A2).
- **Deviation flagged for the user:** reports 4–6 (User Activity, Public Portal Usage, Integration Health) deferred because `AuditLog`/`PublicUser` have no WMA FK and cannot be row-scoped without a schema/design decision. Confirm before Plan A2.
- **Type consistency:** `ReportTable(Title, Columns, Rows)`, `ReportColumn(Header, Numeric)`, `ReportFilter(...)`, `IReportExporter(Format/ContentType/FileExtension/WriteAsync)`, `IReportingService` three methods — used identically across service, exporters, controller, and views.
- **Caching correctness:** cache key includes the scope signature (wmaId + all/wma) so two users in different WMAs never collide — matches the spec's stated risk control.
- **Edge cases:** empty result sets render "No data" (view) and produce header-only exports; `Completion %` guards divide-by-zero; Excel sheet-name sanitised to ≤31 chars without illegal chars; CSV RFC-4180 quoting tested; PDF community licence set defensively for unit tests.
