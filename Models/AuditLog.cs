public class AuditLog
{
    public Guid AuditLogId { get; set; }
    public required string EntityType { get; set; }
    public required string EntityId { get; set; }
    public Guid? ApplicationUserId { get; set; }
    public ApplicationUser? ApplicationUser { get; set; }
    public string? UserName { get; set; }
    public required string Action { get; set; }
    public string? OldValues { get; set; } // JSON
    public string? NewValues { get; set; } // JSON
    public string? Description { get; set; }
    public string? IPAddress { get; set; }
    public DateTime Timestamp { get; set; }
}
