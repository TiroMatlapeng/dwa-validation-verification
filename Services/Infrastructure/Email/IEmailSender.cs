namespace dwa_ver_val.Services.Infrastructure.Email;

public interface IEmailSender
{
    /// <summary>
    /// Best-effort email dispatch. Returns true on success. Implementations
    /// MUST NOT throw on transient failures — log and return false instead so
    /// callers can flip the appropriate "EmailSent" persistence flag.
    /// </summary>
    Task<bool> SendAsync(EmailMessage message, CancellationToken ct);
}
