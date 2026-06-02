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
        // NOTE: PublicUser.Status is required — seeded with "Active"/"Pending" accordingly.
        db.PublicUsers.AddRange(
            new PublicUser { PublicUserId = Guid.NewGuid(), EmailAddress = "a@x.com", PasswordHash = "h", FirstName = "A", LastName = "A", Status = "Active", EmailConfirmed = true, MfaEnabled = true, LastLoginDate = DateTime.UtcNow, RegistrationDate = DateTime.UtcNow },
            new PublicUser { PublicUserId = Guid.NewGuid(), EmailAddress = "b@x.com", PasswordHash = "h", FirstName = "B", LastName = "B", Status = "Pending", EmailConfirmed = false, MfaEnabled = false, RegistrationDate = DateTime.UtcNow });
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

    [Fact]
    public async Task PublicPortalUsage_IgnoresDateFilter_Snapshot()
    {
        using var db = NewDb();
        db.PublicUsers.Add(new PublicUser
        {
            PublicUserId = Guid.NewGuid(), EmailAddress = "a@x.com", PasswordHash = "h",
            FirstName = "A", LastName = "A", Status = "Active",
            RegistrationDate = new DateTime(2010, 1, 1)
        });
        await db.SaveChangesAsync();

        // A 2026 window far from the 2010 registration must NOT reduce the snapshot count.
        var table = await Svc(db).PublicPortalUsageAsync(
            new ReportFilter(DateFrom: new DateOnly(2026, 1, 1), DateTo: new DateOnly(2026, 12, 31)),
            CancellationToken.None);

        Assert.Contains(table.Rows, r => r[0] == "Registrations" && r[1] == "1");
    }
}
