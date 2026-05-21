using dwa_ver_val.Services.Audit;
using dwa_ver_val.Services.Letters;
using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using QuestPDF.Infrastructure;
using Xunit;

namespace dwa_ver_val.Tests.Services.Letters;

/// <summary>
/// BUG-013 acceptance: the named recipient submitted on the IssueLetter form must be
/// persisted to LetterIssuance.RecipientName, distinct from the issuing/signing user's
/// display name (which is stored in ServingOfficialName for in-person service).
/// This proves the recipient is not overwritten by the signatory — NWA S35(2)(d) record.
/// </summary>
public class LetterServiceRecipientTests
{
    static LetterServiceRecipientTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static LetterService BuildService(ApplicationDBContext db)
    {
        var template = new Mock<ILetterTemplate>();
        template.SetupGet(t => t.LetterCode).Returns("S35_L1");
        template.SetupGet(t => t.Title).Returns("Test Letter");
        template.SetupGet(t => t.NWAReference).Returns("Section 35(1)");

        var registry = new Mock<ILetterTemplateRegistry>();
        registry.Setup(r => r.Get(It.IsAny<string>())).Returns(template.Object);

        var renderer = new Mock<IPdfRenderer>();
        renderer.Setup(r => r.RenderLetter(It.IsAny<ILetterTemplate>(), It.IsAny<LetterContext>()))
                .Returns(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // "%PDF"

        var blobs = new Mock<IBlobStore>();
        blobs.Setup(b => b.WriteAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
             .ReturnsAsync((string path, byte[] _) => path);

        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.LogAsync(It.IsAny<AuditEvent>())).Returns(Task.CompletedTask);

        return new LetterService(db, registry.Object, renderer.Object, blobs.Object, audit.Object);
    }

    [Fact]
    public async Task IssueAsync_PersistsSubmittedRecipientName_DistinctFromIssuingUser()
    {
        const string recipient = "P.J. van der Merwe";
        const string issuingUserDisplayName = "Thabo Official";

        var db = TestDbContextFactory.Create();
        var prop = new Property { PropertyId = Guid.NewGuid() };
        db.Properties.Add(prop);
        var fm = SeedHelper.NewFileMaster(prop.PropertyId);
        db.FileMasters.Add(fm);
        db.LetterTypes.Add(new LetterType
        {
            LetterTypeId = Guid.NewGuid(),
            LetterName = "S35_L1",
            LetterDescription = "S35 Letter 1"
        });
        await db.SaveChangesAsync();

        var sut = BuildService(db);
        var officialId = Guid.NewGuid();

        var req = new IssueLetterRequest(
            RecipientName: recipient,
            RecipientAddress: null,
            IssueMethod: "InPerson",
            IssueDate: DateOnly.FromDateTime(DateTime.Today),
            DueDate: DateOnly.FromDateTime(DateTime.Today.AddDays(60)),
            ServedByOfficialId: officialId,                 // in-person service → ServingOfficialName set
            AdditionalNotes: null,
            SignedByUserId: officialId,
            SignedByDisplayName: issuingUserDisplayName,     // the issuing/signing official's name
            SignedByTitle: "Regional Manager",
            SignedByOrgUnit: "Limpopo Regional Office");

        var issuance = await sut.IssueAsync(fm.FileMasterId, "S35_L1", req);

        // Re-read from the database to prove it was actually persisted, not just set in memory.
        var saved = await db.LetterIssuances.SingleAsync(l => l.LetterIssuanceId == issuance.LetterIssuanceId);

        Assert.Equal(recipient, saved.RecipientName);
        // The recipient must NOT be the issuing user's display name.
        Assert.NotEqual(issuingUserDisplayName, saved.RecipientName);
        // For in-person service the issuing user's name is recorded separately as the serving official.
        Assert.Equal(issuingUserDisplayName, saved.ServingOfficialName);
    }
}
