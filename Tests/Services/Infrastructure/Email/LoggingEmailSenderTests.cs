using dwa_ver_val.Services.Infrastructure.Email;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Services.Infrastructure.Email;

public class LoggingEmailSenderTests
{
    [Fact]
    public async Task SendAsync_LogsRecipientAndSubject()
    {
        var logger = new Mock<ILogger<LoggingEmailSender>>();
        var sender = new LoggingEmailSender(logger.Object);

        var msg = new EmailMessage
        {
            To = "user@example.com",
            Subject = "Confirm your account",
            BodyText = "Click here: https://portal/confirm/abc"
        };

        var ok = await sender.SendAsync(msg, CancellationToken.None);

        Assert.True(ok);
        logger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("user@example.com") && v.ToString()!.Contains("Confirm your account")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendAsync_ReturnsFalse_WhenToIsEmpty()
    {
        var logger = new Mock<ILogger<LoggingEmailSender>>();
        var sender = new LoggingEmailSender(logger.Object);

        var msg = new EmailMessage { To = "", Subject = "x", BodyText = "y" };

        var ok = await sender.SendAsync(msg, CancellationToken.None);

        Assert.False(ok);
    }
}
