using Microsoft.Extensions.Logging;

namespace dwa_ver_val.Services.Portal.Mfa;

public class LoggingSmsGateway : ISmsGateway
{
    private readonly ILogger<LoggingSmsGateway> _logger;

    public LoggingSmsGateway(ILogger<LoggingSmsGateway> logger)
        => _logger = logger;

    public Task<bool> SendAsync(string to, string body, CancellationToken ct = default)
    {
        _logger.LogInformation("[SMS STUB] To: {To} | Body: {Body}", to, body);
        return Task.FromResult(true);
    }
}
