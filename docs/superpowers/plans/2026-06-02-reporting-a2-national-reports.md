# Reporting Plan A2 — National Oversight Reports (4–6)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Add the three remaining Appendix C reports — User Activity, Public Portal Usage, Integration Health — as **NationalManager-only** national-scope reports (no per-WMA row scoping), reusing Plan A's `ReportingService`/exporters/generic view.

**Decision (confirmed with stakeholder):** `AuditLog`/`PublicUser` have no WMA foreign key, so these three oversight reports are **restricted to NationalManager/SystemAdmin** and show **national (unscoped)** data. No schema change. Per-WMA scoping can be added later if needed.

**Architecture:** Extend `IReportingService` with three methods returning `ReportTable`. They do NOT use `IScopedCaseQuery` (national data); access is gated by a new `CanViewNationalReports` policy (NationalManager+) on the controller actions. National results are cached under a scope-free key. UI reuses the generic `Report.cshtml`; the Reports index shows the national section only to authorized users.

**Tech Stack:** ASP.NET Core 10 MVC, EF Core 10, xUnit + EF InMemory.

**Spec:** `docs/superpowers/specs/2026-06-02-reporting-and-analytics-design.md` (§5 reports 4–6, §14.2/14.3).

**Depends on:** Plan A (`IReportingService`, exporters, `ReportsController`, generic `Report.cshtml`, `ReportFilter`, `ReportTable`) — on branch `feature/reporting-foundation`. Built on a branch stacked off it.

**Data sources (confirmed):** `AuditLog(UserName, ApplicationUserId, Action, EntityType, EntityId, Timestamp:DateTime)`; `PublicUser(EmailConfirmed, MfaEnabled, RegistrationDate, LastLoginDate)`; `Document.UploadedByPublicUserId`.

**Scope notes / deviations:**
- **User Activity** = actions per officer with action count + last activity (the spec's "cases completed" needs a completion-event signal not yet modelled — deferred; documented in code).
- **Public Portal Usage** = current snapshot metrics (registrations, email-confirmed, MFA-enabled, logged-in, portal document uploads). Date filter not applied (snapshot) — documented.
- **Integration Health** = counts of `IntegrationSent`/`IntegrationReceived` audit actions; **empty until `IntegrationService`/eWULAAS exists** (shell), as the spec anticipates.

---

## File Structure

**Modify**
- `Services/Auth/DwsPolicies.cs` — add `CanViewNationalReports` (NationalManager+).
- `Services/Reporting/IReportingService.cs` — add 3 method signatures.
- `Services/Reporting/ReportingService.cs` — national cache helper + 3 implementations.
- `Controllers/ReportsController.cs` — 3 national-only actions.
- `Views/Reports/Index.cshtml` — national-reports section (shown only to authorized users).

**Create**
- `Tests/Services/Reporting/NationalReportsTests.cs`
- (extend) `Tests/Controllers/ReportsControllerTests.cs` — national actions.

---

## Task 1: Policy + ReportingService national reports

**Files:**
- Modify: `Services/Auth/DwsPolicies.cs`, `Services/Reporting/IReportingService.cs`, `Services/Reporting/ReportingService.cs`
- Test: `Tests/Services/Reporting/NationalReportsTests.cs`

- [ ] **Step 1: Add the policy**

In `Services/Auth/DwsPolicies.cs`, add a const after `CanRead`:
```csharp
    public const string CanViewNationalReports = "CanViewNationalReports";
```
and in `Configure`, after the `CanRead` policy:
```csharp
        options.AddPolicy(CanViewNationalReports,
            p => p.RequireAuthenticatedUser().RequireRole(DwsRoles.AtLeastNationalManager));
```

- [ ] **Step 2: Add interface methods**

In `Services/Reporting/IReportingService.cs`, add to the interface:
```csharp
    // National oversight reports (NationalManager+; not WMA-row-scoped — AuditLog/PublicUser have no WMA FK).
    Task<ReportTable> UserActivityAsync(ReportFilter filter, CancellationToken ct);
    Task<ReportTable> PublicPortalUsageAsync(ReportFilter filter, CancellationToken ct);
    Task<ReportTable> IntegrationHealthAsync(ReportFilter filter, CancellationToken ct);
```

- [ ] **Step 3: Write the failing tests**

Create `Tests/Services/Reporting/NationalReportsTests.cs`:

```csharp
using dwa_ver_val.Services.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace dwa_ver_val.Tests.Services.Reporting;

public class NationalReportsTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ReportingService Svc(ApplicationDBContext db) =>
        new(db, new ScopedCaseQuery(db), new MemoryCache(new MemoryCacheOptions()));

    private static AuditLog Audit(string user, string action, string entityType, DateTime ts) => new()
    {
        AuditLogId = Guid.NewGuid(), UserName = user, Action = action,
        EntityType = entityType, EntityId = Guid.NewGuid().ToString(), Timestamp = ts
    };

    [Fact]
    public async Task UserActivity_CountsActionsPerOfficer_OrderedDesc()
    {
        using var db = NewDb();
        db.AuditLogs.AddRange(
            Audit("alice", "WorkflowStepCompleted", "FileMaster", new DateTime(2026, 1, 2)),
            Audit("alice", "LetterIssued", "LetterIssuance", new DateTime(2026, 1, 3)),
            Audit("bob", "Login", "ApplicationUser", new DateTime(2026, 1, 4)));
        await db.SaveChangesAsync();

        var table = await Svc(db).UserActivityAsync(new ReportFilter(), CancellationToken.None);

        Assert.Equal("User Activity", table.Title);
        Assert.Equal("alice", table.Rows[0][0]); // most actions first
        Assert.Equal("2", table.Rows[0][1]);
        Assert.Equal("bob", table.Rows[1][0]);
        Assert.Equal("1", table.Rows[1][1]);
    }

    [Fact]
    public async Task UserActivity_RespectsDateFilter()
    {
        using var db = NewDb();
        db.AuditLogs.AddRange(
            Audit("alice", "X", "FileMaster", new DateTime(2020, 1, 1)),   // out of range
            Audit("alice", "Y", "FileMaster", new DateTime(2026, 6, 1)));  // in range
        await db.SaveChangesAsync();

        var table = await Svc(db).UserActivityAsync(
            new ReportFilter(DateFrom: new DateOnly(2026, 1, 1), DateTo: new DateOnly(2026, 12, 31)),
            CancellationToken.None);

        var row = Assert.Single(table.Rows);
        Assert.Equal("alice", row[0]);
        Assert.Equal("1", row[1]); // only the in-range action
    }

    [Fact]
    public async Task PublicPortalUsage_SnapshotMetrics()
    {
        using var db = NewDb();
        db.PublicUsers.AddRange(
            new PublicUser { PublicUserId = Guid.NewGuid(), EmailAddress = "a@x.com", PasswordHash = "h", FirstName = "A", LastName = "A", EmailConfirmed = true, MfaEnabled = true, LastLoginDate = DateTime.UtcNow, RegistrationDate = DateTime.UtcNow },
            new PublicUser { PublicUserId = Guid.NewGuid(), EmailAddress = "b@x.com", PasswordHash = "h", FirstName = "B", LastName = "B", EmailConfirmed = false, MfaEnabled = false, RegistrationDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var table = await Svc(db).PublicPortalUsageAsync(new ReportFilter(), CancellationToken.None);

        Assert.Equal("Public Portal Usage", table.Title);
        Assert.Contains(table.Rows, r => r[0] == "Registrations" && r[1] == "2");
        Assert.Contains(table.Rows, r => r[0] == "Email Confirmed" && r[1] == "1");
        Assert.Contains(table.Rows, r => r[0] == "MFA Enabled" && r[1] == "1");
        Assert.Contains(table.Rows, r => r[0] == "Have Logged In" && r[1] == "1");
    }

    [Fact]
    public async Task IntegrationHealth_CountsIntegrationActions_EmptyWhenNone()
    {
        using var db = NewDb();
        db.AuditLogs.Add(Audit("system", "IntegrationSent", "FileMaster", new DateTime(2026, 1, 2)));
        await db.SaveChangesAsync();

        var table = await Svc(db).IntegrationHealthAsync(new ReportFilter(), CancellationToken.None);

        Assert.Equal("Integration Health", table.Title);
        var row = Assert.Single(table.Rows);
        Assert.Equal("IntegrationSent", row[0]);
        Assert.Equal("1", row[1]);
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~NationalReportsTests`
Expected: FAIL — the methods don't exist.

- [ ] **Step 5: Implement the national cache helper + three methods**

In `Services/Reporting/ReportingService.cs`, add a national cache helper next to the existing `CachedAsync`:
```csharp
    // National reports are not WMA-scoped — the same data for every authorized (NationalManager+)
    // caller, so the cache key omits the scope signature.
    private Task<ReportTable> CachedNationalAsync(string report, ReportFilter f, Func<Task<ReportTable>> build)
    {
        var key = $"rpt:{report}:national:{f.DateFrom}:{f.DateTo}";
        return _cache.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return build();
        })!;
    }
```

Add the three methods (anywhere in the class, after the existing report methods). Note `using System.Globalization;` is already present from Plan A; ensure `using Microsoft.EntityFrameworkCore;` is present.

```csharp
    public Task<ReportTable> UserActivityAsync(ReportFilter filter, CancellationToken ct)
        => CachedNationalAsync("useractivity", filter, async () =>
        {
            var q = _db.AuditLogs.AsNoTracking();
            if (filter.DateFrom is { } from) q = q.Where(a => a.Timestamp >= from.ToDateTime(TimeOnly.MinValue));
            if (filter.DateTo is { } to) q = q.Where(a => a.Timestamp < to.ToDateTime(TimeOnly.MinValue).AddDays(1));

            var rows = await q
                .GroupBy(a => a.UserName ?? "(system)")
                .Select(g => new { Officer = g.Key, Actions = g.Count(), Last = g.Max(x => x.Timestamp) })
                .OrderByDescending(x => x.Actions)
                .ToListAsync(ct);

            // NOTE: spec §5 also lists "cases completed" per officer; that needs a discrete
            // completion-event signal not yet modelled, so it is deferred. Actions + last-activity
            // give the core oversight value now.
            var tableRows = rows
                .Select(r => (IReadOnlyList<string>)new[]
                {
                    r.Officer, r.Actions.ToString(), r.Last.ToString("yyyy-MM-dd HH:mm"),
                })
                .ToList();

            return new ReportTable("User Activity",
                new[] { new ReportColumn("Officer"), new ReportColumn("Actions", true), new ReportColumn("Last Activity") },
                tableRows);
        });

    public Task<ReportTable> PublicPortalUsageAsync(ReportFilter filter, CancellationToken ct)
        => CachedNationalAsync("portalusage", filter, async () =>
        {
            // Snapshot of current totals; the date filter is intentionally not applied here.
            var registrations = await _db.PublicUsers.CountAsync(ct);
            var confirmed = await _db.PublicUsers.CountAsync(u => u.EmailConfirmed, ct);
            var mfa = await _db.PublicUsers.CountAsync(u => u.MfaEnabled, ct);
            var loggedIn = await _db.PublicUsers.CountAsync(u => u.LastLoginDate != null, ct);
            var portalDocs = await _db.Documents.CountAsync(d => d.UploadedByPublicUserId != null, ct);

            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "Registrations", registrations.ToString() },
                new[] { "Email Confirmed", confirmed.ToString() },
                new[] { "MFA Enabled", mfa.ToString() },
                new[] { "Have Logged In", loggedIn.ToString() },
                new[] { "Documents Uploaded (portal)", portalDocs.ToString() },
            };

            return new ReportTable("Public Portal Usage",
                new[] { new ReportColumn("Metric"), new ReportColumn("Count", true) },
                rows);
        });

    public Task<ReportTable> IntegrationHealthAsync(ReportFilter filter, CancellationToken ct)
        => CachedNationalAsync("integration", filter, async () =>
        {
            // Shell: populated once IntegrationService/eWULAAS writes IntegrationSent/IntegrationReceived
            // audit actions. Empty until then.
            var q = _db.AuditLogs.AsNoTracking()
                .Where(a => a.Action == "IntegrationSent" || a.Action == "IntegrationReceived");
            if (filter.DateFrom is { } from) q = q.Where(a => a.Timestamp >= from.ToDateTime(TimeOnly.MinValue));
            if (filter.DateTo is { } to) q = q.Where(a => a.Timestamp < to.ToDateTime(TimeOnly.MinValue).AddDays(1));

            var rows = await q
                .GroupBy(a => a.Action)
                .Select(g => new { Action = g.Key, Count = g.Count(), Last = g.Max(x => x.Timestamp) })
                .OrderBy(x => x.Action)
                .ToListAsync(ct);

            var tableRows = rows
                .Select(r => (IReadOnlyList<string>)new[]
                {
                    r.Action, r.Count.ToString(), r.Last.ToString("yyyy-MM-dd HH:mm"),
                })
                .ToList();

            return new ReportTable("Integration Health",
                new[] { new ReportColumn("Integration Action"), new ReportColumn("Count", true), new ReportColumn("Last Occurred") },
                tableRows);
        });
```

- [ ] **Step 6: Run the tests to verify they pass + build**

Run: `dotnet test --filter FullyQualifiedName~NationalReportsTests` → PASS (4 tests).
Run: `dotnet build` → Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add Services/Auth/DwsPolicies.cs Services/Reporting/IReportingService.cs Services/Reporting/ReportingService.cs Tests/Services/Reporting/NationalReportsTests.cs
git commit -m "feat(reporting): national oversight reports (User Activity, Portal Usage, Integration Health) + policy"
```

---

## Task 2: Controller actions + index + tests + verification

**Files:**
- Modify: `Controllers/ReportsController.cs`, `Views/Reports/Index.cshtml`
- Test: `Tests/Controllers/ReportsControllerTests.cs`

- [ ] **Step 1: Add the failing controller tests**

In `Tests/Controllers/ReportsControllerTests.cs`, extend the `FakeReporting` stub to implement the three new interface methods, and add tests. First add to `FakeReporting`:
```csharp
        public Task<ReportTable> UserActivityAsync(ReportFilter f, CancellationToken ct)
            => Task.FromResult(new ReportTable("User Activity",
                new[] { new ReportColumn("Officer"), new ReportColumn("Actions", true) },
                new[] { (IReadOnlyList<string>)new[] { "alice", "3" } }));
        public Task<ReportTable> PublicPortalUsageAsync(ReportFilter f, CancellationToken ct)
            => Task.FromResult(new ReportTable("Public Portal Usage",
                new[] { new ReportColumn("Metric"), new ReportColumn("Count", true) },
                new[] { (IReadOnlyList<string>)new[] { "Registrations", "5" } }));
        public Task<ReportTable> IntegrationHealthAsync(ReportFilter f, CancellationToken ct)
            => Task.FromResult(new ReportTable("Integration Health",
                new[] { new ReportColumn("Integration Action") },
                Array.Empty<IReadOnlyList<string>>()));
```
Then add tests:
```csharp
    [Fact]
    public async Task UserActivity_Html_ReturnsViewWithTable()
    {
        var result = await Build().UserActivity(new ReportFilter(), null, CancellationToken.None);
        var view = Assert.IsType<ViewResult>(result);
        var table = Assert.IsType<ReportTable>(view.Model);
        Assert.Equal("User Activity", table.Title);
        Assert.Equal("Report", view.ViewName);
    }

    [Fact]
    public async Task PublicPortalUsage_Csv_ReturnsFile()
    {
        var result = await Build().PublicPortalUsage(new ReportFilter(), "csv", CancellationToken.None);
        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv", file.ContentType);
    }

    [Fact]
    public async Task IntegrationHealth_Html_ReturnsView()
    {
        var result = await Build().IntegrationHealth(new ReportFilter(), null, CancellationToken.None);
        Assert.IsType<ViewResult>(result);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~ReportsControllerTests`
Expected: FAIL — `FakeReporting` won't compile until the controller has the actions referencing the interface methods (and the actions don't exist yet). (Compile error counts as the failing state.)

- [ ] **Step 3: Add the controller actions**

In `Controllers/ReportsController.cs`, add three actions, each gated NationalManager+. They reuse the existing `RenderAsync(filter, build, format, ct)`:
```csharp
    [Authorize(Policy = DwsPolicies.CanViewNationalReports)]
    public Task<IActionResult> UserActivity(ReportFilter filter, string? format, CancellationToken ct)
        => RenderAsync(filter, () => _reporting.UserActivityAsync(filter, ct), format, ct);

    [Authorize(Policy = DwsPolicies.CanViewNationalReports)]
    public Task<IActionResult> PublicPortalUsage(ReportFilter filter, string? format, CancellationToken ct)
        => RenderAsync(filter, () => _reporting.PublicPortalUsageAsync(filter, ct), format, ct);

    [Authorize(Policy = DwsPolicies.CanViewNationalReports)]
    public Task<IActionResult> IntegrationHealth(ReportFilter filter, string? format, CancellationToken ct)
        => RenderAsync(filter, () => _reporting.IntegrationHealthAsync(filter, ct), format, ct);
```
(Class-level `[Authorize(CanRead)]` stays; the action-level `[Authorize(CanViewNationalReports)]` adds the stricter requirement — both must pass, so effectively NationalManager+.)

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~ReportsControllerTests` → PASS (existing + 3 new).

- [ ] **Step 5: Add national reports to the index (shown only to authorized users)**

In `Views/Reports/Index.cshtml`, at the top add:
```cshtml
@using Microsoft.AspNetCore.Authorization
@inject IAuthorizationService Auth
@{
    var canViewNational = (await Auth.AuthorizeAsync(User, DwsPolicies.CanViewNationalReports)).Succeeded;
}
```
After the existing `<ul>` of three reports (still inside the card), add:
```cshtml
    @if (canViewNational)
    {
        <div class="form-section-title" style="margin-top: 16px;">National Oversight (managers)</div>
        <ul style="line-height: 2;">
            <li><a asp-action="UserActivity">User Activity</a> &mdash; actions per officer</li>
            <li><a asp-action="PublicPortalUsage">Public Portal Usage</a> &mdash; registrations, logins, document uploads</li>
            <li><a asp-action="IntegrationHealth">Integration Health</a> &mdash; eWULAAS/WARMS sync activity (populates once integration is live)</li>
        </ul>
    }
```

- [ ] **Step 6: Build + full reporting tests**

Run: `dotnet build` → Build succeeded (Razor compiles).
Run: `dotnet test --filter FullyQualifiedName~Reporting` and `dotnet test --filter FullyQualifiedName~ReportsControllerTests` → all pass.

- [ ] **Step 7: Commit**

```bash
git add Controllers/ReportsController.cs Views/Reports/Index.cshtml Tests/Controllers/ReportsControllerTests.cs
git commit -m "feat(reporting): national report actions (NationalManager+) + index links"
```

---

## Task 3: Verification

- [ ] **Step 1: Build** — `dotnet build` → 0 errors.
- [ ] **Step 2: Full regression** — `dotnet test` → all pass (existing + 4 NationalReports + 3 controller tests). SQL Server must be up for integration tests.
- [ ] **Step 3: EF translation check (SQL Server available at localhost:1433)** — run the three national report queries against live SQL (e.g. via a throwaway harness or the running app); confirm `GroupBy(UserName)` / `GroupBy(Action)` with `Max(Timestamp)` and the PublicUser scalar counts translate without client-eval. Delete any throwaway after.
- [ ] **Step 4: Manual check (app running)** — log in as NationalManager: Reports index shows the "National Oversight" section; each report renders + exports; Integration Health shows the empty state. Log in as RegionalManager/Validator: the National Oversight section is hidden, and hitting `/Reports/UserActivity` directly returns 403/AccessDenied (policy enforced).
- [ ] **Step 5: Final commit (if tweaks)** — `git commit -am "test(reporting): national reports verified"`.

---

## Self-Review Notes (author)

- **Spec coverage:** reports 4 (User Activity), 5 (Public Portal Usage), 6 (Integration Health) from Appendix C §14.2; RBAC = NationalManager+ via `CanViewNationalReports`; national (unscoped) per the confirmed decision; reuses exporters + generic view.
- **Security:** national reports are gated by `CanViewNationalReports` (AtLeastNationalManager) at the action level on top of the class-level `CanRead`; the index hides the section from unauthorized users; direct navigation is still policy-blocked (Step 4 manual check).
- **Documented deviations:** User Activity omits "cases completed" (no completion-event model — deferred, code comment); Public Portal Usage is a snapshot (date filter not applied — code comment); Integration Health is a shell (empty until IntegrationService — code comment).
- **Type consistency:** new `IReportingService` methods take `(ReportFilter, CancellationToken)` (no `ClaimsPrincipal`, since national/unscoped) and return `ReportTable`; controller actions reuse `RenderAsync(filter, build, format, ct)`; `FakeReporting` in tests implements all six interface methods.
- **EF translation:** national queries are simple `GroupBy` + `Max`/`Count` and scalar `CountAsync` — verify against live SQL in Task 3 (InMemory can't prove translation).
- **Caching:** national results use a scope-free key (`CachedNationalAsync`) since all authorized callers see identical national data.
