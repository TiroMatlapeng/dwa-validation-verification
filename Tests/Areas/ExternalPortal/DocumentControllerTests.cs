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
