using dwa_ver_val.Services.Audit;

public interface IAuditService
{
    /// <summary>
    /// Writes an immutable AuditLog row. Never updates or deletes existing rows.
    /// </summary>
    Task LogAsync(AuditEvent evt);
}
