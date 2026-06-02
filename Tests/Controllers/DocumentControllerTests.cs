using System.Security.Claims;
using dwa_ver_val.Controllers;
using dwa_ver_val.Services.Documents;
using dwa_ver_val.Services.Infrastructure.Storage;
using dwa_ver_val.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace dwa_ver_val.Tests.Controllers;

public class DocumentControllerTests
{
    private sealed class FakeStorage : IFileStorage
    {
        public Task<StoredFileResult> SaveAsync(Stream content, string contentType, string originalFileName, CancellationToken ct)
            => Task.FromResult(new StoredFileResult
            {
                RelativePath = "docs/" + originalFileName,
                ContentType = contentType,
                SizeBytes = content.Length,
                Sha256Hex = "deadbeef"
            });
        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct)
            => Task.FromResult<Stream>(new MemoryStream(new byte[] { 1, 2, 3 }));
        public Task<bool> DeleteAsync(string relativePath, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> ExistsAsync(string relativePath, CancellationToken ct) => Task.FromResult(true);
    }

    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ClaimsPrincipal User(Guid userId, string role) =>
        new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
        }, "TestAuth"));

    private static (FileMaster fm, Property prop) SeedCase(ApplicationDBContext db, Guid wmaId)
    {
        var prop = new Property { PropertyId = Guid.NewGuid(), PropertyReferenceNumber = "P", SGCode = "SG", WmaId = wmaId };
        var fm = new FileMaster
        {
            FileMasterId = Guid.NewGuid(), PropertyId = prop.PropertyId,
            RegistrationNumber = "N/A", SurveyorGeneralCode = "SG", PrimaryCatchment = "A21",
            QuaternaryCatchment = "A21A", FarmName = "F", FarmNumber = 1,
            RegistrationDivision = "TD", FarmPortion = "0"
        };
        db.Properties.Add(prop); db.FileMasters.Add(fm);
        db.SaveChanges();
        return (fm, prop);
    }

    private static IFormFile PdfFile(string name = "deed.pdf")
    {
        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }

    private static DocumentController BuildController(ApplicationDBContext db, ClaimsPrincipal user)
    {
        var controller = new DocumentController(db, new ScopedCaseQuery(db), new FakeStorage(), new TestAuditService());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        return controller;
    }

    [Fact]
    public async Task Upload_PersistsAnnotatedDocument_WhenInScope()
    {
        using var db = NewDb();
        var wma = Guid.NewGuid();
        var (fm, _) = SeedCase(db, wma);
        var uid = Guid.NewGuid();
        var ctrl = BuildController(db, User(uid, DwsRoles.Validator));
        ((ClaimsIdentity)ctrl.User.Identity!).AddClaim(new Claim("wmaId", wma.ToString()));

        var model = new CaseDocumentUploadViewModel
        {
            FileMasterId = fm.FileMasterId,
            DocumentType = DocumentTypes.TitleDeedReport,
            File = PdfFile()
        };

        var result = await ctrl.Upload(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var doc = await db.Documents.SingleAsync();
        Assert.Equal(DocumentTypes.TitleDeedReport, doc.DocumentType);
        Assert.Equal(uid, doc.UploadedByUserId);
        Assert.Equal("Pending", doc.VirusScanStatus);
        Assert.Equal("NotSynced", doc.SyncStatus);
        Assert.Equal("deadbeef", doc.DocumentHash);
    }

    [Fact]
    public async Task Upload_RejectsUnknownDocumentType()
    {
        using var db = NewDb();
        var wma = Guid.NewGuid();
        var (fm, _) = SeedCase(db, wma);
        var ctrl = BuildController(db, User(Guid.NewGuid(), DwsRoles.Validator));
        ((ClaimsIdentity)ctrl.User.Identity!).AddClaim(new Claim("wmaId", wma.ToString()));

        var model = new CaseDocumentUploadViewModel
        {
            FileMasterId = fm.FileMasterId,
            DocumentType = "JunkType",
            File = PdfFile()
        };

        var result = await ctrl.Upload(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.False(ctrl.ModelState.IsValid);
        Assert.Empty(db.Documents);
    }

    [Fact]
    public async Task Upload_RejectsDisallowedExtension()
    {
        using var db = NewDb();
        var wma = Guid.NewGuid();
        var (fm, _) = SeedCase(db, wma);
        var ctrl = BuildController(db, User(Guid.NewGuid(), DwsRoles.Validator));
        ((ClaimsIdentity)ctrl.User.Identity!).AddClaim(new Claim("wmaId", wma.ToString()));

        var model = new CaseDocumentUploadViewModel
        {
            FileMasterId = fm.FileMasterId,
            DocumentType = DocumentTypes.TitleDeedReport,
            File = PdfFile("malware.exe")
        };

        var result = await ctrl.Upload(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Empty(db.Documents);
    }

    [Fact]
    public async Task Upload_ForbidsWhenCaseOutOfScope()
    {
        using var db = NewDb();
        var (fm, _) = SeedCase(db, Guid.NewGuid());
        var ctrl = BuildController(db, User(Guid.NewGuid(), DwsRoles.Validator));
        ((ClaimsIdentity)ctrl.User.Identity!).AddClaim(new Claim("wmaId", Guid.NewGuid().ToString()));

        var model = new CaseDocumentUploadViewModel
        {
            FileMasterId = fm.FileMasterId,
            DocumentType = DocumentTypes.TitleDeedReport,
            File = PdfFile()
        };

        var result = await ctrl.Upload(model, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(db.Documents);
    }

    [Fact]
    public async Task Delete_RemovesDocument_WhenInScope()
    {
        using var db = NewDb();
        var wma = Guid.NewGuid();
        var (fm, _) = SeedCase(db, wma);
        var doc = new Document
        {
            DocumentId = Guid.NewGuid(), FileMasterId = fm.FileMasterId,
            DocumentType = DocumentTypes.TitleDeedReport, FileName = "d.pdf",
            BlobPath = "docs/d.pdf", SyncStatus = "NotSynced", UploadDate = DateTime.UtcNow
        };
        db.Documents.Add(doc); await db.SaveChangesAsync();

        var ctrl = BuildController(db, User(Guid.NewGuid(), DwsRoles.Validator));
        ((ClaimsIdentity)ctrl.User.Identity!).AddClaim(new Claim("wmaId", wma.ToString()));

        var result = await ctrl.Delete(doc.DocumentId, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(db.Documents);
    }
}
