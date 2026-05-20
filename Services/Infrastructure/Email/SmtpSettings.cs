namespace dwa_ver_val.Services.Infrastructure.Email;

public class SmtpSettings
{
    public string? Host { get; init; }
    public int Port { get; init; } = 587;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string FromAddress { get; init; } = "noreply@dws.gov.za";
    public string FromName { get; init; } = "DWA V&V System";
    public bool UseSsl { get; init; } = true;
}
