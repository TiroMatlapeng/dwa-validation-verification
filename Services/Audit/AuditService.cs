using System.Text.Json;
using dwa_ver_val.Services.Audit;

public class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly ApplicationDBContext _db;

    public AuditService(ApplicationDBContext db)
    {
        _db = db;
    }

    public async Task LogAsync(AuditEvent evt)
    {
        var row = new AuditLog
        {
            AuditLogId = Guid.NewGuid(),
            EntityType = evt.EntityType,
            EntityId = evt.EntityId,
            Action = evt.Action,
            ApplicationUserId = evt.UserId,
            UserName = evt.UserDisplayName,
            OldValues = evt.FromValue is null ? null : JsonSerializer.Serialize(new { value = evt.FromValue }, JsonOptions),
            NewValues = evt.ToValue is null ? null : JsonSerializer.Serialize(new { value = evt.ToValue }, JsonOptions),
            Description = evt.Reason,
            IPAddress = evt.IPAddress,
            Timestamp = evt.OccurredAt?.ToUniversalTime() ?? DateTime.UtcNow
        };
        _db.AuditLogs.Add(row);
        await _db.SaveChangesAsync();
    }
}
