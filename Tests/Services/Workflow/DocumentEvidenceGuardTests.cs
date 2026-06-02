using dwa_ver_val.Services.Documents;
using dwa_ver_val.Services.Workflow;
using dwa_ver_val.Services.Workflow.Guards;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace dwa_ver_val.Tests.Services.Workflow;

public class DocumentEvidenceGuardTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static FileMaster NewCase() => new FileMaster
    {
        FileMasterId = Guid.NewGuid(),
        PropertyId = Guid.NewGuid(),
        RegistrationNumber = "N/A",
        SurveyorGeneralCode = "N/A",
        PrimaryCatchment = "N/A",
        QuaternaryCatchment = "N/A",
        FarmName = "N/A",
        FarmNumber = 0,
        RegistrationDivision = "N/A",
        FarmPortion = "N/A"
    };

    private static GuardContext Leaving(FileMaster fm, string fromCp, string toCp) =>
        new(fm,
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = $"{fromCp}_Step", DisplayOrder = 1, Phase = "Test" },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = $"{toCp}_Step", DisplayOrder = 2, Phase = "Test" });

    private static void AddDoc(ApplicationDBContext db, Guid fileMasterId, string type, string? virusStatus)
    {
        db.Documents.Add(new Document
        {
            DocumentId = Guid.NewGuid(),
            FileMasterId = fileMasterId,
            DocumentType = type,
            FileName = "f.pdf",
            BlobPath = "x/f.pdf",
            VirusScanStatus = virusStatus,
            SyncStatus = "NotSynced",
            UploadDate = DateTime.UtcNow
        });
    }

    [Fact]
    public async Task Cp2_DeniesWhenTitleDeedAndSgMissing()
    {
        using var db = NewDb();
        var fm = NewCase();
        db.FileMasters.Add(fm);
        await db.SaveChangesAsync();

        var result = await new DocumentEvidenceGuard(db).CheckAsync(Leaving(fm, "CP2", "CP3"));
        Assert.False(result.Allowed);
        Assert.Contains("Title Deed report", result.Reason);
    }

    [Fact]
    public async Task Cp2_AllowsWhenBothPresent()
    {
        using var db = NewDb();
        var fm = NewCase();
        db.FileMasters.Add(fm);
        AddDoc(db, fm.FileMasterId, DocumentTypes.TitleDeedReport, "Clean");
        AddDoc(db, fm.FileMasterId, DocumentTypes.SgDiagram, null); // null virus status is acceptable
        await db.SaveChangesAsync();

        var result = await new DocumentEvidenceGuard(db).CheckAsync(Leaving(fm, "CP2", "CP3"));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task Cp3_DeniesWhenWarmsReportInfected()
    {
        using var db = NewDb();
        var fm = NewCase();
        db.FileMasters.Add(fm);
        AddDoc(db, fm.FileMasterId, DocumentTypes.WarmsReport, "Infected");
        await db.SaveChangesAsync();

        var result = await new DocumentEvidenceGuard(db).CheckAsync(Leaving(fm, "CP3", "CP4"));
        Assert.False(result.Allowed);
        Assert.Contains("WARMS report", result.Reason);
    }

    [Fact]
    public async Task Cp3_AllowsWhenWarmsReportPresent()
    {
        using var db = NewDb();
        var fm = NewCase();
        db.FileMasters.Add(fm);
        AddDoc(db, fm.FileMasterId, DocumentTypes.WarmsReport, "Pending");
        await db.SaveChangesAsync();

        var result = await new DocumentEvidenceGuard(db).CheckAsync(Leaving(fm, "CP3", "CP4"));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task PassesWhenLeavingUnmappedState()
    {
        using var db = NewDb();
        var fm = NewCase();
        db.FileMasters.Add(fm);
        await db.SaveChangesAsync();

        var result = await new DocumentEvidenceGuard(db).CheckAsync(Leaving(fm, "CP6", "CP7"));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task PassesOnInternalTransition_SameCp()
    {
        using var db = NewDb();
        var fm = NewCase();
        db.FileMasters.Add(fm);
        await db.SaveChangesAsync();

        // Leaving CP2 → CP2 (sub-step) must not fire.
        var result = await new DocumentEvidenceGuard(db).CheckAsync(Leaving(fm, "CP2", "CP2"));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task Cp2_DoesNotCountDocumentBelongingToDifferentCase()
    {
        using var db = NewDb();
        var fm = NewCase();
        var otherFm = NewCase();
        db.FileMasters.Add(fm);
        db.FileMasters.Add(otherFm);
        AddDoc(db, otherFm.FileMasterId, DocumentTypes.TitleDeedReport, "Clean");
        AddDoc(db, otherFm.FileMasterId, DocumentTypes.SgDiagram, "Clean");
        await db.SaveChangesAsync();

        var result = await new DocumentEvidenceGuard(db).CheckAsync(Leaving(fm, "CP2", "CP3"));
        Assert.False(result.Allowed);
    }
}
