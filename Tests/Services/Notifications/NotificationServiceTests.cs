using dwa_ver_val.Services.Infrastructure.Email;
using dwa_ver_val.Services.Notifications;
using dwa_ver_val.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Services.Notifications;

public class NotificationServiceTests
{
    private static PublicUser MakeUser(Guid id) => new()
    {
        PublicUserId = id,
        EmailAddress = "user@test.com",
        PasswordHash = "hash",
        FirstName = "Alice",
        LastName = "Smith",
        Status = "Active",
        IsHDI = false
    };

    [Fact]
    public async Task NotifyPublicUser_HappyPath_CreatesRecordAndSendsEmail()
    {
        var db = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        db.PublicUsers.Add(MakeUser(userId));
        await db.SaveChangesAsync();

        var email = new Mock<IEmailSender>();
        email.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);

        var svc = new NotificationService(db, email.Object, NullLogger<NotificationService>.Instance);
        await svc.NotifyPublicUserAsync(userId, null, "Letter", "Letter issued", "Your S35 Letter 1 is ready.", null);

        var n = db.Notifications.Single();
        Assert.Equal(userId, n.PublicUserId);
        Assert.Equal("Letter", n.NotificationType);
        Assert.True(n.EmailSent);
        email.Verify(e => e.SendAsync(It.Is<EmailMessage>(m => m.To == "user@test.com"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyPublicUser_EmailFails_StillSavesRecord()
    {
        var db = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        db.PublicUsers.Add(MakeUser(userId));
        await db.SaveChangesAsync();

        var email = new Mock<IEmailSender>();
        email.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(false);

        var svc = new NotificationService(db, email.Object, NullLogger<NotificationService>.Instance);
        await svc.NotifyPublicUserAsync(userId, null, "Letter", "Subject", "Body", null);

        var n = db.Notifications.Single();
        Assert.False(n.EmailSent);
    }

    [Fact]
    public async Task NotifyPublicUser_UnknownUser_DoesNotThrowAndNoRecord()
    {
        var db = TestDbContextFactory.Create();
        var email = new Mock<IEmailSender>();
        var svc = new NotificationService(db, email.Object, NullLogger<NotificationService>.Instance);

        await svc.NotifyPublicUserAsync(Guid.NewGuid(), null, "Letter", "Subject", "Body", null);

        Assert.Empty(db.Notifications);
        email.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
