public class Notification
{
    public Guid NotificationId { get; set; }
    public Guid? ApplicationUserId { get; set; }
    public ApplicationUser? ApplicationUser { get; set; }
    public Guid? PublicUserId { get; set; }
    public PublicUser? PublicUser { get; set; }
    public Guid? FileMasterId { get; set; }
    public FileMaster? FileMaster { get; set; }
    public required string NotificationType { get; set; } // WorkflowStep, Letter, CaseStatus, Comment, Upload, Protest, System
    public required string Subject { get; set; }
    public string? Body { get; set; }
    public string? ActionUrl { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ReadDate { get; set; }
    public bool IsRead { get; set; }
    public bool EmailSent { get; set; }
    public DateTime? EmailSentDate { get; set; }
    public bool SmsSent { get; set; }
    public DateTime? SmsSentDate { get; set; }
}
