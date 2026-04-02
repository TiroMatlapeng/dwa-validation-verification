using dwa_ver_val.Tests.Helpers;

namespace dwa_ver_val.Tests.Models;

public class ModelValidationTests
{
    [Fact]
    public async Task AuditLog_Is_Immutable_Record()
    {
        using var context = TestDbContextFactory.Create();

        var log = new AuditLog
        {
            AuditLogId = Guid.NewGuid(),
            EntityType = "FileMaster",
            EntityId = Guid.NewGuid().ToString(),
            Action = "StateTransition",
            OldValues = "{\"State\":\"CP1\"}",
            NewValues = "{\"State\":\"CP2\"}",
            Timestamp = DateTime.UtcNow,
            UserName = "validator@dws.gov.za"
        };
        context.AuditLogs.Add(log);
        await context.SaveChangesAsync();

        var retrieved = await context.AuditLogs.FindAsync(log.AuditLogId);
        Assert.NotNull(retrieved);
        Assert.Equal("StateTransition", retrieved.Action);
        Assert.Equal("FileMaster", retrieved.EntityType);
    }

    [Fact]
    public async Task LetterType_Stores_NWA_Section()
    {
        using var context = TestDbContextFactory.Create();

        var types = new[]
        {
            new LetterType { LetterTypeId = Guid.NewGuid(), LetterName = "Letter 1", LetterDescription = "Notice to apply", NWASection = "S35(1)" },
            new LetterType { LetterTypeId = Guid.NewGuid(), LetterName = "Letter 1A", LetterDescription = "Directive to apply", NWASection = "S53(1)" },
            new LetterType { LetterTypeId = Guid.NewGuid(), LetterName = "Letter 2", LetterDescription = "Request additional info", NWASection = "S35(3)(a)" },
            new LetterType { LetterTypeId = Guid.NewGuid(), LetterName = "Letter 3", LetterDescription = "Confirmation of ELU", NWASection = "S35(4)" },
        };
        context.LetterTypes.AddRange(types);
        await context.SaveChangesAsync();

        Assert.Equal(4, context.LetterTypes.Count());
        var letter3 = context.LetterTypes.First(lt => lt.LetterName == "Letter 3");
        Assert.Equal("S35(4)", letter3.NWASection);
    }

    [Fact]
    public async Task Document_Tracks_Virus_Scan_And_Hash()
    {
        using var context = TestDbContextFactory.Create();

        var doc = new Document
        {
            DocumentId = Guid.NewGuid(),
            DocumentType = "TitleDeed",
            FileName = "title_deed_T1234.pdf",
            BlobPath = "/storage/documents/title_deed_T1234.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 2048576,
            UploadDate = DateTime.UtcNow,
            VirusScanStatus = "Clean",
            DocumentHash = "a1b2c3d4e5f6789012345678901234567890abcdef1234567890abcdef123456"
        };
        context.Documents.Add(doc);
        await context.SaveChangesAsync();

        var retrieved = await context.Documents.FindAsync(doc.DocumentId);
        Assert.Equal("Clean", retrieved!.VirusScanStatus);
        Assert.Equal(64, retrieved.DocumentHash!.Length); // SHA-256 hex
    }

    [Fact]
    public async Task Notification_Supports_Both_Internal_And_External_Users()
    {
        using var context = TestDbContextFactory.Create();

        var appUser = new ApplicationUser
        {
            ApplicationUserId = Guid.NewGuid(),
            FirstName = "Internal",
            LastName = "User",
            EmployeeNumber = "EMP100"
        };
        context.ApplicationUsers.Add(appUser);

        var publicUser = new PublicUser
        {
            PublicUserId = Guid.NewGuid(),
            EmailAddress = "external@example.co.za",
            PasswordHash = "hashed",
            FirstName = "External",
            LastName = "User",
            Status = "Active",
            RegistrationDate = DateTime.UtcNow
        };
        context.PublicUsers.Add(publicUser);

        var internalNotif = new Notification
        {
            NotificationId = Guid.NewGuid(),
            ApplicationUserId = appUser.ApplicationUserId,
            NotificationType = "WorkflowStep",
            Subject = "Case REG-001 advanced to CP2",
            CreatedDate = DateTime.UtcNow
        };

        var externalNotif = new Notification
        {
            NotificationId = Guid.NewGuid(),
            PublicUserId = publicUser.PublicUserId,
            NotificationType = "Letter",
            Subject = "Letter 1 issued for your property",
            CreatedDate = DateTime.UtcNow,
            EmailSent = true,
            EmailSentDate = DateTime.UtcNow
        };

        context.Notifications.AddRange(internalNotif, externalNotif);
        await context.SaveChangesAsync();

        Assert.Equal(2, context.Notifications.Count());
        Assert.True(context.Notifications.Any(n => n.ApplicationUserId != null));
        Assert.True(context.Notifications.Any(n => n.PublicUserId != null));
    }

    [Fact]
    public async Task WorkflowState_Has_Phase_And_DisplayOrder()
    {
        using var context = TestDbContextFactory.Create();

        var states = new[]
        {
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "NotStarted", Phase = "Inception", DisplayOrder = 0, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP1_Complete", Phase = "Inception", DisplayOrder = 7, IsTerminal = false },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "Closed", Phase = "Complete", DisplayOrder = 99, IsTerminal = true },
        };
        context.WorkflowStates.AddRange(states);
        await context.SaveChangesAsync();

        var terminal = context.WorkflowStates.Where(ws => ws.IsTerminal).ToList();
        Assert.Single(terminal);
        Assert.Equal("Closed", terminal[0].StateName);
    }

    [Fact]
    public async Task DigitalSignature_Captures_Audit_Fields()
    {
        using var context = TestDbContextFactory.Create();

        var doc = new Document
        {
            DocumentId = Guid.NewGuid(),
            DocumentType = "Letter",
            FileName = "letter1.pdf",
            BlobPath = "/letters/letter1.pdf",
            UploadDate = DateTime.UtcNow
        };
        context.Documents.Add(doc);

        var sig = new DigitalSignature
        {
            SignatureId = Guid.NewGuid(),
            DocumentId = doc.DocumentId,
            SignatureHash = "abc123def456",
            SignedAt = DateTime.UtcNow,
            IPAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0",
            Reason = "Section 35(4) Confirmation",
            DocumentHashAtSigning = "xyz789"
        };
        context.DigitalSignatures.Add(sig);
        await context.SaveChangesAsync();

        var retrieved = await context.DigitalSignatures.FindAsync(sig.SignatureId);
        Assert.Equal("192.168.1.100", retrieved!.IPAddress);
        Assert.Equal("Section 35(4) Confirmation", retrieved.Reason);
    }
}
