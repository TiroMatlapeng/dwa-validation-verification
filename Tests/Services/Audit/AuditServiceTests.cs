using System.Text.Json;
using dwa_ver_val.Services.Audit;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace dwa_ver_val.Tests.Services.Audit;

public class AuditServiceTests
{
    private static ApplicationDBContext CreateDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task LogAsync_WithFixtureEvent_WritesMatchingAuditRow()
    {
        var fixturePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "contracts", "fixtures", "audit", "audit-event.json"));
        var fixture = JsonSerializer.Deserialize<FixtureShape>(File.ReadAllText(fixturePath),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;

        var evt = new AuditEvent(
            EntityType: fixture.EntityType,
            EntityId: fixture.EntityId,
            Action: fixture.Action,
            UserId: Guid.Parse(fixture.UserId),
            UserDisplayName: fixture.UserDisplayName,
            FromValue: fixture.FromValue,
            ToValue: fixture.ToValue,
            Reason: fixture.Reason,
            IPAddress: fixture.IPAddress,
            OccurredAt: DateTime.Parse(fixture.OccurredAt, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal));

        using var db = CreateDb();
        var sut = new AuditService(db);

        await sut.LogAsync(evt);

        var row = Assert.Single(await db.AuditLogs.ToListAsync());
        Assert.Equal(fixture.EntityType, row.EntityType);
        Assert.Equal(fixture.EntityId, row.EntityId);
        Assert.Equal(fixture.Action, row.Action);
        Assert.Equal(fixture.UserDisplayName, row.UserName);
        Assert.Equal(fixture.Reason, row.Description);
        Assert.Equal(fixture.IPAddress, row.IPAddress);
        Assert.Equal(Guid.Parse(fixture.UserId), row.ApplicationUserId);
        Assert.Contains(fixture.FromValue, row.OldValues);
        Assert.Contains(fixture.ToValue, row.NewValues);
        Assert.Equal(DateTimeKind.Utc, row.Timestamp.Kind);
    }

    [Fact]
    public async Task LogAsync_DefaultsOccurredAt_ToUtcNow_WhenOmitted()
    {
        using var db = CreateDb();
        var sut = new AuditService(db);
        var before = DateTime.UtcNow.AddSeconds(-1);

        await sut.LogAsync(new AuditEvent("User", Guid.NewGuid().ToString(), "UserCreated"));

        var row = Assert.Single(await db.AuditLogs.ToListAsync());
        Assert.True(row.Timestamp >= before);
        Assert.True(row.Timestamp <= DateTime.UtcNow.AddSeconds(1));
    }

    private record FixtureShape(
        string EntityType,
        string EntityId,
        string Action,
        string UserId,
        string UserDisplayName,
        string FromValue,
        string ToValue,
        string Reason,
        string IPAddress,
        string OccurredAt);
}
