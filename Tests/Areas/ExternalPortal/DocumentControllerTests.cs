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
        var fm = SeedHelper.NewFileMaster(prop.PropertyId);
        db.Properties.Add(prop);
        db.FileMasters.Add(fm);
        db.PublicUserProperties.Add(new PublicUserProperty
        {
            Id = Guid.NewGuid(), PublicUserId = userId, PropertyId = prop.PropertyId,
            Status = PropertyClaimStatus.Approved,
            EvidenceType = PropertyClaimEvidenceType.IdMatch, RequestedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Use real PDF magic bytes (%PDF) so the magic-byte check passes.
        var fileContent = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("titledeed.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.Length).Returns(fileContent.Length);
        file.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(fileContent));

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

    // ── EXT-01: GET access check ─────────────────────────────────────────────

    [Fact]
    public async Task Upload_Get_LinkedCase_ReturnsView()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);

        var prop = new Property { PropertyId = Guid.NewGuid(), SGCode = "T0010", WmaId = null, PropertyReferenceNumber = "R10" };
        var fm = SeedHelper.NewFileMaster(prop.PropertyId);
        db.Properties.Add(prop);
        db.FileMasters.Add(fm);
        db.PublicUserProperties.Add(new PublicUserProperty
        {
            Id = Guid.NewGuid(), PublicUserId = userId, PropertyId = prop.PropertyId,
            Status = PropertyClaimStatus.Approved,
            EvidenceType = PropertyClaimEvidenceType.IdMatch, RequestedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await controller.Upload(fm.FileMasterId, default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocumentUploadViewModel>(view.Model);
        Assert.Equal(fm.FileMasterId, model.FileMasterId);
    }

    [Fact]
    public async Task Upload_Get_UnlinkedCase_ReturnsForbid()
    {
        var userId = Guid.NewGuid();
        var (_, controller) = Build(userId);

        var result = await controller.Upload(Guid.NewGuid(), default);

        Assert.IsType<ForbidResult>(result);
    }

    // ── DOC-03: document type vocabulary validation ──────────────────────────

    [Fact]
    public async Task Upload_Post_UnknownDocumentType_ReturnsView_NotSaved()
    {
        // DOC-03: an unknown DocumentType must be rejected and nothing saved.
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);

        var prop = new Property { PropertyId = Guid.NewGuid(), SGCode = "T0002", WmaId = null, PropertyReferenceNumber = "R2" };
        var fm = SeedHelper.NewFileMaster(prop.PropertyId);
        db.Properties.Add(prop);
        db.FileMasters.Add(fm);
        db.PublicUserProperties.Add(new PublicUserProperty
        {
            Id = Guid.NewGuid(), PublicUserId = userId, PropertyId = prop.PropertyId,
            Status = PropertyClaimStatus.Approved,
            EvidenceType = PropertyClaimEvidenceType.IdMatch, RequestedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var pdfMagic = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("deed.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.Length).Returns(pdfMagic.Length);
        file.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(pdfMagic));

        var result = await controller.Upload(
            new DocumentUploadViewModel
            {
                FileMasterId = fm.FileMasterId,
                DocumentType = "Malware",   // not in DocumentTypes.All
                File = file.Object
            }, default);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(db.Documents);
    }

    // ── DOC-03: explicit size cap ────────────────────────────────────────────

    [Fact]
    public async Task Upload_Post_OversizeFile_ReturnsView_NotSaved()
    {
        // DOC-03: file larger than the 10 MB cap must be rejected.
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);

        var prop = new Property { PropertyId = Guid.NewGuid(), SGCode = "T0003", WmaId = null, PropertyReferenceNumber = "R3" };
        var fm = SeedHelper.NewFileMaster(prop.PropertyId);
        db.Properties.Add(prop);
        db.FileMasters.Add(fm);
        db.PublicUserProperties.Add(new PublicUserProperty
        {
            Id = Guid.NewGuid(), PublicUserId = userId, PropertyId = prop.PropertyId,
            Status = PropertyClaimStatus.Approved,
            EvidenceType = PropertyClaimEvidenceType.IdMatch, RequestedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        const long OverLimit = 10L * 1024 * 1024 + 1; // 10 MB + 1 byte
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("big.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.Length).Returns(OverLimit);
        // OpenReadStream content doesn't matter — the size check fires before the magic-byte check
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }));

        var result = await controller.Upload(
            new DocumentUploadViewModel
            {
                FileMasterId = fm.FileMasterId,
                DocumentType = "TitleDeed",
                File = file.Object
            }, default);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(db.Documents);
    }

    // ── DOC-02: magic-byte content validation ────────────────────────────────

    [Fact]
    public async Task Upload_Post_ContentExtensionMismatch_ReturnsView_NotSaved()
    {
        // DOC-02: "hello" bytes with a .pdf extension must be rejected.
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);

        var prop = new Property { PropertyId = Guid.NewGuid(), SGCode = "T0004", WmaId = null, PropertyReferenceNumber = "R4" };
        var fm = SeedHelper.NewFileMaster(prop.PropertyId);
        db.Properties.Add(prop);
        db.FileMasters.Add(fm);
        db.PublicUserProperties.Add(new PublicUserProperty
        {
            Id = Guid.NewGuid(), PublicUserId = userId, PropertyId = prop.PropertyId,
            Status = PropertyClaimStatus.Approved,
            EvidenceType = PropertyClaimEvidenceType.IdMatch, RequestedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var helloBytes = Encoding.UTF8.GetBytes("hello");
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("notreally.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.Length).Returns(helloBytes.Length);
        file.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(helloBytes));

        var result = await controller.Upload(
            new DocumentUploadViewModel
            {
                FileMasterId = fm.FileMasterId,
                DocumentType = "TitleDeed",
                File = file.Object
            }, default);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(db.Documents);
    }
}
