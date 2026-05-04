using Microsoft.Extensions.Logging;

namespace dwa_ver_val.Services.Infrastructure.Email;

/// <summary>
/// Development-only sender that writes the email body to the application log.
/// MUST be replaced with a real provider before any production data lands —
/// see Program.cs startup warning and design spec §5.6.
/// </summary>
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message.To))
        {
            _logger.LogWarning("LoggingEmailSender: skipped send because To address is empty (Subject: {Subject}).", message.Subject);
            return Task.FromResult(false);
        }

        _logger.LogInformation(
            "LoggingEmailSender: would send email To={To} Subject={Subject} Body={Body}",
            message.To, message.Subject, message.BodyText);

        return Task.FromResult(true);
    }
}
