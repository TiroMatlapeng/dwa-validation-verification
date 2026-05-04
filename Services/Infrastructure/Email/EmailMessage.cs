namespace dwa_ver_val.Services.Infrastructure.Email;

public class EmailMessage
{
    public required string To { get; set; }
    public required string Subject { get; set; }
    public required string BodyText { get; set; }
    public string? BodyHtml { get; set; }
}
