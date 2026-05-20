using dwa_ver_val.Services.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace dwa_ver_val.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly ApplicationDBContext _db;
    private readonly IEmailSender _email;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ApplicationDBContext db, IEmailSender email,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _email = email;
        _logger = logger;
    }

    public async Task NotifyPublicUserAsync(
        Guid publicUserId, Guid? fileMasterId,
        string notificationType, string subject, string body, string? actionUrl,
        CancellationToken ct = default)
    {
        var user = await _db.PublicUsers.FindAsync(new object[] { publicUserId }, ct);
        if (user is null)
        {
            _logger.LogWarning(
                "NotificationService: PublicUser {Id} not found; skipping.", publicUserId);
            return;
        }

        var note = new Notification
        {
            NotificationId = Guid.NewGuid(),
            PublicUserId = publicUserId,
            FileMasterId = fileMasterId,
            NotificationType = notificationType,
            Subject = subject,
            Body = body,
            ActionUrl = actionUrl,
            CreatedDate = DateTime.UtcNow,
            IsRead = false
        };
        _db.Notifications.Add(note);

        var sent = await _email.SendAsync(
            new EmailMessage { To = user.EmailAddress, Subject = subject, BodyText = body }, ct);

        note.EmailSent = sent;
        if (sent) note.EmailSentDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task NotifyDwsValidatorAsync(
        Guid fileMasterId, string notificationType,
        string subject, string body,
        CancellationToken ct = default)
    {
        var fm = await _db.FileMasters
            .Include(f => f.Validator)
            .FirstOrDefaultAsync(f => f.FileMasterId == fileMasterId, ct);

        if (fm?.Validator is null)
        {
            _logger.LogWarning(
                "NotificationService: FileMaster {Id} has no Validator; skipping.", fileMasterId);
            return;
        }

        var note = new Notification
        {
            NotificationId = Guid.NewGuid(),
            ApplicationUserId = fm.ValidatorId,
            FileMasterId = fileMasterId,
            NotificationType = notificationType,
            Subject = subject,
            Body = body,
            CreatedDate = DateTime.UtcNow,
            IsRead = false
        };
        _db.Notifications.Add(note);

        var sent = await _email.SendAsync(
            new EmailMessage
            {
                To = fm.Validator.Email ?? "",
                Subject = subject,
                BodyText = body
            }, ct);

        note.EmailSent = sent;
        if (sent) note.EmailSentDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}
