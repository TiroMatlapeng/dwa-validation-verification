namespace dwa_ver_val.Services.Workflow;

/// <summary>
/// Thrown by <see cref="WorkflowService"/> when a concurrent workflow transition
/// on the same <see cref="WorkflowInstance"/> wins the rowversion check and the
/// current caller's <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>
/// is translated into a domain-level error.
/// </summary>
/// <remarks>
/// Callers (controllers, API endpoints) should surface this to the operator as
/// "This case was advanced by another user. Refresh and retry." so they can reload
/// and decide whether their advance is still appropriate.
/// </remarks>
public sealed class WorkflowConcurrencyException : InvalidOperationException
{
    public WorkflowConcurrencyException()
        : base("This case was advanced by another user. Refresh and retry.")
    {
    }

    public WorkflowConcurrencyException(string message)
        : base(message)
    {
    }

    public WorkflowConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
