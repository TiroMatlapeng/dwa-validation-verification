namespace dwa_ver_val.Services.Audit;

/// <summary>
/// Structured payload accepted by <see cref="IAuditService.LogAsync(AuditEvent)"/>.
/// Maps onto an AuditLog row per docs/contracts/audit-event.md.
/// </summary>
public record AuditEvent(
    string EntityType,
    string EntityId,
    string Action,
    Guid? UserId = null,
    string? UserDisplayName = null,
    string? FromValue = null,
    string? ToValue = null,
    string? Reason = null,
    string? IPAddress = null,
    DateTime? OccurredAt = null);
