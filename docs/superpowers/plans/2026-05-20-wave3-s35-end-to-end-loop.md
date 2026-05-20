# Wave 3 — S35 End-to-End Loop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the statutory S35 loop — DWS issues letters → water users receive notifications and access the portal → water users upload documents, submit responses, lodge objections → DWS staff see all portal activity in an inbox on the case file.

**Architecture:** A thin `NotificationService` persists `Notification` DB records and fires `IEmailSender`. Five new portal controller/view sets handle the water-user actions. A `_PortalInboxPanel` partial in the internal system surfaces those actions to DWS staff. All portal controllers use the existing `PortalAuthenticated` policy and `PublicUserPropertyAccessor` for row-level access control.

**Tech Stack:** ASP.NET Core 10 MVC, EF Core 10 / SQL Server 2022, MailKit 4.x (new NuGet), QuestPDF 2025.7 (existing), xUnit + Moq (existing test pattern).

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| **Create** | `Services/Notifications/INotificationService.cs` | Service contract |
| **Create** | `Services/Notifications/NotificationService.cs` | Persists Notification record + fires IEmailSender |
| **Create** | `Services/Infrastructure/Email/SmtpSettings.cs` | POCO bound from appsettings |
| **Create** | `Services/Infrastructure/Email/SmtpEmailSender.cs` | MailKit IEmailSender implementation |
| **Modify** | `Program.cs` | Register INotificationService; switch to SmtpEmailSender when settings present |
| **Modify** | `appsettings.json` | Add empty SmtpSettings section |
| **Modify** | `Controllers/FileMasterController.cs` | Inject INotificationService; fire notification in IssueLetter; add PortalInbox/MarkRead/PortalReply actions |
| **Create** | `Controllers/Admin/PropertyClaimsController.cs` | DWS admin: list/approve/reject property claims |
| **Create** | `Areas/ExternalPortal/Controllers/PropertyClaimController.cs` | Water user submits property claim |
| **Create** | `Areas/ExternalPortal/Controllers/CaseController.cs` | Case list + detail + letter download |
| **Create** | `Areas/ExternalPortal/Controllers/DocumentController.cs` | Upload supporting documents |
| **Create** | `Areas/ExternalPortal/Controllers/ResponseController.cs` | Submit CaseComment (letter response) |
| **Create** | `Areas/ExternalPortal/Controllers/ObjectionController.cs` | Lodge Objection/appeal |
| **Create** | `Areas/ExternalPortal/ViewModels/PropertyClaimViewModel.cs` | Claim form model |
| **Create** | `Areas/ExternalPortal/ViewModels/CaseSummaryViewModel.cs` | Case list/detail display model |
| **Create** | `Areas/ExternalPortal/ViewModels/DocumentUploadViewModel.cs` | Upload form model |
| **Create** | `Areas/ExternalPortal/ViewModels/LetterResponseViewModel.cs` | Response form model |
| **Create** | `Areas/ExternalPortal/ViewModels/ObjectionViewModel.cs` | Objection form model |
| **Create** | `Areas/ExternalPortal/Views/PropertyClaim/Submit.cshtml` | Claim submission form |
| **Create** | `Areas/ExternalPortal/Views/PropertyClaim/Pending.cshtml` | "Claim submitted — awaiting DWS approval" page |
| **Create** | `Areas/ExternalPortal/Views/Case/Index.cshtml` | My cases list |
| **Create** | `Areas/ExternalPortal/Views/Case/Detail.cshtml` | Per-case detail + letter list + activity feed |
| **Create** | `Areas/ExternalPortal/Views/Document/Upload.cshtml` | Document upload form |
| **Create** | `Areas/ExternalPortal/Views/Response/Submit.cshtml` | Letter response form |
| **Create** | `Areas/ExternalPortal/Views/Objection/Lodge.cshtml` | Objection form |
| **Modify** | `Areas/ExternalPortal/Views/Dashboard/Index.cshtml` | Add "My Cases" + "Claim a Property" links |
| **Modify** | `Areas/ExternalPortal/Views/Shared/_PortalLayout.cshtml` | Add wide-card CSS class; add minimal nav bar |
| **Create** | `Views/FileMaster/_PortalInboxPanel.cshtml` | DWS partial: portal comments/docs/objections |
| **Modify** | `Views/FileMaster/Details.cshtml` | Include _PortalInboxPanel |
| **Create** | `Views/Admin/PropertyClaims/Index.cshtml` | DWS admin: pending claim list |
| **Create** | `Tests/Services/Notifications/NotificationServiceTests.cs` | Unit tests |
| **Create** | `Tests/Areas/ExternalPortal/PropertyClaimControllerTests.cs` | Controller tests |
| **Create** | `Tests/Areas/ExternalPortal/CaseControllerTests.cs` | Controller tests |
| **Create** | `Tests/Areas/ExternalPortal/DocumentControllerTests.cs` | Controller tests |
| **Create** | `Tests/Areas/ExternalPortal/ResponseControllerTests.cs` | Controller tests |
| **Create** | `Tests/Areas/ExternalPortal/ObjectionControllerTests.cs` | Controller tests |

---

## Task 1 — SmtpEmailSender

**Files:**
- **Create:** `Services/Infrastructure/Email/SmtpSettings.cs`
- **Create:** `Services/Infrastructure/Email/SmtpEmailSender.cs`
- **Modify:** `appsettings.json` — add empty SmtpSettings section
- **Modify:** `Program.cs` — conditional registration

No test for SmtpEmailSender (it wraps a live SMTP socket — integration concern, not unit). LoggingEmailSender remains active unless SmtpSettings.Host is populated.

- [ ] **Step 1.1 — Install MailKit**

Run in project root:
```bash
dotnet add package MailKit --version 4.8.0
```
Expected: package added to dwa_ver_val.csproj.

- [ ] **Step 1.2 — Create SmtpSettings**

Create `Services/Infrastructure/Email/SmtpSettings.cs`:
```csharp
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
```

- [ ] **Step 1.3 — Create SmtpEmailSender**

Create `Services/Infrastructure/Email/SmtpEmailSender.cs`:
```csharp
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
                _settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                ct);
            if (!string.IsNullOrEmpty(_settings.Username))
                await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
            await client.SendAsync(mime, ct);
            await client.DisconnectAsync(true, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SmtpEmailSender: failed to send to {To}. Subject: {Subject}",
                message.To, message.Subject);
            return false;
        }
    }
}
```

- [ ] **Step 1.4 — Add SmtpSettings section to appsettings.json**

Open `appsettings.json` and add after the existing top-level keys:
```json
"SmtpSettings": {
  "Host": "",
  "Port": 587,
  "Username": "",
  "Password": "",
  "FromAddress": "noreply@dws.gov.za",
  "FromName": "DWA V&V System",
  "UseSsl": true
}
```

- [ ] **Step 1.5 — Conditional registration in Program.cs**

In `Program.cs`, find the line:
```csharp
builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
```
Replace it with:
```csharp
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
var smtpHost = builder.Configuration["SmtpSettings:Host"];
if (!string.IsNullOrWhiteSpace(smtpHost))
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
else
    builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
```

- [ ] **Step 1.6 — Build to verify**
```bash
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 1.7 — Commit**
```bash
git add Services/Infrastructure/Email/SmtpSettings.cs Services/Infrastructure/Email/SmtpEmailSender.cs appsettings.json Program.cs dwa_ver_val.csproj
git commit -m "feat(email): SmtpEmailSender (MailKit) — conditional registration; LoggingEmailSender stays default"
```

---

## Task 2 — INotificationService + NotificationService

**Files:**
- **Create:** `Services/Notifications/INotificationService.cs`
- **Create:** `Services/Notifications/NotificationService.cs`
- **Modify:** `Program.cs` — register INotificationService
- **Create:** `Tests/Services/Notifications/NotificationServiceTests.cs`

- [ ] **Step 2.1 — Write failing tests first**

Create `Tests/Services/Notifications/NotificationServiceTests.cs`:
```csharp
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
```

- [ ] **Step 2.2 — Run tests to confirm they fail**
```bash
cd Tests && dotnet test --filter "NotificationServiceTests" -v minimal 2>&1 | tail -5
```
Expected: compilation error — `NotificationService` does not exist.

- [ ] **Step 2.3 — Create INotificationService**

Create `Services/Notifications/INotificationService.cs`:
```csharp
namespace dwa_ver_val.Services.Notifications;

public interface INotificationService
{
    /// <summary>
    /// Persist a Notification record for a water user and send them an email.
    /// Never throws — email failures are logged and the EmailSent flag set to false.
    /// </summary>
    Task NotifyPublicUserAsync(
        Guid publicUserId, Guid? fileMasterId,
        string notificationType, string subject, string body, string? actionUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Persist a Notification for the DWS Validator on the case and email them.
    /// No-ops if the FileMaster has no assigned Validator.
    /// </summary>
    Task NotifyDwsValidatorAsync(
        Guid fileMasterId, string notificationType,
        string subject, string body,
        CancellationToken ct = default);
}
```

- [ ] **Step 2.4 — Create NotificationService**

Create `Services/Notifications/NotificationService.cs`:
```csharp
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
```

- [ ] **Step 2.5 — Register in Program.cs**

In `Program.cs`, after the `IFileStorage` registration block, add:
```csharp
builder.Services.AddScoped<dwa_ver_val.Services.Notifications.INotificationService,
    dwa_ver_val.Services.Notifications.NotificationService>();
```

- [ ] **Step 2.6 — Run tests**
```bash
cd Tests && dotnet test --filter "NotificationServiceTests" -v minimal 2>&1 | tail -8
```
Expected: 3 tests pass.

- [ ] **Step 2.7 — Commit**
```bash
git add Services/Notifications/ Program.cs Tests/Services/Notifications/
git commit -m "feat(notifications): INotificationService + NotificationService — persists Notification record, fires IEmailSender"
```

---

## Task 3 — Wire INotificationService into FileMasterController.IssueLetter

**Files:**
- **Modify:** `Controllers/FileMasterController.cs`

When DWS issues any S35 letter, find all `Approved` PublicUserProperty links for that case's property and notify each linked water user.

- [ ] **Step 3.1 — Inject INotificationService into FileMasterController**

In `Controllers/FileMasterController.cs`, find the constructor. It currently injects `ILetterService _letters`. Add `INotificationService` alongside:

Find:
```csharp
    private readonly ILetterService _letters;
```
Add immediately after:
```csharp
    private readonly dwa_ver_val.Services.Notifications.INotificationService _notify;
```

Find the constructor parameter list (it has `ILetterService letters`) and add:
```csharp
        dwa_ver_val.Services.Notifications.INotificationService notify,
```
In the constructor body, add:
```csharp
        _notify = notify;
```

- [ ] **Step 3.2 — Fire notification in IssueLetter after successful issue**

In `Controllers/FileMasterController.cs`, find the `IssueLetter` action. Locate the line:
```csharp
            await _context.SaveChangesAsync();
```
that follows the `WorkflowService` advance call (near the bottom of the action, after `_workflow.AdvanceAsync`). After that `SaveChangesAsync`, add:

```csharp
            // Notify linked water users that a letter has been issued on their case.
            var property = await _context.FileMasters
                .Where(f => f.FileMasterId == id)
                .Select(f => new { f.PropertyId })
                .FirstOrDefaultAsync();
            if (property is not null)
            {
                var linkedUsers = await _context.PublicUserProperties
                    .Where(p => p.PropertyId == property.PropertyId
                             && p.Status == dwa_ver_val.Models.Enums.PropertyClaimStatus.Approved)
                    .Select(p => p.PublicUserId)
                    .ToListAsync();
                foreach (var uid in linkedUsers)
                    await _notify.NotifyPublicUserAsync(uid, id, "Letter",
                        $"A letter has been issued on your V&V case",
                        $"Reference: {map.LetterCode}. Log in to the portal to view and respond.",
                        actionUrl: null);
            }
```

- [ ] **Step 3.3 — Build to verify**
```bash
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3.4 — Commit**
```bash
git add Controllers/FileMasterController.cs
git commit -m "feat(notifications): notify linked portal users when S35/S33 letter is issued"
```

---

## Task 4 — External Portal: Property Claim

Water users claim a property by entering its SGCode or PropertyReferenceNumber. DWS approves/rejects in an admin screen.

**Files:**
- **Create:** `Areas/ExternalPortal/ViewModels/PropertyClaimViewModel.cs`
- **Create:** `Areas/ExternalPortal/Controllers/PropertyClaimController.cs`
- **Create:** `Areas/ExternalPortal/Views/PropertyClaim/Submit.cshtml`
- **Create:** `Areas/ExternalPortal/Views/PropertyClaim/Pending.cshtml`
- **Create:** `Controllers/Admin/PropertyClaimsController.cs`
- **Create:** `Views/Admin/PropertyClaims/Index.cshtml`
- **Create:** `Tests/Areas/ExternalPortal/PropertyClaimControllerTests.cs`

- [ ] **Step 4.1 — Write failing tests**

Create `Tests/Areas/ExternalPortal/PropertyClaimControllerTests.cs`:
```csharp
using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.Controllers;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Models.Enums;
using dwa_ver_val.Services.Notifications;
using dwa_ver_val.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class PropertyClaimControllerTests
{
    private static (ApplicationDBContext db, PropertyClaimController controller) Build(Guid userId)
    {
        var db = TestDbContextFactory.Create();
        var notify = new Mock<INotificationService>();
        var controller = new PropertyClaimController(db, notify.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                }, "test"))
            }
        };
        return (db, controller);
    }

    [Fact]
    public async Task Submit_Post_ValidSGCode_CreatesPendingClaim()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);

        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            SGCode = "T1234",
            WmaId = null,
            PropertyReferenceNumber = "REF-001"
        };
        db.Properties.Add(property);
        db.PublicUsers.Add(new PublicUser
        {
            PublicUserId = userId, EmailAddress = "u@t.com",
            PasswordHash = "h", FirstName = "A", LastName = "B",
            Status = "Active", IsHDI = false
        });
        await db.SaveChangesAsync();

        var result = await controller.Submit(
            new PropertyClaimViewModel { PropertyCode = "T1234" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Pending", redirect.ActionName);

        var claim = db.PublicUserProperties.Single();
        Assert.Equal(userId, claim.PublicUserId);
        Assert.Equal(property.PropertyId, claim.PropertyId);
        Assert.Equal(PropertyClaimStatus.Pending, claim.Status);
    }

    [Fact]
    public async Task Submit_Post_UnknownCode_ReturnsViewWithError()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);
        db.PublicUsers.Add(new PublicUser
        {
            PublicUserId = userId, EmailAddress = "u@t.com",
            PasswordHash = "h", FirstName = "A", LastName = "B",
            Status = "Active", IsHDI = false
        });
        await db.SaveChangesAsync();

        var result = await controller.Submit(
            new PropertyClaimViewModel { PropertyCode = "NOPE" }, default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Submit_Post_DuplicateClaim_ReturnsViewWithError()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);

        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            SGCode = "T9999",
            WmaId = null,
            PropertyReferenceNumber = "REF-DUP"
        };
        db.Properties.Add(property);
        db.PublicUsers.Add(new PublicUser
        {
            PublicUserId = userId, EmailAddress = "u@t.com",
            PasswordHash = "h", FirstName = "A", LastName = "B",
            Status = "Active", IsHDI = false
        });
        db.PublicUserProperties.Add(new PublicUserProperty
        {
            Id = Guid.NewGuid(),
            PublicUserId = userId,
            PropertyId = property.PropertyId,
            Status = PropertyClaimStatus.Pending,
            EvidenceType = PropertyClaimEvidenceType.IdMatch,
            RequestedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await controller.Submit(
            new PropertyClaimViewModel { PropertyCode = "T9999" }, default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }
}
```

- [ ] **Step 4.2 — Run tests to confirm failure**
```bash
cd Tests && dotnet test --filter "PropertyClaimControllerTests" -v minimal 2>&1 | tail -5
```
Expected: compilation error.

- [ ] **Step 4.3 — Create PropertyClaimViewModel**

Create `Areas/ExternalPortal/ViewModels/PropertyClaimViewModel.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.Areas.ExternalPortal.ViewModels;

public class PropertyClaimViewModel
{
    [Required(ErrorMessage = "Property code is required.")]
    [Display(Name = "Property Code (SGCode or Reference Number)")]
    public string PropertyCode { get; set; } = "";
}
```

- [ ] **Step 4.4 — Create PropertyClaimController**

Create `Areas/ExternalPortal/Controllers/PropertyClaimController.cs`:
```csharp
using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Models.Enums;
using dwa_ver_val.Services.Notifications;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Areas.ExternalPortal.Controllers;

[Area("ExternalPortal")]
[Route("ExternalPortal/[controller]/[action]")]
[Authorize(Policy = PortalPolicies.PortalAuthenticated)]
public class PropertyClaimController : Controller
{
    private readonly ApplicationDBContext _db;
    private readonly INotificationService _notify;

    public PropertyClaimController(ApplicationDBContext db, INotificationService notify)
    {
        _db = db;
        _notify = notify;
    }

    [HttpGet]
    public IActionResult Submit() => View(new PropertyClaimViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(PropertyClaimViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(vm);

        var publicUserId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Portal user not authenticated."));

        var property = await _db.Properties
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.SGCode == vm.PropertyCode ||
                p.PropertyReferenceNumber == vm.PropertyCode, ct);

        if (property is null)
        {
            ModelState.AddModelError(nameof(vm.PropertyCode),
                "No property found with that code. Check your title deed or SGCode.");
            return View(vm);
        }

        var alreadyExists = await _db.PublicUserProperties.AnyAsync(p =>
            p.PublicUserId == publicUserId &&
            p.PropertyId == property.PropertyId, ct);

        if (alreadyExists)
        {
            ModelState.AddModelError(nameof(vm.PropertyCode),
                "You have already submitted a claim for this property.");
            return View(vm);
        }

        _db.PublicUserProperties.Add(new PublicUserProperty
        {
            Id = Guid.NewGuid(),
            PublicUserId = publicUserId,
            PropertyId = property.PropertyId,
            Status = PropertyClaimStatus.Pending,
            EvidenceType = PropertyClaimEvidenceType.IdMatch,
            RequestedDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Pending));
    }

    [HttpGet]
    public IActionResult Pending() => View();
}
```

- [ ] **Step 4.5 — Create Submit view**

Create `Areas/ExternalPortal/Views/PropertyClaim/Submit.cshtml`:
```html
@model dwa_ver_val.Areas.ExternalPortal.ViewModels.PropertyClaimViewModel
@{
    ViewData["Title"] = "Claim a Property";
    Layout = "~/Areas/ExternalPortal/Views/Shared/_PortalLayout.cshtml";
}
<div class="portal-card">
    <h1>Claim a Property</h1>
    <p class="muted">Enter your property's SGCode (e.g. T1234) or reference number to link it to your account. A DWS official will approve your claim before you can view case data.</p>

    <form asp-action="Submit" asp-controller="PropertyClaim" asp-area="ExternalPortal" method="post">
        @Html.AntiForgeryToken()
        <div asp-validation-summary="All" class="errors" style="display:@(ViewData.ModelState.IsValid ? "none" : "block")"></div>
        <label asp-for="PropertyCode"></label>
        <input asp-for="PropertyCode" type="text" placeholder="e.g. T1234 or REF-001" autocomplete="off" />
        <span asp-validation-for="PropertyCode" class="errors" style="padding:4px 0;border:none;background:none;font-size:var(--dws-fs-xs)"></span>
        <div class="actions">
            <button type="submit" class="btn-primary">Submit Claim</button>
            <a href="@Url.Action("Index", "Dashboard", new { area = "ExternalPortal" })">Cancel</a>
        </div>
    </form>
</div>
```

- [ ] **Step 4.6 — Create Pending view**

Create `Areas/ExternalPortal/Views/PropertyClaim/Pending.cshtml`:
```html
@{
    ViewData["Title"] = "Claim Submitted";
    Layout = "~/Areas/ExternalPortal/Views/Shared/_PortalLayout.cshtml";
}
<div class="portal-card">
    <h1>Claim Submitted</h1>
    <p>Your property claim has been submitted and is awaiting review by a DWS official. You will receive an email when it is approved.</p>
    <div class="actions">
        <a href="@Url.Action("Index", "Dashboard", new { area = "ExternalPortal" })" class="btn-primary">Back to Dashboard</a>
    </div>
</div>
```

- [ ] **Step 4.7 — Create DWS admin PropertyClaimsController**

Create `Controllers/Admin/PropertyClaimsController.cs`:
```csharp
using dwa_ver_val.Models.Enums;
using dwa_ver_val.Services.Auth;
using dwa_ver_val.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Controllers.Admin;

[Authorize(Policy = DwsPolicies.CanManageUsers)]
public class PropertyClaimsController : Controller
{
    private readonly ApplicationDBContext _db;
    private readonly INotificationService _notify;

    public PropertyClaimsController(ApplicationDBContext db, INotificationService notify)
    {
        _db = db;
        _notify = notify;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var claims = await _db.PublicUserProperties
            .Include(p => p.PublicUser)
            .Include(p => p.Property)
            .Where(p => p.Status == PropertyClaimStatus.Pending)
            .OrderBy(p => p.RequestedDate)
            .ToListAsync(ct);
        return View(claims);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var claim = await _db.PublicUserProperties.FindAsync(new object[] { id }, ct)
            ?? throw new InvalidOperationException($"Claim {id} not found.");

        claim.Status = PropertyClaimStatus.Approved;
        claim.ApprovedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _notify.NotifyPublicUserAsync(claim.PublicUserId, null,
            "ClaimApproved",
            "Your property claim has been approved",
            "You can now log in to the V&V portal to view your case status.",
            actionUrl: null, ct);

        TempData["Success"] = "Claim approved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, string? reason, CancellationToken ct)
    {
        var claim = await _db.PublicUserProperties.FindAsync(new object[] { id }, ct)
            ?? throw new InvalidOperationException($"Claim {id} not found.");

        claim.Status = PropertyClaimStatus.Rejected;
        claim.RejectionReason = reason;
        await _db.SaveChangesAsync(ct);

        await _notify.NotifyPublicUserAsync(claim.PublicUserId, null,
            "ClaimRejected",
            "Your property claim could not be approved",
            reason ?? "Your claim has been reviewed and could not be approved at this time.",
            actionUrl: null, ct);

        TempData["Error"] = "Claim rejected.";
        return RedirectToAction(nameof(Index));
    }
}
```

- [ ] **Step 4.8 — Create admin Index view**

Create `Views/Admin/PropertyClaims/Index.cshtml`:
```html
@model IEnumerable<PublicUserProperty>
@{
    ViewData["Title"] = "Pending Property Claims";
    Layout = "~/Views/Shared/_Layout.cshtml";
}
<h2>Pending Property Claims</h2>
@if (TempData["Success"] is string ok)
{
    <div class="alert alert-success">@ok</div>
}
@if (TempData["Error"] is string err)
{
    <div class="alert alert-danger">@err</div>
}

@if (!Model.Any())
{
    <p class="text-muted">No pending claims.</p>
}
else
{
    <table class="table table-sm table-hover">
        <thead>
            <tr>
                <th>User</th><th>Email</th><th>Property</th><th>SGCode</th>
                <th>Requested</th><th>Actions</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var c in Model)
            {
                <tr>
                    <td>@c.PublicUser?.FirstName @c.PublicUser?.LastName</td>
                    <td>@c.PublicUser?.EmailAddress</td>
                    <td>@c.Property?.PropertyReferenceNumber</td>
                    <td>@c.Property?.SGCode</td>
                    <td>@c.RequestedDate.ToString("dd MMM yyyy")</td>
                    <td>
                        <form method="post" asp-action="Approve" asp-controller="PropertyClaims"
                              asp-route-id="@c.Id" style="display:inline">
                            @Html.AntiForgeryToken()
                            <button type="submit" class="btn btn-sm btn-success">Approve</button>
                        </form>
                        <form method="post" asp-action="Reject" asp-controller="PropertyClaims"
                              asp-route-id="@c.Id" style="display:inline">
                            @Html.AntiForgeryToken()
                            <input type="hidden" name="reason" value="Evidence not sufficient." />
                            <button type="submit" class="btn btn-sm btn-danger">Reject</button>
                        </form>
                    </td>
                </tr>
            }
        </tbody>
    </table>
}
```

- [ ] **Step 4.9 — Run tests**
```bash
cd Tests && dotnet test --filter "PropertyClaimControllerTests" -v minimal 2>&1 | tail -8
```
Expected: 3 tests pass.

- [ ] **Step 4.10 — Build**
```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 4.11 — Commit**
```bash
git add Areas/ExternalPortal/Controllers/PropertyClaimController.cs Areas/ExternalPortal/ViewModels/PropertyClaimViewModel.cs Areas/ExternalPortal/Views/PropertyClaim/ Controllers/Admin/PropertyClaimsController.cs Views/Admin/PropertyClaims/ Tests/Areas/ExternalPortal/PropertyClaimControllerTests.cs
git commit -m "feat(portal): property claim flow — water user submits claim; DWS admin approves/rejects with notification"
```

---

## Task 5 — External Portal: Case Dashboard

**Files:**
- **Create:** `Areas/ExternalPortal/ViewModels/CaseSummaryViewModel.cs`
- **Create:** `Areas/ExternalPortal/Controllers/CaseController.cs`
- **Create:** `Areas/ExternalPortal/Views/Case/Index.cshtml`
- **Create:** `Areas/ExternalPortal/Views/Case/Detail.cshtml`
- **Modify:** `Areas/ExternalPortal/Controllers/DashboardController.cs`
- **Modify:** `Areas/ExternalPortal/Views/Dashboard/Index.cshtml`
- **Modify:** `Areas/ExternalPortal/Views/Shared/_PortalLayout.cshtml` — add wide-card class + nav
- **Create:** `Tests/Areas/ExternalPortal/CaseControllerTests.cs`

- [ ] **Step 5.1 — Write failing tests**

Create `Tests/Areas/ExternalPortal/CaseControllerTests.cs`:
```csharp
using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.Controllers;
using dwa_ver_val.Models.Enums;
using dwa_ver_val.Services.Letters;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class CaseControllerTests
{
    private static (ApplicationDBContext db, CaseController controller) Build(Guid userId)
    {
        var db = TestDbContextFactory.Create();
        var accessor = new PublicUserPropertyAccessor(db);
        var letters = new Mock<ILetterService>();
        var blobs = new Mock<IBlobStore>();
        var controller = new CaseController(db, accessor, blobs.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                }, "test"))
            }
        };
        return (db, controller);
    }

    private static async Task<(Property prop, FileMaster fm)> SeedApprovedCase(
        ApplicationDBContext db, Guid userId)
    {
        var prop = new Property
        {
            PropertyId = Guid.NewGuid(),
            SGCode = "T0001",
            WmaId = null,
            PropertyReferenceNumber = "R001"
        };
        var fm = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = prop.PropertyId,
            FarmName = "Test Farm"
        };
        db.Properties.Add(prop);
        db.FileMasters.Add(fm);
        db.PublicUserProperties.Add(new PublicUserProperty
        {
            Id = Guid.NewGuid(),
            PublicUserId = userId,
            PropertyId = prop.PropertyId,
            Status = PropertyClaimStatus.Approved,
            EvidenceType = PropertyClaimEvidenceType.IdMatch,
            RequestedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return (prop, fm);
    }

    [Fact]
    public async Task Index_ReturnsOnlyApprovedCases()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);
        await SeedApprovedCase(db, userId);

        // Add a second property with pending claim — should not appear
        var prop2 = new Property
        {
            PropertyId = Guid.NewGuid(), SGCode = "T0002", WmaId = null, PropertyReferenceNumber = "R002"
        };
        db.Properties.Add(prop2);
        db.PublicUserProperties.Add(new PublicUserProperty
        {
            Id = Guid.NewGuid(),
            PublicUserId = userId,
            PropertyId = prop2.PropertyId,
            Status = PropertyClaimStatus.Pending,
            EvidenceType = PropertyClaimEvidenceType.IdMatch,
            RequestedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await controller.Index(default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<dwa_ver_val.Areas.ExternalPortal.ViewModels.CaseSummaryViewModel>>(view.Model);
        Assert.Single(model);
    }

    [Fact]
    public async Task Detail_ApprovedCase_ReturnsView()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);
        var (_, fm) = await SeedApprovedCase(db, userId);

        var result = await controller.Detail(fm.FileMasterId, default);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Detail_UnlinkedCase_Returns404()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);

        var result = await controller.Detail(Guid.NewGuid(), default);

        Assert.IsType<NotFoundResult>(result);
    }
}
```

- [ ] **Step 5.2 — Run to confirm failure**
```bash
cd Tests && dotnet test --filter "CaseControllerTests" -v minimal 2>&1 | tail -5
```
Expected: compilation error.

- [ ] **Step 5.3 — Create CaseSummaryViewModel**

Create `Areas/ExternalPortal/ViewModels/CaseSummaryViewModel.cs`:
```csharp
namespace dwa_ver_val.Areas.ExternalPortal.ViewModels;

public class CaseSummaryViewModel
{
    public Guid FileMasterId { get; init; }
    public string FarmName { get; init; } = "";
    public string PropertyReference { get; init; } = "";
    public string SGCode { get; init; } = "";
    public string? WorkflowState { get; init; }
    public int UnreadNotifications { get; init; }
    public int PendingLetters { get; init; }
}

public class CaseDetailViewModel
{
    public FileMaster FileMaster { get; init; } = null!;
    public IReadOnlyList<LetterIssuance> Letters { get; init; } = [];
    public IReadOnlyList<CaseComment> Comments { get; init; } = [];
    public IReadOnlyList<Document> Documents { get; init; } = [];
    public IReadOnlyList<Objection> Objections { get; init; } = [];
    public IReadOnlyList<Notification> Notifications { get; init; } = [];
}
```

- [ ] **Step 5.4 — Create CaseController**

Create `Areas/ExternalPortal/Controllers/CaseController.cs`:
```csharp
using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Models.Enums;
using dwa_ver_val.Services.Letters;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Areas.ExternalPortal.Controllers;

[Area("ExternalPortal")]
[Route("ExternalPortal/[controller]/[action]")]
[Authorize(Policy = PortalPolicies.PortalAuthenticated)]
public class CaseController : Controller
{
    private readonly ApplicationDBContext _db;
    private readonly IPublicUserPropertyAccessor _access;
    private readonly IBlobStore _blobs;

    public CaseController(ApplicationDBContext db, IPublicUserPropertyAccessor access, IBlobStore blobs)
    {
        _db = db;
        _access = access;
        _blobs = blobs;
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Not authenticated."));

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var uid = CurrentUserId();
        var approvedPropertyIds = await _access.GetApprovedPropertyIdsAsync(uid, ct);

        var cases = await _db.FileMasters
            .Include(f => f.Property)
            .Include(f => f.WorkflowInstance).ThenInclude(wi => wi!.CurrentWorkflowState)
            .Where(f => approvedPropertyIds.Contains(f.PropertyId))
            .ToListAsync(ct);

        var unread = await _db.Notifications
            .Where(n => n.PublicUserId == uid && !n.IsRead)
            .GroupBy(n => n.FileMasterId)
            .Select(g => new { FileMasterId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var unreadMap = unread.ToDictionary(u => u.FileMasterId ?? Guid.Empty, u => u.Count);

        var vms = cases.Select(f => new CaseSummaryViewModel
        {
            FileMasterId = f.FileMasterId,
            FarmName = f.FarmName ?? f.FileMasterId.ToString()[..8],
            PropertyReference = f.Property?.PropertyReferenceNumber ?? "",
            SGCode = f.Property?.SGCode ?? "",
            WorkflowState = f.WorkflowInstance?.CurrentWorkflowState?.StateName,
            UnreadNotifications = unreadMap.TryGetValue(f.FileMasterId, out var c) ? c : 0
        }).ToList();

        return View(vms);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var uid = CurrentUserId();
        try { await _access.AssertHasAccessToFileMasterAsync(uid, id, ct); }
        catch { return NotFound(); }

        var fm = await _db.FileMasters
            .Include(f => f.Property)
            .Include(f => f.WorkflowInstance).ThenInclude(wi => wi!.CurrentWorkflowState)
            .FirstOrDefaultAsync(f => f.FileMasterId == id, ct);
        if (fm is null) return NotFound();

        var letters = await _db.LetterIssuances
            .Include(l => l.LetterType)
            .Where(l => l.FileMasterId == id)
            .OrderBy(l => l.IssuedDate)
            .ToListAsync(ct);

        var comments = await _db.CaseComments
            .Where(c => c.FileMasterId == id && c.PublicUserId == uid)
            .OrderByDescending(c => c.SubmittedDate)
            .ToListAsync(ct);

        var docs = await _db.Documents
            .Where(d => d.FileMasterId == id && d.UploadedByPublicUserId == uid)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync(ct);

        var objections = await _db.Objections
            .Where(o => o.FileMasterId == id && o.PublicUserId == uid)
            .OrderByDescending(o => o.LodgedDate)
            .ToListAsync(ct);

        // Mark unread notifications for this case as read
        var unreadNotes = await _db.Notifications
            .Where(n => n.PublicUserId == uid && n.FileMasterId == id && !n.IsRead)
            .ToListAsync(ct);
        foreach (var n in unreadNotes) { n.IsRead = true; n.ReadDate = DateTime.UtcNow; }
        if (unreadNotes.Count > 0) await _db.SaveChangesAsync(ct);

        return View(new CaseDetailViewModel
        {
            FileMaster = fm,
            Letters = letters,
            Comments = comments,
            Documents = docs,
            Objections = objections
        });
    }

    [HttpGet("{fileMasterId:guid}/{issuanceId:guid}")]
    public async Task<IActionResult> DownloadLetter(Guid fileMasterId, Guid issuanceId, CancellationToken ct)
    {
        var uid = CurrentUserId();
        try { await _access.AssertHasAccessToFileMasterAsync(uid, fileMasterId, ct); }
        catch { return Forbid(); }

        var issuance = await _db.LetterIssuances
            .FirstOrDefaultAsync(l => l.LetterIssuanceId == issuanceId && l.FileMasterId == fileMasterId, ct);
        if (issuance is null || string.IsNullOrEmpty(issuance.BlobPath)) return NotFound();

        var bytes = await _blobs.ReadAsync(issuance.BlobPath);
        return File(bytes, "application/pdf",
            $"letter-{issuance.IssuedDate:yyyyMMdd}-{issuanceId.ToString()[..8]}.pdf");
    }
}
```

- [ ] **Step 5.5 — Create Case/Index view**

Create `Areas/ExternalPortal/Views/Case/Index.cshtml`:
```html
@model IEnumerable<dwa_ver_val.Areas.ExternalPortal.ViewModels.CaseSummaryViewModel>
@{
    ViewData["Title"] = "My Cases";
    Layout = "~/Areas/ExternalPortal/Views/Shared/_PortalLayout.cshtml";
}
<div class="portal-card portal-card--wide">
    <h1>My Cases</h1>
    @if (!Model.Any())
    {
        <p class="muted">No approved cases yet. <a href="@Url.Action("Submit","PropertyClaim",new{area="ExternalPortal"})">Claim a property</a> to get started.</p>
    }
    else
    {
        <table class="portal-table">
            <thead>
                <tr><th>Farm Name</th><th>Property Ref</th><th>SGCode</th><th>Workflow State</th><th>Unread</th><th></th></tr>
            </thead>
            <tbody>
                @foreach (var c in Model)
                {
                    <tr>
                        <td>@c.FarmName</td>
                        <td>@c.PropertyReference</td>
                        <td>@c.SGCode</td>
                        <td>@(c.WorkflowState ?? "Not started")</td>
                        <td>@(c.UnreadNotifications > 0 ? c.UnreadNotifications.ToString() : "")</td>
                        <td><a href="@Url.Action("Detail","Case",new{area="ExternalPortal",id=c.FileMasterId})" class="btn-primary" style="font-size:var(--dws-fs-sm);padding:4px 10px">View</a></td>
                    </tr>
                }
            </tbody>
        </table>
    }
    <div style="margin-top:var(--dws-space-4)">
        <a href="@Url.Action("Submit","PropertyClaim",new{area="ExternalPortal"})">+ Claim another property</a>
    </div>
</div>
```

- [ ] **Step 5.6 — Create Case/Detail view**

Create `Areas/ExternalPortal/Views/Case/Detail.cshtml`:
```html
@model dwa_ver_val.Areas.ExternalPortal.ViewModels.CaseDetailViewModel
@{
    ViewData["Title"] = Model.FileMaster.FarmName ?? "Case Detail";
    Layout = "~/Areas/ExternalPortal/Views/Shared/_PortalLayout.cshtml";
    var fmId = Model.FileMaster.FileMasterId;
}
<div class="portal-card portal-card--wide">
    <h1>@(Model.FileMaster.FarmName ?? "Case")</h1>
    <p class="muted">SGCode: @(Model.FileMaster.Property?.SGCode ?? "—") &nbsp;|&nbsp; Status: <strong>@(Model.FileMaster.WorkflowInstance?.CurrentWorkflowState?.StateName ?? "Not started")</strong></p>

    <h2 style="margin-top:var(--dws-space-5);font-size:16px">Letters Issued</h2>
    @if (!Model.Letters.Any())
    {
        <p class="muted">No letters issued yet.</p>
    }
    else
    {
        <ul style="list-style:none;padding:0">
            @foreach (var l in Model.Letters)
            {
                <li style="padding:6px 0;border-bottom:1px solid var(--dws-border)">
                    <strong>@l.LetterType?.LetterName</strong>
                    &nbsp; issued @l.IssuedDate.ToString("dd MMM yyyy")
                    &nbsp;
                    <a href="@Url.Action("DownloadLetter","Case",new{area="ExternalPortal",fileMasterId=fmId,issuanceId=l.LetterIssuanceId})">Download PDF</a>
                    &nbsp;
                    <a href="@Url.Action("Submit","Response",new{area="ExternalPortal",fileMasterId=fmId,issuanceId=l.LetterIssuanceId})">Respond</a>
                </li>
            }
        </ul>
    }

    <h2 style="margin-top:var(--dws-space-5);font-size:16px">Supporting Documents</h2>
    <a href="@Url.Action("Upload","Document",new{area="ExternalPortal",fileMasterId=fmId})" class="btn-primary" style="font-size:var(--dws-fs-sm);padding:5px 12px">Upload Document</a>
    @if (Model.Documents.Any())
    {
        <ul style="list-style:none;padding:0;margin-top:var(--dws-space-3)">
            @foreach (var d in Model.Documents)
            {
                <li style="padding:4px 0">@d.FileName &nbsp; <span class="muted" style="font-size:var(--dws-fs-xs)">@d.UploadDate.ToString("dd MMM yyyy") — @d.DocumentType</span></li>
            }
        </ul>
    }

    <h2 style="margin-top:var(--dws-space-5);font-size:16px">Responses &amp; Comments</h2>
    @if (Model.Comments.Any())
    {
        @foreach (var c in Model.Comments)
        {
            <div style="background:var(--dws-surface-alt,#f8f9fa);padding:8px 12px;border-radius:4px;margin-bottom:6px">
                <p style="margin:0">@c.CommentText</p>
                <span class="muted" style="font-size:var(--dws-fs-xs)">Submitted @c.SubmittedDate.ToString("dd MMM yyyy HH:mm")</span>
            </div>
        }
    }

    <h2 style="margin-top:var(--dws-space-5);font-size:16px">Objections / Appeals</h2>
    @if (!Model.Objections.Any())
    {
        <p class="muted">No objections lodged.</p>
        <a href="@Url.Action("Lodge","Objection",new{area="ExternalPortal",fileMasterId=fmId})">Lodge an Objection / Appeal</a>
    }
    else
    {
        @foreach (var o in Model.Objections)
        {
            <div style="padding:6px 0;border-bottom:1px solid var(--dws-border)">
                Lodged @o.LodgedDate.ToString("dd MMM yyyy") — Status: <strong>@o.Status</strong>
                @if (!string.IsNullOrEmpty(o.ResolutionNotes))
                {
                    <p style="margin:4px 0;font-size:var(--dws-fs-sm)">@o.ResolutionNotes</p>
                }
            </div>
        }
    }

    <div style="margin-top:var(--dws-space-5)">
        <a href="@Url.Action("Index","Case",new{area="ExternalPortal"})">← Back to My Cases</a>
    </div>
</div>
```

- [ ] **Step 5.7 — Add wide-card CSS + nav to _PortalLayout.cshtml**

In `Areas/ExternalPortal/Views/Shared/_PortalLayout.cshtml`, find the `<style>` block and add after the `.portal-card` rule:
```css
        .portal-card--wide {
            max-width: 860px;
        }
        .portal-table {
            width: 100%;
            border-collapse: collapse;
            font-size: var(--dws-fs-sm);
        }
        .portal-table th, .portal-table td {
            padding: 6px 10px;
            border-bottom: 1px solid var(--dws-border);
            text-align: left;
        }
        .portal-nav {
            width: 100%;
            max-width: 860px;
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: var(--dws-space-3);
            font-size: var(--dws-fs-sm);
        }
        .portal-nav a { color: var(--dws-blue-100); text-decoration: none; margin-left: var(--dws-space-3); }
        .portal-nav a:hover { text-decoration: underline; }
```

In the same file, find `@RenderBody()` and add a nav bar before it:
```html
        @if (User.Identity?.IsAuthenticated == true)
        {
            <div class="portal-nav">
                <span style="color:var(--dws-blue-100);font-size:var(--dws-fs-xs)">@User.Identity.Name</span>
                <div>
                    <a href="@Url.Action("Index","Dashboard",new{area="ExternalPortal"})">Dashboard</a>
                    <a href="@Url.Action("Index","Case",new{area="ExternalPortal"})">My Cases</a>
                    <a href="@Url.Action("Submit","PropertyClaim",new{area="ExternalPortal"})">Claim Property</a>
                    <form asp-action="Logout" asp-controller="Account" asp-area="ExternalPortal" method="post" style="display:inline">
                        @Html.AntiForgeryToken()
                        <button type="submit" style="background:none;border:none;color:var(--dws-blue-100);cursor:pointer;font-size:var(--dws-fs-sm)">Logout</button>
                    </form>
                </div>
            </div>
        }
        @RenderBody()
```

- [ ] **Step 5.8 — Update Dashboard/Index.cshtml**

Replace the content of `Areas/ExternalPortal/Views/Dashboard/Index.cshtml` with:
```html
@{
    ViewData["Title"] = "Dashboard";
    Layout = "~/Areas/ExternalPortal/Views/Shared/_PortalLayout.cshtml";
}
<div class="portal-card portal-card--wide">
    <h1>Welcome</h1>
    <p class="muted">You are logged in to the DWA V&amp;V Public Portal.</p>

    <div style="display:flex;gap:var(--dws-space-4);margin-top:var(--dws-space-4);flex-wrap:wrap">
        <a href="@Url.Action("Index","Case",new{area="ExternalPortal"})" class="btn-primary">View My Cases</a>
        <a href="@Url.Action("Submit","PropertyClaim",new{area="ExternalPortal"})" class="btn-primary" style="background:var(--dws-teal-600)">Claim a Property</a>
    </div>
</div>
```

- [ ] **Step 5.9 — Run tests**
```bash
cd Tests && dotnet test --filter "CaseControllerTests" -v minimal 2>&1 | tail -8
```
Expected: 3 tests pass.

- [ ] **Step 5.10 — Build**
```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 5.11 — Commit**
```bash
git add Areas/ExternalPortal/Controllers/CaseController.cs Areas/ExternalPortal/ViewModels/CaseSummaryViewModel.cs Areas/ExternalPortal/Views/Case/ Areas/ExternalPortal/Views/Dashboard/Index.cshtml Areas/ExternalPortal/Views/Shared/_PortalLayout.cshtml Tests/Areas/ExternalPortal/CaseControllerTests.cs
git commit -m "feat(portal): case dashboard — case list, per-case detail, letter download, unread notification tracking"
```

---

## Task 6 — External Portal: Document Upload

**Files:**
- **Create:** `Areas/ExternalPortal/ViewModels/DocumentUploadViewModel.cs`
- **Create:** `Areas/ExternalPortal/Controllers/DocumentController.cs`
- **Create:** `Areas/ExternalPortal/Views/Document/Upload.cshtml`
- **Create:** `Tests/Areas/ExternalPortal/DocumentControllerTests.cs`

- [ ] **Step 6.1 — Write failing tests**

Create `Tests/Areas/ExternalPortal/DocumentControllerTests.cs`:
```csharp
using System.Security.Claims;
using System.Text;
using dwa_ver_val.Areas.ExternalPortal.Controllers;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Models.Enums;
using dwa_ver_val.Services.Infrastructure.Storage;
using dwa_ver_val.Services.Notifications;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class DocumentControllerTests
{
    private static (ApplicationDBContext db, DocumentController controller) Build(Guid userId)
    {
        var db = TestDbContextFactory.Create();
        var accessor = new PublicUserPropertyAccessor(db);
        var storage = new Mock<IFileStorage>();
        storage.Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredFileResult
            {
                RelativePath = "2026/05/test.pdf",
                ContentType = "application/pdf",
                SizeBytes = 100,
                Sha256Hex = "abc"
            });
        var notify = new Mock<INotificationService>();
        var controller = new DocumentController(db, accessor, storage.Object, notify.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                }, "test"))
            }
        };
        return (db, controller);
    }

    [Fact]
    public async Task Upload_Post_ValidFile_CreatesDocumentRecord()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);

        var prop = new Property { PropertyId = Guid.NewGuid(), SGCode = "T0001", WmaId = null, PropertyReferenceNumber = "R1" };
        var fm = new FileMaster { FileMasterId = Guid.NewGuid(), PropertyId = prop.PropertyId, FarmName = "Farm" };
        db.Properties.Add(prop);
        db.FileMasters.Add(fm);
        db.PublicUserProperties.Add(new PublicUserProperty
        {
            Id = Guid.NewGuid(), PublicUserId = userId, PropertyId = prop.PropertyId,
            Status = PropertyClaimStatus.Approved,
            EvidenceType = PropertyClaimEvidenceType.IdMatch, RequestedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var fileContent = Encoding.UTF8.GetBytes("fake pdf");
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("titledeed.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.Length).Returns(fileContent.Length);
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(fileContent));

        var result = await controller.Upload(
            new DocumentUploadViewModel
            {
                FileMasterId = fm.FileMasterId,
                DocumentType = "TitleDeed",
                File = file.Object
            }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", redirect.ActionName);

        var doc = db.Documents.Single();
        Assert.Equal(fm.FileMasterId, doc.FileMasterId);
        Assert.Equal("TitleDeed", doc.DocumentType);
        Assert.Equal(userId, doc.UploadedByPublicUserId);
    }

    [Fact]
    public async Task Upload_Post_UnlinkedCase_ReturnsForbid()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("x.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.Length).Returns(10);
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[10]));

        var result = await controller.Upload(
            new DocumentUploadViewModel
            {
                FileMasterId = Guid.NewGuid(),
                DocumentType = "TitleDeed",
                File = file.Object
            }, default);

        Assert.IsType<ForbidResult>(result);
    }
}
```

- [ ] **Step 6.2 — Run to confirm failure**
```bash
cd Tests && dotnet test --filter "DocumentControllerTests" -v minimal 2>&1 | tail -5
```
Expected: compilation error.

- [ ] **Step 6.3 — Create DocumentUploadViewModel**

Create `Areas/ExternalPortal/ViewModels/DocumentUploadViewModel.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace dwa_ver_val.Areas.ExternalPortal.ViewModels;

public class DocumentUploadViewModel
{
    public Guid FileMasterId { get; set; }

    [Required(ErrorMessage = "Please select a document type.")]
    [Display(Name = "Document Type")]
    public string DocumentType { get; set; } = "TitleDeed";

    [Required(ErrorMessage = "Please select a file.")]
    public IFormFile? File { get; set; }
}
```

- [ ] **Step 6.4 — Create DocumentController**

Create `Areas/ExternalPortal/Controllers/DocumentController.cs`:
```csharp
using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Services.Infrastructure.Storage;
using dwa_ver_val.Services.Notifications;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dwa_ver_val.Areas.ExternalPortal.Controllers;

[Area("ExternalPortal")]
[Route("ExternalPortal/[controller]/[action]")]
[Authorize(Policy = PortalPolicies.PortalAuthenticated)]
public class DocumentController : Controller
{
    private readonly ApplicationDBContext _db;
    private readonly IPublicUserPropertyAccessor _access;
    private readonly IFileStorage _storage;
    private readonly INotificationService _notify;

    public DocumentController(ApplicationDBContext db, IPublicUserPropertyAccessor access,
        IFileStorage storage, INotificationService notify)
    {
        _db = db;
        _access = access;
        _storage = storage;
        _notify = notify;
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public IActionResult Upload(Guid fileMasterId) =>
        View(new DocumentUploadViewModel { FileMasterId = fileMasterId });

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> Upload(DocumentUploadViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(vm);

        var uid = CurrentUserId();
        try { await _access.AssertHasAccessToFileMasterAsync(uid, vm.FileMasterId, ct); }
        catch { return Forbid(); }

        await using var stream = vm.File!.OpenReadStream();
        var stored = await _storage.SaveAsync(stream, vm.File.ContentType, vm.File.FileName, ct);

        _db.Documents.Add(new Document
        {
            DocumentId = Guid.NewGuid(),
            FileMasterId = vm.FileMasterId,
            DocumentType = vm.DocumentType,
            FileName = vm.File.FileName,
            BlobPath = stored.RelativePath,
            ContentType = vm.File.ContentType,
            FileSizeBytes = stored.SizeBytes,
            UploadedByPublicUserId = uid,
            UploadDate = DateTime.UtcNow,
            VirusScanStatus = "Pending",
            DocumentHash = stored.Sha256Hex
        });
        await _db.SaveChangesAsync(ct);

        await _notify.NotifyDwsValidatorAsync(vm.FileMasterId, "Upload",
            "New document uploaded on case",
            $"A water user uploaded '{vm.File.FileName}' ({vm.DocumentType}).", ct);

        TempData["Success"] = "Document uploaded successfully.";
        return RedirectToAction("Detail", "Case", new { area = "ExternalPortal", id = vm.FileMasterId });
    }
}
```

- [ ] **Step 6.5 — Create Upload view**

Create `Areas/ExternalPortal/Views/Document/Upload.cshtml`:
```html
@model dwa_ver_val.Areas.ExternalPortal.ViewModels.DocumentUploadViewModel
@{
    ViewData["Title"] = "Upload Document";
    Layout = "~/Areas/ExternalPortal/Views/Shared/_PortalLayout.cshtml";
}
<div class="portal-card">
    <h1>Upload Supporting Document</h1>
    <p class="muted">Upload a title deed, permit, or other supporting evidence. Max 10 MB. PDF or image files only.</p>

    <form asp-action="Upload" asp-controller="Document" asp-area="ExternalPortal"
          method="post" enctype="multipart/form-data">
        @Html.AntiForgeryToken()
        <input type="hidden" asp-for="FileMasterId" />
        <div asp-validation-summary="All" class="errors"
             style="display:@(ViewData.ModelState.IsValid ? "none" : "block")"></div>

        <label asp-for="DocumentType">Document Type</label>
        <select asp-for="DocumentType" style="width:100%;padding:7px 10px;border:1px solid var(--dws-border);border-radius:4px;font-size:var(--dws-fs-sm);margin-top:4px">
            <option value="TitleDeed">Title Deed</option>
            <option value="Permit">Permit</option>
            <option value="SurveyDiagram">SG Diagram</option>
            <option value="FieldSurvey">Field Survey</option>
            <option value="Other">Other</option>
        </select>

        <label asp-for="File" style="margin-top:var(--dws-space-3)">File</label>
        <input asp-for="File" type="file" accept=".pdf,.jpg,.jpeg,.png"
               style="margin-top:4px;width:100%;font-size:var(--dws-fs-sm)" />
        <span asp-validation-for="File" class="errors" style="padding:4px 0;border:none;background:none;font-size:var(--dws-fs-xs)"></span>

        <div class="actions" style="margin-top:var(--dws-space-4)">
            <button type="submit" class="btn-primary">Upload</button>
            <a href="@Url.Action("Detail","Case",new{area="ExternalPortal",id=Model.FileMasterId})">Cancel</a>
        </div>
    </form>
</div>
```

- [ ] **Step 6.6 — Run tests**
```bash
cd Tests && dotnet test --filter "DocumentControllerTests" -v minimal 2>&1 | tail -8
```
Expected: 2 tests pass.

- [ ] **Step 6.7 — Commit**
```bash
git add Areas/ExternalPortal/Controllers/DocumentController.cs Areas/ExternalPortal/ViewModels/DocumentUploadViewModel.cs Areas/ExternalPortal/Views/Document/ Tests/Areas/ExternalPortal/DocumentControllerTests.cs
git commit -m "feat(portal): document upload — water user attaches supporting evidence; notifies DWS validator"
```

---

## Task 7 — External Portal: Letter Response (CaseComment)

**Files:**
- **Create:** `Areas/ExternalPortal/ViewModels/LetterResponseViewModel.cs`
- **Create:** `Areas/ExternalPortal/Controllers/ResponseController.cs`
- **Create:** `Areas/ExternalPortal/Views/Response/Submit.cshtml`
- **Create:** `Tests/Areas/ExternalPortal/ResponseControllerTests.cs`

- [ ] **Step 7.1 — Write failing tests**

Create `Tests/Areas/ExternalPortal/ResponseControllerTests.cs`:
```csharp
using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.Controllers;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Models.Enums;
using dwa_ver_val.Services.Notifications;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class ResponseControllerTests
{
    private static (ApplicationDBContext db, ResponseController controller) Build(Guid userId)
    {
        var db = TestDbContextFactory.Create();
        var accessor = new PublicUserPropertyAccessor(db);
        var notify = new Mock<INotificationService>();
        var controller = new ResponseController(db, accessor, notify.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                }, "test"))
            }
        };
        return (db, controller);
    }

    private static async Task<FileMaster> SeedApprovedCase(ApplicationDBContext db, Guid userId)
    {
        var prop = new Property { PropertyId = Guid.NewGuid(), SGCode = "T001", WmaId = null, PropertyReferenceNumber = "R1" };
        var fm = new FileMaster { FileMasterId = Guid.NewGuid(), PropertyId = prop.PropertyId, FarmName = "F" };
        db.Properties.Add(prop);
        db.FileMasters.Add(fm);
        db.PublicUserProperties.Add(new PublicUserProperty
        {
            Id = Guid.NewGuid(), PublicUserId = userId, PropertyId = prop.PropertyId,
            Status = PropertyClaimStatus.Approved,
            EvidenceType = PropertyClaimEvidenceType.IdMatch, RequestedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return fm;
    }

    [Fact]
    public async Task Submit_Post_ValidResponse_CreatesCaseComment()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);
        var fm = await SeedApprovedCase(db, userId);

        var result = await controller.Submit(
            new LetterResponseViewModel
            {
                FileMasterId = fm.FileMasterId,
                ResponseText = "I agree with the findings."
            }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", redirect.ActionName);

        var comment = db.CaseComments.Single();
        Assert.Equal(fm.FileMasterId, comment.FileMasterId);
        Assert.Equal(userId, comment.PublicUserId);
        Assert.Equal("PublicUser", comment.AuthorType);
        Assert.Equal("I agree with the findings.", comment.CommentText);
    }

    [Fact]
    public async Task Submit_Post_UnlinkedCase_ReturnsForbid()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);

        var result = await controller.Submit(
            new LetterResponseViewModel
            {
                FileMasterId = Guid.NewGuid(),
                ResponseText = "Anything"
            }, default);

        Assert.IsType<ForbidResult>(result);
    }
}
```

- [ ] **Step 7.2 — Run to confirm failure**
```bash
cd Tests && dotnet test --filter "ResponseControllerTests" -v minimal 2>&1 | tail -5
```
Expected: compilation error.

- [ ] **Step 7.3 — Create LetterResponseViewModel**

Create `Areas/ExternalPortal/ViewModels/LetterResponseViewModel.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.Areas.ExternalPortal.ViewModels;

public class LetterResponseViewModel
{
    public Guid FileMasterId { get; set; }
    public Guid? LetterIssuanceId { get; set; }

    [Required(ErrorMessage = "Response text is required.")]
    [MinLength(10, ErrorMessage = "Please provide at least 10 characters.")]
    [MaxLength(4000)]
    [Display(Name = "Your Response")]
    public string ResponseText { get; set; } = "";
}
```

- [ ] **Step 7.4 — Create ResponseController**

Create `Areas/ExternalPortal/Controllers/ResponseController.cs`:
```csharp
using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Services.Notifications;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dwa_ver_val.Areas.ExternalPortal.Controllers;

[Area("ExternalPortal")]
[Route("ExternalPortal/[controller]/[action]")]
[Authorize(Policy = PortalPolicies.PortalAuthenticated)]
public class ResponseController : Controller
{
    private readonly ApplicationDBContext _db;
    private readonly IPublicUserPropertyAccessor _access;
    private readonly INotificationService _notify;

    public ResponseController(ApplicationDBContext db, IPublicUserPropertyAccessor access,
        INotificationService notify)
    {
        _db = db;
        _access = access;
        _notify = notify;
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public IActionResult Submit(Guid fileMasterId, Guid? issuanceId) =>
        View(new LetterResponseViewModel { FileMasterId = fileMasterId, LetterIssuanceId = issuanceId });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(LetterResponseViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(vm);

        var uid = CurrentUserId();
        try { await _access.AssertHasAccessToFileMasterAsync(uid, vm.FileMasterId, ct); }
        catch { return Forbid(); }

        _db.CaseComments.Add(new CaseComment
        {
            CommentId = Guid.NewGuid(),
            FileMasterId = vm.FileMasterId,
            PublicUserId = uid,
            AuthorType = "PublicUser",
            CommentText = vm.ResponseText,
            SubmittedDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        await _notify.NotifyDwsValidatorAsync(vm.FileMasterId, "Comment",
            "Water user submitted a response on case",
            $"A response has been submitted: \"{vm.ResponseText[..Math.Min(200, vm.ResponseText.Length)]}...\"",
            ct);

        TempData["Success"] = "Response submitted.";
        return RedirectToAction("Detail", "Case", new { area = "ExternalPortal", id = vm.FileMasterId });
    }
}
```

- [ ] **Step 7.5 — Create Submit view**

Create `Areas/ExternalPortal/Views/Response/Submit.cshtml`:
```html
@model dwa_ver_val.Areas.ExternalPortal.ViewModels.LetterResponseViewModel
@{
    ViewData["Title"] = "Submit Response";
    Layout = "~/Areas/ExternalPortal/Views/Shared/_PortalLayout.cshtml";
}
<div class="portal-card">
    <h1>Submit a Response</h1>
    <p class="muted">Your written response will be reviewed by the DWS Validator assigned to your case.</p>

    <form asp-action="Submit" asp-controller="Response" asp-area="ExternalPortal" method="post">
        @Html.AntiForgeryToken()
        <input type="hidden" asp-for="FileMasterId" />
        <input type="hidden" asp-for="LetterIssuanceId" />
        <div asp-validation-summary="All" class="errors"
             style="display:@(ViewData.ModelState.IsValid ? "none" : "block")"></div>

        <label asp-for="ResponseText">Your Response</label>
        <textarea asp-for="ResponseText" rows="8"
                  style="width:100%;box-sizing:border-box;padding:8px 10px;border:1px solid var(--dws-border);border-radius:4px;font-size:var(--dws-fs-sm);margin-top:4px;resize:vertical"
                  placeholder="Enter your response here..."></textarea>
        <span asp-validation-for="ResponseText"
              class="errors" style="padding:4px 0;border:none;background:none;font-size:var(--dws-fs-xs)"></span>

        <div class="actions" style="margin-top:var(--dws-space-4)">
            <button type="submit" class="btn-primary">Submit Response</button>
            <a href="@Url.Action("Detail","Case",new{area="ExternalPortal",id=Model.FileMasterId})">Cancel</a>
        </div>
    </form>
</div>
```

- [ ] **Step 7.6 — Run tests**
```bash
cd Tests && dotnet test --filter "ResponseControllerTests" -v minimal 2>&1 | tail -8
```
Expected: 2 tests pass.

- [ ] **Step 7.7 — Commit**
```bash
git add Areas/ExternalPortal/Controllers/ResponseController.cs Areas/ExternalPortal/ViewModels/LetterResponseViewModel.cs Areas/ExternalPortal/Views/Response/ Tests/Areas/ExternalPortal/ResponseControllerTests.cs
git commit -m "feat(portal): letter response — water user submits CaseComment; notifies DWS validator"
```

---

## Task 8 — External Portal: Objection / Appeal

**Files:**
- **Create:** `Areas/ExternalPortal/ViewModels/ObjectionViewModel.cs`
- **Create:** `Areas/ExternalPortal/Controllers/ObjectionController.cs`
- **Create:** `Areas/ExternalPortal/Views/Objection/Lodge.cshtml`
- **Create:** `Tests/Areas/ExternalPortal/ObjectionControllerTests.cs`

- [ ] **Step 8.1 — Write failing tests**

Create `Tests/Areas/ExternalPortal/ObjectionControllerTests.cs`:
```csharp
using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.Controllers;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Models.Enums;
using dwa_ver_val.Services.Notifications;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class ObjectionControllerTests
{
    private static (ApplicationDBContext db, ObjectionController controller) Build(Guid userId)
    {
        var db = TestDbContextFactory.Create();
        var accessor = new PublicUserPropertyAccessor(db);
        var notify = new Mock<INotificationService>();
        var controller = new ObjectionController(db, accessor, notify.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                }, "test"))
            }
        };
        return (db, controller);
    }

    private static async Task<FileMaster> SeedApprovedCase(ApplicationDBContext db, Guid userId)
    {
        var prop = new Property { PropertyId = Guid.NewGuid(), SGCode = "T001", WmaId = null, PropertyReferenceNumber = "R1" };
        var fm = new FileMaster { FileMasterId = Guid.NewGuid(), PropertyId = prop.PropertyId, FarmName = "F" };
        db.Properties.Add(prop);
        db.FileMasters.Add(fm);
        db.PublicUserProperties.Add(new PublicUserProperty
        {
            Id = Guid.NewGuid(), PublicUserId = userId, PropertyId = prop.PropertyId,
            Status = PropertyClaimStatus.Approved,
            EvidenceType = PropertyClaimEvidenceType.IdMatch, RequestedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return fm;
    }

    [Fact]
    public async Task Lodge_Post_ValidGrounds_CreatesObjection()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);
        var fm = await SeedApprovedCase(db, userId);

        var result = await controller.Lodge(
            new ObjectionViewModel
            {
                FileMasterId = fm.FileMasterId,
                Grounds = "The ELU determination underestimates our historical use."
            }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", redirect.ActionName);

        var objection = db.Objections.Single();
        Assert.Equal(fm.FileMasterId, objection.FileMasterId);
        Assert.Equal(userId, objection.PublicUserId);
        Assert.Equal("Lodged", objection.Status);
    }

    [Fact]
    public async Task Lodge_Post_DuplicateObjection_ReturnsViewWithError()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);
        var fm = await SeedApprovedCase(db, userId);
        db.Objections.Add(new Objection
        {
            ObjectionId = Guid.NewGuid(), FileMasterId = fm.FileMasterId,
            PublicUserId = userId, Status = "Lodged", LodgedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await controller.Lodge(
            new ObjectionViewModel
            {
                FileMasterId = fm.FileMasterId,
                Grounds = "Second objection attempt."
            }, default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }
}
```

- [ ] **Step 8.2 — Run to confirm failure**
```bash
cd Tests && dotnet test --filter "ObjectionControllerTests" -v minimal 2>&1 | tail -5
```
Expected: compilation error.

- [ ] **Step 8.3 — Create ObjectionViewModel**

Create `Areas/ExternalPortal/ViewModels/ObjectionViewModel.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.Areas.ExternalPortal.ViewModels;

public class ObjectionViewModel
{
    public Guid FileMasterId { get; set; }

    [Required(ErrorMessage = "Grounds for objection are required.")]
    [MinLength(20, ErrorMessage = "Please provide at least 20 characters explaining your grounds.")]
    [MaxLength(4000)]
    [Display(Name = "Grounds for Objection / Appeal")]
    public string Grounds { get; set; } = "";
}
```

- [ ] **Step 8.4 — Create ObjectionController**

Create `Areas/ExternalPortal/Controllers/ObjectionController.cs`:
```csharp
using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Services.Notifications;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Areas.ExternalPortal.Controllers;

[Area("ExternalPortal")]
[Route("ExternalPortal/[controller]/[action]")]
[Authorize(Policy = PortalPolicies.PortalAuthenticated)]
public class ObjectionController : Controller
{
    private readonly ApplicationDBContext _db;
    private readonly IPublicUserPropertyAccessor _access;
    private readonly INotificationService _notify;

    public ObjectionController(ApplicationDBContext db, IPublicUserPropertyAccessor access,
        INotificationService notify)
    {
        _db = db;
        _access = access;
        _notify = notify;
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public IActionResult Lodge(Guid fileMasterId) =>
        View(new ObjectionViewModel { FileMasterId = fileMasterId });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Lodge(ObjectionViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(vm);

        var uid = CurrentUserId();
        try { await _access.AssertHasAccessToFileMasterAsync(uid, vm.FileMasterId, ct); }
        catch { return Forbid(); }

        var alreadyLodged = await _db.Objections.AnyAsync(o =>
            o.FileMasterId == vm.FileMasterId &&
            o.PublicUserId == uid &&
            o.Status == "Lodged", ct);

        if (alreadyLodged)
        {
            ModelState.AddModelError(nameof(vm.Grounds),
                "You already have an open objection on this case. Wait for it to be resolved before lodging another.");
            return View(vm);
        }

        _db.Objections.Add(new Objection
        {
            ObjectionId = Guid.NewGuid(),
            FileMasterId = vm.FileMasterId,
            PublicUserId = uid,
            LodgedDate = DateTime.UtcNow,
            Status = "Lodged"
        });
        await _db.SaveChangesAsync(ct);

        await _notify.NotifyDwsValidatorAsync(vm.FileMasterId, "Objection",
            "An objection / appeal has been lodged",
            $"A water user lodged an objection: \"{vm.Grounds[..Math.Min(200, vm.Grounds.Length)]}...\"",
            ct);

        TempData["Success"] = "Your objection has been lodged and is under review.";
        return RedirectToAction("Detail", "Case", new { area = "ExternalPortal", id = vm.FileMasterId });
    }
}
```

- [ ] **Step 8.5 — Create Lodge view**

Create `Areas/ExternalPortal/Views/Objection/Lodge.cshtml`:
```html
@model dwa_ver_val.Areas.ExternalPortal.ViewModels.ObjectionViewModel
@{
    ViewData["Title"] = "Lodge Objection / Appeal";
    Layout = "~/Areas/ExternalPortal/Views/Shared/_PortalLayout.cshtml";
}
<div class="portal-card">
    <h1>Lodge an Objection / Appeal</h1>
    <p class="muted">You may lodge a formal objection to the ELU determination on your case. State your grounds clearly. A DWS official will review and respond.</p>

    <form asp-action="Lodge" asp-controller="Objection" asp-area="ExternalPortal" method="post">
        @Html.AntiForgeryToken()
        <input type="hidden" asp-for="FileMasterId" />
        <div asp-validation-summary="All" class="errors"
             style="display:@(ViewData.ModelState.IsValid ? "none" : "block")"></div>

        <label asp-for="Grounds">Grounds for Objection / Appeal</label>
        <textarea asp-for="Grounds" rows="10"
                  style="width:100%;box-sizing:border-box;padding:8px 10px;border:1px solid var(--dws-border);border-radius:4px;font-size:var(--dws-fs-sm);margin-top:4px;resize:vertical"
                  placeholder="Describe your grounds for objection in detail..."></textarea>
        <span asp-validation-for="Grounds"
              class="errors" style="padding:4px 0;border:none;background:none;font-size:var(--dws-fs-xs)"></span>

        <div class="actions" style="margin-top:var(--dws-space-4)">
            <button type="submit" class="btn-primary">Submit Objection</button>
            <a href="@Url.Action("Detail","Case",new{area="ExternalPortal",id=Model.FileMasterId})">Cancel</a>
        </div>
    </form>
</div>
```

- [ ] **Step 8.6 — Run tests**
```bash
cd Tests && dotnet test --filter "ObjectionControllerTests" -v minimal 2>&1 | tail -8
```
Expected: 2 tests pass.

- [ ] **Step 8.7 — Commit**
```bash
git add Areas/ExternalPortal/Controllers/ObjectionController.cs Areas/ExternalPortal/ViewModels/ObjectionViewModel.cs Areas/ExternalPortal/Views/Objection/ Tests/Areas/ExternalPortal/ObjectionControllerTests.cs
git commit -m "feat(portal): objection/appeal — water user lodges formal objection; blocks duplicate open objections; notifies DWS validator"
```

---

## Task 9 — DWS Internal: Portal Inbox Panel

Shows comments, documents, and objections from portal users on the FileMaster Details page. DWS validators can mark comments read and post a reply.

**Files:**
- **Create:** `Views/FileMaster/_PortalInboxPanel.cshtml`
- **Modify:** `Views/FileMaster/Details.cshtml` — include panel
- **Modify:** `Controllers/FileMasterController.cs` — add PortalInbox, MarkCommentRead, PortalReply actions

- [ ] **Step 9.1 — Add portal inbox actions to FileMasterController**

In `Controllers/FileMasterController.cs`, add at the end of the class (before the closing `}`):

```csharp
    // ── Portal Inbox ──

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCapture)]
    public async Task<IActionResult> MarkCommentRead(Guid id, Guid commentId, CancellationToken ct)
    {
        var comment = await _context.CaseComments.FindAsync(new object[] { commentId }, ct);
        if (comment is null || comment.FileMasterId != id) return NotFound();
        comment.ReadByDWSDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCapture)]
    public async Task<IActionResult> PortalReply(Guid id, Guid? parentCommentId,
        string replyText, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(replyText))
        {
            TempData["Error"] = "Reply text cannot be empty.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _context.CaseComments.Add(new CaseComment
        {
            CommentId = Guid.NewGuid(),
            FileMasterId = id,
            ApplicationUserId = userId is not null ? Guid.Parse(userId) : null,
            AuthorType = "DWSOfficial",
            ParentCommentId = parentCommentId,
            CommentText = replyText,
            SubmittedDate = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(ct);

        // Find the portal user on the parent comment and notify them of the reply
        if (parentCommentId.HasValue)
        {
            var parent = await _context.CaseComments.FindAsync(new object[] { parentCommentId.Value }, ct);
            if (parent?.PublicUserId.HasValue == true)
                await _notify.NotifyPublicUserAsync(parent.PublicUserId!.Value, id, "Reply",
                    "DWS has responded to your comment",
                    replyText.Length > 200 ? replyText[..200] + "..." : replyText,
                    actionUrl: null, ct);
        }

        TempData["Success"] = "Reply posted.";
        return RedirectToAction(nameof(Details), new { id });
    }
```

Note: `FileMasterController` already injects `_context` (ApplicationDBContext) and `_notify` (added in Task 3). Add `using System.Security.Claims;` at the top if not already present.

- [ ] **Step 9.2 — Create _PortalInboxPanel partial**

Create `Views/FileMaster/_PortalInboxPanel.cshtml`:
```html
@model FileMaster
@{
    var fmId = Model.FileMasterId;
}
@{
    var comments = ViewBag.PortalComments as IList<CaseComment> ?? new List<CaseComment>();
    var documents = ViewBag.PortalDocuments as IList<Document> ?? new List<Document>();
    var objections = ViewBag.PortalObjections as IList<Objection> ?? new List<Objection>();
    var unreadCount = comments.Count(c => c.AuthorType == "PublicUser" && c.ReadByDWSDate is null);
}

<div class="panel" id="portal-inbox">
    <div class="panel__header">
        Portal Inbox
        @if (unreadCount > 0)
        {
            <span class="badge badge--amber" style="margin-left:8px">@unreadCount unread</span>
        }
    </div>
    <div class="panel__body">

        @* ── Comments / Responses ── *@
        <h4 style="margin:0 0 var(--dws-space-2) 0;font-size:14px">Comments &amp; Responses</h4>
        @if (!comments.Any())
        {
            <p class="text-muted" style="font-size:var(--dws-fs-sm)">No comments yet.</p>
        }
        else
        {
            @foreach (var c in comments.Where(c => c.ParentCommentId is null).OrderBy(c => c.SubmittedDate))
            {
                <div style="border-left:3px solid @(c.AuthorType == "PublicUser" ? "var(--dws-teal-600)" : "var(--dws-primary)");padding:6px 10px;margin-bottom:6px;background:var(--dws-surface)">
                    <div style="font-size:var(--dws-fs-xs);color:var(--dws-text-muted)">
                        @(c.AuthorType == "PublicUser" ? "Water User" : "DWS Official")
                        — @c.SubmittedDate.ToString("dd MMM yyyy HH:mm")
                        @if (c.AuthorType == "PublicUser" && c.ReadByDWSDate is null)
                        {
                            <span style="color:var(--dws-amber-700);font-weight:600"> ● Unread</span>
                        }
                    </div>
                    <p style="margin:4px 0 6px 0;font-size:var(--dws-fs-sm)">@c.CommentText</p>

                    @if (c.AuthorType == "PublicUser" && c.ReadByDWSDate is null)
                    {
                        <form method="post" asp-action="MarkCommentRead" asp-route-id="@fmId" asp-route-commentId="@c.CommentId" style="display:inline">
                            @Html.AntiForgeryToken()
                            <button type="submit" class="btn btn-sm btn-outline-secondary" style="font-size:11px;padding:2px 8px">Mark Read</button>
                        </form>
                    }

                    @* Nested DWS replies *@
                    @foreach (var reply in comments.Where(r => r.ParentCommentId == c.CommentId).OrderBy(r => r.SubmittedDate))
                    {
                        <div style="margin-left:16px;padding:4px 8px;border-left:2px solid var(--dws-border);font-size:var(--dws-fs-sm)">
                            <span style="font-size:var(--dws-fs-xs);color:var(--dws-text-muted)">DWS — @reply.SubmittedDate.ToString("dd MMM yyyy HH:mm")</span>
                            <p style="margin:2px 0">@reply.CommentText</p>
                        </div>
                    }

                    @* Reply form *@
                    <details style="margin-top:6px">
                        <summary style="font-size:var(--dws-fs-xs);cursor:pointer;color:var(--dws-primary)">Reply</summary>
                        <form method="post" asp-action="PortalReply" asp-route-id="@fmId" style="margin-top:4px">
                            @Html.AntiForgeryToken()
                            <input type="hidden" name="parentCommentId" value="@c.CommentId" />
                            <textarea name="replyText" rows="3" placeholder="Your reply..."
                                      style="width:100%;padding:5px 8px;border:1px solid var(--dws-border);border-radius:4px;font-size:var(--dws-fs-sm);box-sizing:border-box"></textarea>
                            <button type="submit" class="btn btn-sm btn-primary" style="margin-top:4px">Send Reply</button>
                        </form>
                    </details>
                </div>
            }
        }

        @* ── Documents ── *@
        <h4 style="margin:var(--dws-space-4) 0 var(--dws-space-2) 0;font-size:14px">Uploaded Documents</h4>
        @if (!documents.Any())
        {
            <p class="text-muted" style="font-size:var(--dws-fs-sm)">No documents uploaded by water users.</p>
        }
        else
        {
            <ul style="list-style:none;padding:0;margin:0">
                @foreach (var d in documents.OrderByDescending(d => d.UploadDate))
                {
                    <li style="padding:4px 0;border-bottom:1px solid var(--dws-border);font-size:var(--dws-fs-sm)">
                        <strong>@d.FileName</strong>
                        — @d.DocumentType
                        <span class="text-muted" style="font-size:var(--dws-fs-xs)"> @d.UploadDate.ToString("dd MMM yyyy")</span>
                        <span class="badge @(d.VirusScanStatus == "Clean" ? "badge--green" : d.VirusScanStatus == "Infected" ? "badge--red" : "badge--amber")"
                              style="margin-left:6px;font-size:10px">@(d.VirusScanStatus ?? "Pending")</span>
                    </li>
                }
            </ul>
        }

        @* ── Objections ── *@
        <h4 style="margin:var(--dws-space-4) 0 var(--dws-space-2) 0;font-size:14px">Objections / Appeals</h4>
        @if (!objections.Any())
        {
            <p class="text-muted" style="font-size:var(--dws-fs-sm)">No objections lodged.</p>
        }
        else
        {
            @foreach (var o in objections.OrderByDescending(o => o.LodgedDate))
            {
                <div style="padding:6px 0;border-bottom:1px solid var(--dws-border);font-size:var(--dws-fs-sm)">
                    Lodged @o.LodgedDate.ToString("dd MMM yyyy")
                    — Status: <strong>@o.Status</strong>
                    @if (!string.IsNullOrEmpty(o.ResolutionNotes))
                    {
                        <p style="margin:4px 0;color:var(--dws-text-muted)">@o.ResolutionNotes</p>
                    }
                </div>
            }
        }
    </div>
</div>
```

- [ ] **Step 9.3 — Load portal data in FileMasterController.Details**

In `Controllers/FileMasterController.cs`, find the `Details` action (GET). After the `vm.Letters = ...` line (where letters are loaded), add:

```csharp
        // Portal inbox data for the _PortalInboxPanel partial
        ViewBag.PortalComments = await _context.CaseComments
            .Where(c => c.FileMasterId == id)
            .OrderBy(c => c.SubmittedDate)
            .ToListAsync();

        ViewBag.PortalDocuments = await _context.Documents
            .Where(d => d.FileMasterId == id && d.UploadedByPublicUserId != null)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();

        ViewBag.PortalObjections = await _context.Objections
            .Where(o => o.FileMasterId == id)
            .OrderByDescending(o => o.LodgedDate)
            .ToListAsync();
```

- [ ] **Step 9.4 — Include panel in Details.cshtml**

In `Views/FileMaster/Details.cshtml`, find the section that renders the letters panel partial:
```csharp
@await Html.PartialAsync("_LettersPanel", Model)
```
After it, add:
```csharp
@await Html.PartialAsync("_PortalInboxPanel", Model)
```

- [ ] **Step 9.5 — Build**
```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 9.6 — Commit**
```bash
git add Views/FileMaster/_PortalInboxPanel.cshtml Views/FileMaster/Details.cshtml Controllers/FileMasterController.cs Controllers/Admin/PropertyClaimsController.cs
git commit -m "feat(internal): portal inbox panel on FileMaster Details — comments, docs, objections from portal; MarkRead + PortalReply"
```

---

## Task 10 — Migration + Full Test Run

- [ ] **Step 10.1 — Check for pending migration**

Run:
```bash
dotnet ef migrations list 2>&1 | tail -5
```
If the `Notification`, `CaseComment`, `Objection`, `ObjectionDocument`, `Document`, and `PublicUserProperty` tables are already covered by an existing migration, skip Step 10.2.

- [ ] **Step 10.2 — Generate migration (only if needed)**
```bash
dotnet ef migrations add Wave3S35Loop
```
Then apply:
```bash
dotnet ef database update
```
Expected: migration applied, no errors.

- [ ] **Step 10.3 — Full test run**
```bash
cd Tests && dotnet test -v minimal 2>&1 | tail -15
```
Expected: all tests pass (243 existing + ~14 new = ~257 total).

- [ ] **Step 10.4 — Final build**
```bash
dotnet build --configuration Release
```
Expected: Build succeeded, 0 errors, 0 warnings (or only known pre-existing warnings).

- [ ] **Step 10.5 — Final commit**
```bash
git add Migrations/ 2>/dev/null; true
git commit -m "feat(wave3): S35 end-to-end loop complete — notification service, external portal features, DWS portal inbox"
```

---

## Self-Review

### Spec coverage check

| Requirement | Covered by |
|-------------|------------|
| PDF letter generation | Already wired (QuestPdfRenderer registered in Program.cs before this plan) |
| Real email delivery | Task 1 — SmtpEmailSender with MailKit |
| Notification service | Task 2 — INotificationService / NotificationService |
| Notify water user when letter issued | Task 3 — FileMasterController.IssueLetter |
| Water user claims a property | Task 4 — PropertyClaimController |
| DWS approves/rejects claim | Task 4 — PropertyClaimsController (admin) |
| Case status view | Task 5 — CaseController.Index |
| Per-case detail | Task 5 — CaseController.Detail |
| Letter download for portal user | Task 5 — CaseController.DownloadLetter |
| Document upload by water user | Task 6 — DocumentController |
| Letter response / CaseComment | Task 7 — ResponseController |
| Objection / Appeal lodging | Task 8 — ObjectionController |
| DWS sees portal activity | Task 9 — _PortalInboxPanel + FileMasterController |
| DWS replies to water user | Task 9 — PortalReply + notification back to portal user |
| Migration for new tables | Task 10 |

### Placeholder scan
None. All steps contain exact file paths and complete code blocks.

### Type consistency check
- `INotificationService.NotifyPublicUserAsync` and `NotifyDwsValidatorAsync` signatures match across Tasks 2, 3, 4, 6, 7, 8, 9.
- `PropertyClaimController` uses `PropertyClaimStatus.Pending` / `PropertyClaimStatus.Approved` — matches `PropertyClaimStatus` enum (Pending=0, Approved=1, Rejected=2).
- `CaseSummaryViewModel` / `CaseDetailViewModel` referenced by `CaseController` and `Case/Index.cshtml` / `Case/Detail.cshtml` — consistent.
- `PublicUserPropertyAccessor.AssertHasAccessToFileMasterAsync` throws `NotFoundException` — caught as generic `catch { return Forbid/NotFound; }` in CaseController, DocumentController, ResponseController, ObjectionController.
