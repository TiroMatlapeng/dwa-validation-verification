using System.Globalization;
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
                    r.Total == 0 ? "0.0%" : (100.0 * r.Completed / r.Total).ToString("0.0", CultureInfo.InvariantCulture) + "%",
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

    public Task<ReportTable> LetterTrackingAsync(ReportFilter filter, ClaimsPrincipal user, CancellationToken ct)
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

    public Task<ReportTable> ValidationSummaryAsync(ReportFilter filter, ClaimsPrincipal user, CancellationToken ct)
        => CachedAsync("validation", filter, user, async () =>
        {
            await Task.CompletedTask;
            return new ReportTable("Validation Summary",
                new[] { new ReportColumn("Catchment") },
                Array.Empty<IReadOnlyList<string>>());
        });
}
