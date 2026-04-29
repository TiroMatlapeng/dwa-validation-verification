using dwa_ver_val.Services.Audit;

namespace dwa_ver_val.Tests.Helpers;

/// <summary>
/// No-op <see cref="IAuditService"/> for tests that don't assert on audit output.
/// Exposed internals let a test inspect captured events if needed.
/// </summary>
public class TestAuditService : IAuditService
{
    public List<AuditEvent> Events { get; } = new();

    public Task LogAsync(AuditEvent evt)
    {
        Events.Add(evt);
        return Task.CompletedTask;
    }
}
