using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace dwa_ver_val.Services.Infrastructure.Email;

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpSettings> settings, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message.To))
        {
            _logger.LogWarning("SmtpEmailSender: skipped — To address is empty. Subject: {Subject}", message.Subject);
            return false;
        }
        try
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
            mime.To.Add(MailboxAddress.Parse(message.To));
            mime.Subject = message.Subject;
            mime.Body = new TextPart("plain") { Text = message.BodyText ?? "" };

            using var client = new SmtpClient();
            await client.ConnectAsync(
                _settings.Host, _settings.Port,
                _settings.UseSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None,
                ct);
            if (!string.IsNullOrEmpty(_settings.Username))
                await client.AuthenticateAsync(_settings.Username, _settings.Password ?? string.Empty, ct);
            await client.SendAsync(mime, ct);
            await client.DisconnectAsync(true, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("SmtpEmailSender: failed to send to {To}. Subject: {Subject}. Error: {ErrorType}: {ErrorMessage}",
                message.To, message.Subject, ex.GetType().Name, ex.Message);
            return false;
        }
    }
}
