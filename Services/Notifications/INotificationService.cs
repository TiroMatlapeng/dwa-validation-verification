namespace dwa_ver_val.Services.Notifications;

public interface INotificationService
{
    Task NotifyPublicUserAsync(
        Guid publicUserId, Guid? fileMasterId,
        string notificationType, string subject, string body, string? actionUrl,
        CancellationToken ct = default);

    Task NotifyDwsValidatorAsync(
        Guid fileMasterId, string notificationType,
        string subject, string body,
        CancellationToken ct = default);
}
