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
