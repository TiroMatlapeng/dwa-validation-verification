namespace dwa_ver_val.Services.Workflow;

/// <summary>
/// Evaluated by <see cref="IWorkflowService"/> before any state change.
/// Return <see cref="GuardResult.Ok"/> to permit the transition, or
/// <see cref="GuardResult.Deny"/> with a human-readable reason.
/// </summary>
public interface ITransitionGuard
{
    Task<GuardResult> CheckAsync(GuardContext ctx);
}

public record GuardContext(
    FileMaster FileMaster,
    WorkflowState CurrentState,
    WorkflowState TargetState);

public record GuardResult(bool Allowed, string? Reason)
{
    public static readonly GuardResult Ok = new(true, null);
    public static GuardResult Deny(string reason) => new(false, reason);
}
