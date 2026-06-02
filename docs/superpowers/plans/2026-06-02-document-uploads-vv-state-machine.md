# Document Uploads as Gated Workflow Evidence — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let internal DWS staff upload documents to a V&V case, annotated and eWULAAS-ready, and make selected Appendix A documents mandatory evidence that gates workflow transitions.

**Architecture:** Reuse the existing `Document` model + `IFileStorage`. Add annotation/sync fields. A single generic `DocumentEvidenceGuard` (registered alongside existing `ITransitionGuard`s) reads one shared `DocumentRequirements` map and blocks a transition when a mandatory document for the control point being left is absent (or virus-infected). The existing `Cp11FileCompilationGuard` re-checks the full Appendix A document set using the same map. A new internal `DocumentController` plus a documents panel on the FileMaster detail view provide upload/list/download/delete.

**Tech Stack:** ASP.NET Core 10 MVC, EF Core 10 (SQL Server), xUnit + EF InMemory provider, QuestPDF (unrelated here), existing `IFileStorage`/`IAuditService`/`IScopedCaseQuery`.

**Spec:** `docs/superpowers/specs/2026-06-02-document-uploads-vv-state-machine-design.md`

**Implementation note (deviation from spec §5.1):** The requirements map is keyed by **control-point prefix** (`"CP2"`, `"CP3"`) rather than full state name, and the guard matches via the existing `Cp2SpatialInfoGuard.IsLeaving(ctx, prefix)` helper. This is consistent with every sibling guard and with the existing `GuardTests` helper (which builds states named `"{cp}_Step"`). Behaviour is identical for the real seeded states (`CP2_SpatialInfo`, `CP3_WARMSEvaluation`, `CP11_FileCompiled`).

**Deviation from spec §6 (notifications):** Internal upload writes an `AuditLog` but does **not** call `INotificationService` (it would notify the same validator who just uploaded). Cross-actor notification stays handled by the external-portal upload flow. Delete is audited.

---

## File Structure

**Create**
- `Services/Documents/DocumentTypes.cs` — controlled vocabulary (codes, display names, Appendix A flag).
- `Services/Workflow/Guards/DocumentRequirements.cs` — shared map of CP-prefix → required docs + the CP11 set + `DocReq`/`DocumentRequirementStatus` records + status helper.
- `Services/Workflow/Guards/DocumentEvidenceGuard.cs` — the generic gating guard.
- `Controllers/DocumentController.cs` — internal upload/list/download/delete.
- `ViewModels/CaseDocumentUploadViewModel.cs` — internal upload form model.
- `Views/Document/Upload.cshtml` — internal upload form.
- `Views/FileMaster/_DocumentsPanel.cshtml` — documents + requirements checklist panel.
- `Tests/Services/Documents/DocumentTypesTests.cs`
- `Tests/Services/Workflow/DocumentRequirementsTests.cs`
- `Tests/Services/Workflow/DocumentEvidenceGuardTests.cs`
- `Tests/Controllers/DocumentControllerTests.cs`

**Modify**
- `Models/Document.cs` — add `WorkflowStateId`, `WorkflowState`, `ExternalDocumentRef`, `SyncStatus`.
- `DatabaseContexts/ApplicationDBContext.cs` — `Document → WorkflowState` relationship + `SyncStatus`/`ExternalDocumentRef` column constraints.
- `Services/Auth/DwsPolicies.cs` — add `CanManageDocuments` policy (Validator+).
- `Services/Workflow/Guards/FlagGuards.cs` — extend `Cp11FileCompilationGuard` to re-check documents.
- `Program.cs` — register `DocumentEvidenceGuard` as `ITransitionGuard`.
- `ViewModels/FileMasterDetailsViewModel.cs` — add `CaseDocuments` + `DocumentRequirementStatuses`.
- `Controllers/FileMasterController.cs` — populate the two new VM fields in `Details`.
- `Views/FileMaster/Details.cshtml` — render `_DocumentsPanel`.
- `Areas/ExternalPortal/ViewModels/DocumentUploadViewModel.cs` + `Areas/ExternalPortal/Views/Document/Upload.cshtml` — switch type dropdown to the shared vocabulary.

---

## Task 1: Annotate the Document model (fields + EF config + migration)

**Files:**
- Modify: `Models/Document.cs`
- Modify: `DatabaseContexts/ApplicationDBContext.cs:397-416` (after the existing Document relationships)
- Migration: `Migrations/*_AddDocumentWorkflowAnnotationAndSync.cs` (generated)

- [ ] **Step 1: Add the new fields to `Document`**

In `Models/Document.cs`, add these properties after `DocumentHash`:

```csharp
    // Annotation: which control point this document satisfies / belongs to (nullable).
    public Guid? WorkflowStateId { get; set; }
    public WorkflowState? WorkflowState { get; set; }

    // eWULAAS readiness — binary stays in IFileStorage now; pushed to eWULAAS later.
    public string? ExternalDocumentRef { get; set; }   // eWULAAS document id, null until synced
    public string SyncStatus { get; set; } = "NotSynced"; // NotSynced | Pending | Synced | Failed
```

- [ ] **Step 2: Configure the relationship + constraints**

In `ApplicationDBContext.OnModelCreating`, immediately after the `Document → UploadedBy (PublicUser)` block (around line 416), add:

```csharp
        // Document → WorkflowState (annotation; deleting a state must not delete docs)
        modelBuilder.Entity<Document>()
            .HasOne(d => d.WorkflowState)
            .WithMany()
            .HasForeignKey(d => d.WorkflowStateId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Document>()
            .Property(d => d.SyncStatus)
            .HasMaxLength(16)
            .IsRequired();

        modelBuilder.Entity<Document>()
            .Property(d => d.ExternalDocumentRef)
            .HasMaxLength(200);
```

- [ ] **Step 3: Verify the project builds**

Run: `dotnet build`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 4: Create the migration**

Run: `dotnet ef migrations add AddDocumentWorkflowAnnotationAndSync`
Expected: a new file under `Migrations/` whose `Up()` adds `WorkflowStateId`, `ExternalDocumentRef`, `SyncStatus` columns to `Documents` and an FK + index on `WorkflowStateId`. Open it and confirm it only touches the `Documents` table.

- [ ] **Step 5: Commit**

```bash
git add Models/Document.cs DatabaseContexts/ApplicationDBContext.cs Migrations/
git commit -m "feat(docs): annotate Document with workflow state + eWULAAS sync fields"
```

---

## Task 2: Document type vocabulary

**Files:**
- Create: `Services/Documents/DocumentTypes.cs`
- Test: `Tests/Services/Documents/DocumentTypesTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Tests/Services/Documents/DocumentTypesTests.cs`:

```csharp
using dwa_ver_val.Services.Documents;
using Xunit;

namespace dwa_ver_val.Tests.Services.Documents;

public class DocumentTypesTests
{
    [Fact]
    public void All_ContainsAppendixADocumentsFlaggedTrue()
    {
        Assert.True(DocumentTypes.All[DocumentTypes.WarmsReport].IsAppendixA);
        Assert.True(DocumentTypes.All[DocumentTypes.TitleDeedReport].IsAppendixA);
        Assert.True(DocumentTypes.All[DocumentTypes.SgDiagram].IsAppendixA);
    }

    [Fact]
    public void IsKnown_TrueForVocabularyCode_FalseForJunk()
    {
        Assert.True(DocumentTypes.IsKnown(DocumentTypes.WarmsReport));
        Assert.False(DocumentTypes.IsKnown("NotARealType"));
    }

    [Fact]
    public void Display_ReturnsHumanLabel()
    {
        Assert.Equal("WARMS Report", DocumentTypes.Display(DocumentTypes.WarmsReport));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~DocumentTypesTests`
Expected: FAIL — `DocumentTypes` does not exist.

- [ ] **Step 3: Implement the vocabulary**

Create `Services/Documents/DocumentTypes.cs`:

```csharp
namespace dwa_ver_val.Services.Documents;

/// <summary>
/// Controlled vocabulary of document type codes used by both the internal and external
/// uploaders and by the workflow document-requirements map. Codes are persisted to
/// Document.DocumentType. Appendix A items are flagged for report/panel grouping.
/// </summary>
public static class DocumentTypes
{
    public const string WarmsReport     = "WARMSReport";      // Appendix A item 2
    public const string TitleDeedReport = "TitleDeedReport";  // Appendix A item 3
    public const string SgDiagram       = "SGDiagram";        // Appendix A item 4
    public const string PreviousStudy   = "PreviousStudy";    // Appendix A item 5 (optional)
    public const string TitleDeed       = "TitleDeed";
    public const string Permit          = "Permit";
    public const string FieldSurvey     = "FieldSurvey";
    public const string Correspondence  = "Correspondence";
    public const string Other           = "Other";

    public static readonly IReadOnlyDictionary<string, (string Display, bool IsAppendixA)> All =
        new Dictionary<string, (string, bool)>(StringComparer.Ordinal)
        {
            [WarmsReport]     = ("WARMS Report", true),
            [TitleDeedReport] = ("Title Deed Report", true),
            [SgDiagram]       = ("SG Diagram", true),
            [PreviousStudy]   = ("Previous Study / Legislative Docs", true),
            [TitleDeed]       = ("Title Deed", false),
            [Permit]          = ("Permit", false),
            [FieldSurvey]     = ("Field Survey", false),
            [Correspondence]  = ("Correspondence", false),
            [Other]           = ("Other", false),
        };

    public static bool IsKnown(string? code) => code is not null && All.ContainsKey(code);

    public static string Display(string code) =>
        All.TryGetValue(code, out var v) ? v.Display : code;
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~DocumentTypesTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add Services/Documents/DocumentTypes.cs Tests/Services/Documents/DocumentTypesTests.cs
git commit -m "feat(docs): add controlled document-type vocabulary"
```

---

## Task 3: Shared document-requirements map

**Files:**
- Create: `Services/Workflow/Guards/DocumentRequirements.cs`
- Test: `Tests/Services/Workflow/DocumentRequirementsTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Tests/Services/Workflow/DocumentRequirementsTests.cs`:

```csharp
using dwa_ver_val.Services.Documents;
using dwa_ver_val.Services.Workflow.Guards;
using Xunit;

namespace dwa_ver_val.Tests.Services.Workflow;

public class DocumentRequirementsTests
{
    [Fact]
    public void Map_GatesTitleDeedAndSgAtCp2_AndWarmsAtCp3()
    {
        Assert.Contains(DocumentRequirements.Map["CP2"], r => r.DocumentType == DocumentTypes.TitleDeedReport);
        Assert.Contains(DocumentRequirements.Map["CP2"], r => r.DocumentType == DocumentTypes.SgDiagram);
        Assert.Contains(DocumentRequirements.Map["CP3"], r => r.DocumentType == DocumentTypes.WarmsReport);
    }

    [Fact]
    public void EveryRequiredCode_ExistsInVocabulary()
    {
        foreach (var req in DocumentRequirements.Map.Values.SelectMany(x => x))
            Assert.True(DocumentTypes.IsKnown(req.DocumentType), $"Unknown code: {req.DocumentType}");
        foreach (var req in DocumentRequirements.FileCompilationDocuments)
            Assert.True(DocumentTypes.IsKnown(req.DocumentType), $"Unknown code: {req.DocumentType}");
    }

    [Fact]
    public void FileCompilationDocuments_CoversTheThreeMandatoryItems()
    {
        var codes = DocumentRequirements.FileCompilationDocuments.Select(r => r.DocumentType).ToHashSet();
        Assert.Contains(DocumentTypes.WarmsReport, codes);
        Assert.Contains(DocumentTypes.TitleDeedReport, codes);
        Assert.Contains(DocumentTypes.SgDiagram, codes);
    }

    [Fact]
    public void StatusesFor_MarksPresentAndMissing()
    {
        var present = new HashSet<string> { DocumentTypes.WarmsReport };
        var statuses = DocumentRequirements.StatusesFor(present);

        Assert.Contains(statuses, s => s.DocumentType == DocumentTypes.WarmsReport && s.Mandatory && s.Present);
        Assert.Contains(statuses, s => s.DocumentType == DocumentTypes.SgDiagram && s.Mandatory && !s.Present);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~DocumentRequirementsTests`
Expected: FAIL — `DocumentRequirements` does not exist.

- [ ] **Step 3: Implement the map + records + status helper**

Create `Services/Workflow/Guards/DocumentRequirements.cs`:

```csharp
using dwa_ver_val.Services.Documents;

namespace dwa_ver_val.Services.Workflow.Guards;

/// <summary>One required document for a control point.</summary>
public record DocReq(string DocumentType, string DisplayName, string AtControlPoint);

/// <summary>Row used by the FileMaster documents panel checklist.</summary>
public record DocumentRequirementStatus(
    string DocumentType, string DisplayName, bool Mandatory, string? MandatoryAtCp, bool Present);

/// <summary>
/// Single source of truth for which documents are mandatory at which control point.
/// Keyed by control-point PREFIX (matches Cp2SpatialInfoGuard.IsLeaving). Consumed by
/// DocumentEvidenceGuard (per-CP gating) and Cp11FileCompilationGuard (final re-check).
/// </summary>
public static class DocumentRequirements
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<DocReq>> Map =
        new Dictionary<string, IReadOnlyList<DocReq>>(StringComparer.OrdinalIgnoreCase)
        {
            ["CP2"] = new[]
            {
                new DocReq(DocumentTypes.TitleDeedReport, "Title Deed report", "CP2"),
                new DocReq(DocumentTypes.SgDiagram, "SG Diagram", "CP2"),
            },
            ["CP3"] = new[]
            {
                new DocReq(DocumentTypes.WarmsReport, "WARMS report", "CP3"),
            },
        };

    /// <summary>The full Appendix A document set the CP11 compilation guard must re-check.</summary>
    public static readonly IReadOnlyList<DocReq> FileCompilationDocuments =
        Map.Values.SelectMany(x => x).ToList();

    /// <summary>
    /// Builds the panel checklist: every mandatory document (from the map), each marked
    /// present/missing against the supplied set of document-type codes already on the case.
    /// </summary>
    public static IReadOnlyList<DocumentRequirementStatus> StatusesFor(ISet<string> presentTypes)
    {
        return FileCompilationDocuments
            .GroupBy(r => r.DocumentType)
            .Select(g =>
            {
                var first = g.First();
                return new DocumentRequirementStatus(
                    first.DocumentType,
                    first.DisplayName,
                    Mandatory: true,
                    MandatoryAtCp: first.AtControlPoint,
                    Present: presentTypes.Contains(first.DocumentType));
            })
            .ToList();
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~DocumentRequirementsTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add Services/Workflow/Guards/DocumentRequirements.cs Tests/Services/Workflow/DocumentRequirementsTests.cs
git commit -m "feat(docs): shared document-requirements map + status helper"
```

---

## Task 4: DocumentEvidenceGuard + DI

**Files:**
- Create: `Services/Workflow/Guards/DocumentEvidenceGuard.cs`
- Test: `Tests/Services/Workflow/DocumentEvidenceGuardTests.cs`
- Modify: `Program.cs:145` (add registration after `LetterServiceConfirmedGuard`)

- [ ] **Step 1: Write the failing tests**

Create `Tests/Services/Workflow/DocumentEvidenceGuardTests.cs`:

```csharp
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
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~DocumentEvidenceGuardTests`
Expected: FAIL — `DocumentEvidenceGuard` does not exist.

- [ ] **Step 3: Implement the guard**

Create `Services/Workflow/Guards/DocumentEvidenceGuard.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Services.Workflow.Guards;

/// <summary>
/// Blocks leaving a control point until every MANDATORY document for that CP (per
/// DocumentRequirements.Map) has at least one non-"Infected" Document on the case.
/// Null/Pending/Clean virus statuses are acceptable (no AV scanner is wired yet).
/// </summary>
public class DocumentEvidenceGuard : ITransitionGuard
{
    private readonly ApplicationDBContext _db;
    public DocumentEvidenceGuard(ApplicationDBContext db) { _db = db; }

    public async Task<GuardResult> CheckAsync(GuardContext ctx)
    {
        foreach (var (cpPrefix, reqs) in DocumentRequirements.Map)
        {
            if (!Cp2SpatialInfoGuard.IsLeaving(ctx, cpPrefix)) continue;

            foreach (var req in reqs)
            {
                var present = await _db.Documents.AnyAsync(d =>
                    d.FileMasterId == ctx.FileMaster.FileMasterId
                    && d.DocumentType == req.DocumentType
                    && d.VirusScanStatus != "Infected");

                if (!present)
                    return GuardResult.Deny(
                        $"{req.DisplayName} must be uploaded before leaving this control point.");
            }
        }
        return GuardResult.Ok;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~DocumentEvidenceGuardTests`
Expected: PASS (6 tests).

- [ ] **Step 5: Register the guard in DI**

In `Program.cs`, immediately after line 145 (`...LetterServiceConfirmedGuard>();`), add:

```csharp
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.DocumentEvidenceGuard>();
```

- [ ] **Step 6: Build to confirm DI wiring compiles**

Run: `dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 7: Commit**

```bash
git add Services/Workflow/Guards/DocumentEvidenceGuard.cs Tests/Services/Workflow/DocumentEvidenceGuardTests.cs Program.cs
git commit -m "feat(docs): DocumentEvidenceGuard gates CP2/CP3 on mandatory documents"
```

---

## Task 5: Extend Cp11FileCompilationGuard to re-check documents

**Files:**
- Modify: `Services/Workflow/Guards/FlagGuards.cs` (the `Cp11FileCompilationGuard.CheckAsync` method)
- Test: `Tests/Services/Workflow/GuardTests.cs` (add to `Cp11FileCompilationGuardTests`)

- [ ] **Step 1: Write the failing test**

In `Tests/Services/Workflow/GuardTests.cs`, inside the `Cp11FileCompilationGuardTests` class, add a helper and two tests. First add this helper method to the class (next to `SeedFullCase`):

```csharp
    private static void AddCp11Docs(ApplicationDBContext db, Guid fileMasterId)
    {
        foreach (var type in new[] { "WARMSReport", "TitleDeedReport", "SGDiagram" })
        {
            db.Documents.Add(new Document
            {
                DocumentId = Guid.NewGuid(),
                FileMasterId = fileMasterId,
                DocumentType = type,
                FileName = "f.pdf",
                BlobPath = "x/f.pdf",
                VirusScanStatus = "Clean",
                SyncStatus = "NotSynced",
                UploadDate = DateTime.UtcNow
            });
        }
    }
```

Then add these tests:

```csharp
    [Fact]
    public async Task Cp11_DeniesWhenMandatoryDocumentsMissing()
    {
        using var db = NewDb();
        var (fm, _) = await SeedFullCase(db); // seeds data records but NOT documents
        var result = await new Cp11FileCompilationGuard(db).CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("report", result.Reason!); // e.g. "WARMS report" / "Title Deed report"
    }

    [Fact]
    public async Task Cp11_AllowsWhenAllDataAndDocumentsPresent()
    {
        using var db = NewDb();
        var (fm, _) = await SeedFullCase(db);
        AddCp11Docs(db, fm.FileMasterId);
        await db.SaveChangesAsync();
        var result = await new Cp11FileCompilationGuard(db).CheckAsync(LeavingCp11(fm));
        Assert.True(result.Allowed);
    }
```

**Note:** the pre-existing `Cp11_AllowsWhenAllNineItemsPresent` test (and the dam/sfra "Allows" tests) will now FAIL because they don't seed documents. Update each of those three "Allows…" tests to call `AddCp11Docs(db, fm.FileMasterId); await db.SaveChangesAsync();` after `SeedFullCase`/record setup and before the assertion. (The "Denies…" tests already expect denial, so they remain valid.)

- [ ] **Step 2: Run the tests to verify the new ones fail**

Run: `dotnet test --filter FullyQualifiedName~Cp11FileCompilationGuardTests`
Expected: `Cp11_AllowsWhenAllDataAndDocumentsPresent` FAILS (guard denies — doc check not implemented yet), `Cp11_DeniesWhenMandatoryDocumentsMissing` PASSES only once the doc check exists. (Some pre-existing "Allows" tests fail until updated per the note.)

- [ ] **Step 3: Implement the document re-check**

In `Services/Workflow/Guards/FlagGuards.cs`, add `using Microsoft.EntityFrameworkCore;` is already present. In `Cp11FileCompilationGuard.CheckAsync`, immediately before the final `return GuardResult.Ok;`, add:

```csharp
        foreach (var req in DocumentRequirements.FileCompilationDocuments)
        {
            var hasDoc = await _db.Documents.AnyAsync(d =>
                d.FileMasterId == ctx.FileMaster.FileMasterId
                && d.DocumentType == req.DocumentType
                && d.VirusScanStatus != "Infected");
            if (!hasDoc)
                return GuardResult.Deny($"{req.DisplayName} must be uploaded before file can be compiled.");
        }
```

(`DocumentRequirements` is in the same namespace `dwa_ver_val.Services.Workflow.Guards`, so no new using is needed.)

- [ ] **Step 4: Run the full guard test suite**

Run: `dotnet test --filter FullyQualifiedName~GuardTests`
Then: `dotnet test --filter FullyQualifiedName~Cp11FileCompilationGuardTests`
Expected: all PASS (including the three updated "Allows" tests).

- [ ] **Step 5: Commit**

```bash
git add Services/Workflow/Guards/FlagGuards.cs Tests/Services/Workflow/GuardTests.cs
git commit -m "feat(docs): CP11 compilation guard re-checks mandatory documents"
```

---

## Task 6: Internal DocumentController + policy

**Files:**
- Modify: `Services/Auth/DwsPolicies.cs`
- Create: `ViewModels/CaseDocumentUploadViewModel.cs`
- Create: `Controllers/DocumentController.cs`
- Test: `Tests/Controllers/DocumentControllerTests.cs`

- [ ] **Step 1: Add the `CanManageDocuments` policy**

In `Services/Auth/DwsPolicies.cs`, add a const after `CanRead`:

```csharp
    public const string CanManageDocuments = "CanManageDocuments";
```

and in `Configure`, after the `CanRead` policy:

```csharp
        options.AddPolicy(CanManageDocuments,
            p => p.RequireAuthenticatedUser().RequireRole(DwsRoles.AtLeastValidator));
```

- [ ] **Step 2: Create the upload view model**

Create `ViewModels/CaseDocumentUploadViewModel.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

public class CaseDocumentUploadViewModel
{
    public Guid FileMasterId { get; set; }

    [Required(ErrorMessage = "Please select a document type.")]
    [Display(Name = "Document Type")]
    public string DocumentType { get; set; } = "TitleDeedReport";

    [Display(Name = "Control Point (optional)")]
    public Guid? WorkflowStateId { get; set; }

    [Required(ErrorMessage = "Please select a file.")]
    public IFormFile? File { get; set; }
}
```

- [ ] **Step 3: Write the failing controller tests**

Create `Tests/Controllers/DocumentControllerTests.cs`:

```csharp
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
        // Scope claim so the WMA matches.
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
        var (fm, _) = SeedCase(db, Guid.NewGuid());            // case in WMA A
        var ctrl = BuildController(db, User(Guid.NewGuid(), DwsRoles.Validator));
        ((ClaimsIdentity)ctrl.User.Identity!).AddClaim(new Claim("wmaId", Guid.NewGuid().ToString())); // user in WMA B

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
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~DocumentControllerTests`
Expected: FAIL — `DocumentController` does not exist.

- [ ] **Step 5: Implement the controller**

Create `Controllers/DocumentController.cs`:

```csharp
using System.Security.Claims;
using dwa_ver_val.Services.Audit;
using dwa_ver_val.Services.Documents;
using dwa_ver_val.Services.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Controllers;

[Authorize(Policy = DwsPolicies.CanRead)]
public class DocumentController : Controller
{
    private static readonly HashSet<string> _allowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".jpg", ".jpeg", ".png", ".tiff" };
    private const long MaxBytes = 25 * 1024 * 1024;

    private readonly ApplicationDBContext _db;
    private readonly IScopedCaseQuery _scope;
    private readonly IFileStorage _storage;
    private readonly IAuditService _audit;

    public DocumentController(ApplicationDBContext db, IScopedCaseQuery scope, IFileStorage storage, IAuditService audit)
    {
        _db = db; _scope = scope; _storage = storage; _audit = audit;
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Not authenticated."));

    private async Task<FileMaster?> ScopedCaseAsync(Guid fileMasterId)
    {
        var fm = await _db.FileMasters.Include(f => f.Property)
            .FirstOrDefaultAsync(f => f.FileMasterId == fileMasterId);
        if (fm is null) return null;
        return _scope.IsInScope(fm, User) ? fm : null;
    }

    [HttpGet]
    [Authorize(Policy = DwsPolicies.CanCapture)]
    public async Task<IActionResult> Upload(Guid fileMasterId)
    {
        if (await ScopedCaseAsync(fileMasterId) is null) return Forbid();
        return View(new CaseDocumentUploadViewModel { FileMasterId = fileMasterId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCapture)]
    [RequestSizeLimit(MaxBytes)]
    public async Task<IActionResult> Upload(CaseDocumentUploadViewModel model, CancellationToken ct)
    {
        var fm = await ScopedCaseAsync(model.FileMasterId);
        if (fm is null) return Forbid();

        if (!DocumentTypes.IsKnown(model.DocumentType))
            ModelState.AddModelError(nameof(model.DocumentType), "Unknown document type.");

        if (model.File is null || model.File.Length <= 0)
            ModelState.AddModelError(nameof(model.File), "Please select a file.");
        else
        {
            var ext = Path.GetExtension(model.File.FileName);
            if (!_allowedExtensions.Contains(ext))
                ModelState.AddModelError(nameof(model.File), "Only PDF, JPG, PNG, and TIFF files are accepted.");
            if (model.File.Length > MaxBytes)
                ModelState.AddModelError(nameof(model.File), "File exceeds the 25 MB limit.");
        }

        if (!ModelState.IsValid) return View(model);

        var uid = CurrentUserId();
        using var stream = model.File!.OpenReadStream();
        var stored = await _storage.SaveAsync(
            stream, model.File.ContentType ?? "application/octet-stream", model.File.FileName, ct);

        var doc = new Document
        {
            DocumentId = Guid.NewGuid(),
            FileMasterId = model.FileMasterId,
            WorkflowStateId = model.WorkflowStateId,
            DocumentType = model.DocumentType,
            FileName = model.File.FileName,
            BlobPath = stored.RelativePath,
            ContentType = stored.ContentType,
            FileSizeBytes = stored.SizeBytes,
            UploadedByUserId = uid,
            UploadDate = DateTime.UtcNow,
            VirusScanStatus = "Pending",
            SyncStatus = "NotSynced",
            DocumentHash = stored.Sha256Hex
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEvent(
            EntityType: "Document",
            EntityId: doc.DocumentId.ToString(),
            Action: "DocumentUploaded",
            UserId: uid,
            UserDisplayName: User.Identity?.Name,
            ToValue: $"{model.DocumentType}:{model.File.FileName}",
            IPAddress: HttpContext.Connection.RemoteIpAddress?.ToString()));

        return RedirectToAction("Details", "FileMaster", new { id = model.FileMasterId });
    }

    [HttpGet]
    public async Task<IActionResult> Download(Guid documentId, CancellationToken ct)
    {
        var doc = await _db.Documents.Include(d => d.FileMaster).ThenInclude(f => f!.Property)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId, ct);
        if (doc?.FileMaster is null || !_scope.IsInScope(doc.FileMaster, User)) return Forbid();

        var stream = await _storage.OpenReadAsync(doc.BlobPath, ct);
        return File(stream, doc.ContentType ?? "application/octet-stream", doc.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanManageDocuments)]
    public async Task<IActionResult> Delete(Guid documentId, CancellationToken ct)
    {
        var doc = await _db.Documents.Include(d => d.FileMaster).ThenInclude(f => f!.Property)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId, ct);
        if (doc?.FileMaster is null || !_scope.IsInScope(doc.FileMaster, User)) return Forbid();

        var fileMasterId = doc.FileMasterId;
        var snapshot = $"{doc.DocumentType}:{doc.FileName}";
        var blobPath = doc.BlobPath;

        await _storage.DeleteAsync(blobPath, ct);
        _db.Documents.Remove(doc);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEvent(
            EntityType: "Document",
            EntityId: documentId.ToString(),
            Action: "Delete",
            UserId: CurrentUserId(),
            UserDisplayName: User.Identity?.Name,
            FromValue: snapshot,
            IPAddress: HttpContext.Connection.RemoteIpAddress?.ToString()));

        return RedirectToAction("Details", "FileMaster", new { id = fileMasterId });
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~DocumentControllerTests`
Expected: PASS (5 tests).

- [ ] **Step 7: Commit**

```bash
git add Services/Auth/DwsPolicies.cs ViewModels/CaseDocumentUploadViewModel.cs Controllers/DocumentController.cs Tests/Controllers/DocumentControllerTests.cs
git commit -m "feat(docs): internal DocumentController with scope, RBAC, audit"
```

---

## Task 7: Documents panel on the FileMaster detail view

**Files:**
- Modify: `ViewModels/FileMasterDetailsViewModel.cs`
- Modify: `Controllers/FileMasterController.cs` (the `Details` action, around line 143-200)
- Create: `Views/Document/Upload.cshtml`
- Create: `Views/FileMaster/_DocumentsPanel.cshtml`
- Modify: `Views/FileMaster/Details.cshtml` (add the partial near line 37)

- [ ] **Step 1: Extend the details view model**

In `ViewModels/FileMasterDetailsViewModel.cs`, add these properties after `public List<AuditLog> AuditTrail { get; set; } = new();`:

```csharp
    // Internal staff documents attached to this case + the Appendix A requirement checklist.
    public List<Document> CaseDocuments { get; set; } = new();
    public IReadOnlyList<dwa_ver_val.Services.Workflow.Guards.DocumentRequirementStatus> DocumentRequirementStatuses { get; set; }
        = new List<dwa_ver_val.Services.Workflow.Guards.DocumentRequirementStatus>();
```

- [ ] **Step 2: Populate them in the Details action**

In `Controllers/FileMasterController.cs`, inside `Details(Guid id)`, after `vm.Letters = ...` (line 163) and before the method returns the view, add:

```csharp
        vm.CaseDocuments = await _context.Documents
            .Where(d => d.FileMasterId == id && d.UploadedByUserId != null)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();

        var presentTypes = vm.CaseDocuments.Select(d => d.DocumentType).ToHashSet(StringComparer.Ordinal);
        vm.DocumentRequirementStatuses =
            dwa_ver_val.Services.Workflow.Guards.DocumentRequirements.StatusesFor(presentTypes);
```

(If `Microsoft.EntityFrameworkCore` is not already imported in this file, add `using Microsoft.EntityFrameworkCore;` at the top. `_context` is the existing `ApplicationDBContext` field used elsewhere in the action.)

- [ ] **Step 3: Create the upload view**

Create `Views/Document/Upload.cshtml`:

```cshtml
@model CaseDocumentUploadViewModel
@using dwa_ver_val.Services.Documents
@{
    ViewData["Title"] = "Upload Document";
}

<div class="card" style="max-width: 640px;">
    <div class="form-section-title">Upload Case Document</div>

    <form asp-action="Upload" asp-controller="Document" method="post" enctype="multipart/form-data">
        @Html.AntiForgeryToken()
        <input type="hidden" asp-for="FileMasterId" />

        <div asp-validation-summary="ModelOnly" class="text-danger"></div>

        <div class="form-group">
            <label asp-for="DocumentType"></label>
            <select asp-for="DocumentType" class="form-control">
                @foreach (var kvp in DocumentTypes.All)
                {
                    <option value="@kvp.Key">@kvp.Value.Display@(kvp.Value.IsAppendixA ? " (Appendix A)" : "")</option>
                }
            </select>
            <span asp-validation-for="DocumentType" class="text-danger"></span>
        </div>

        <div class="form-group">
            <label asp-for="File">File (PDF, JPG, PNG, TIFF — max 25 MB)</label>
            <input asp-for="File" type="file" class="form-control" accept=".pdf,.jpg,.jpeg,.png,.tiff" />
            <span asp-validation-for="File" class="text-danger"></span>
        </div>

        <div style="margin-top: 16px;">
            <button type="submit" class="btn btn-primary">Upload</button>
            <a asp-action="Details" asp-controller="FileMaster" asp-route-id="@Model.FileMasterId" class="btn btn-secondary">Cancel</a>
        </div>
    </form>
</div>
```

- [ ] **Step 4: Create the documents panel partial**

Create `Views/FileMaster/_DocumentsPanel.cshtml`:

```cshtml
@model FileMasterDetailsViewModel
@using Microsoft.AspNetCore.Authorization
@inject IAuthorizationService Auth
@{
    var canCapture = (await Auth.AuthorizeAsync(User, DwsPolicies.CanCapture)).Succeeded;
    var canManage  = (await Auth.AuthorizeAsync(User, DwsPolicies.CanManageDocuments)).Succeeded;
}

<div class="card" style="max-width: 900px; margin-top: 16px;">
    <div class="form-section-title">Case Documents (Appendix A Evidence)</div>

    <table class="table">
        <thead>
            <tr><th>Required Document</th><th>Mandatory at</th><th>Status</th></tr>
        </thead>
        <tbody>
        @foreach (var s in Model.DocumentRequirementStatuses)
        {
            <tr>
                <td>@s.DisplayName</td>
                <td>@s.MandatoryAtCp</td>
                <td>
                    @if (s.Present)
                    {
                        <span style="color: var(--dws-green, #2e7d32); font-weight: 600;">&#10003; Present</span>
                    }
                    else
                    {
                        <span style="color: var(--dws-red, #c62828); font-weight: 600;">&#10007; Missing</span>
                    }
                </td>
            </tr>
        }
        </tbody>
    </table>

    @if (Model.CaseDocuments.Any())
    {
        <table class="table" style="margin-top: 12px;">
            <thead>
                <tr><th>File</th><th>Type</th><th>Uploaded</th><th></th></tr>
            </thead>
            <tbody>
            @foreach (var d in Model.CaseDocuments)
            {
                <tr>
                    <td>
                        <a asp-action="Download" asp-controller="Document" asp-route-documentId="@d.DocumentId">@d.FileName</a>
                    </td>
                    <td>@dwa_ver_val.Services.Documents.DocumentTypes.Display(d.DocumentType)</td>
                    <td>@d.UploadDate.ToString("yyyy-MM-dd")</td>
                    <td>
                        @if (canManage)
                        {
                            <form asp-action="Delete" asp-controller="Document" method="post" style="display:inline;"
                                  onsubmit="return confirm('Delete this document?');">
                                @Html.AntiForgeryToken()
                                <input type="hidden" name="documentId" value="@d.DocumentId" />
                                <button type="submit" class="btn btn-link" style="color: var(--dws-red, #c62828);">Delete</button>
                            </form>
                        }
                    </td>
                </tr>
            }
            </tbody>
        </table>
    }
    else
    {
        <p style="color:#666;">No documents uploaded yet.</p>
    }

    @if (canCapture)
    {
        <a asp-action="Upload" asp-controller="Document" asp-route-fileMasterId="@Model.FileMaster.FileMasterId"
           class="btn btn-primary" style="margin-top: 8px;">Upload Document</a>
    }
</div>
```

- [ ] **Step 5: Render the panel on the details page**

In `Views/FileMaster/Details.cshtml`, after the `_PortalInboxPanel` partial line (line 37), add:

```cshtml
@await Html.PartialAsync("_DocumentsPanel", Model)
```

- [ ] **Step 6: Build and run the app to verify the panel renders**

Run: `dotnet build`
Expected: `Build succeeded`.
Then run `dotnet run`, log in as a Validator, open any case's Details page, and confirm the "Case Documents (Appendix A Evidence)" panel shows the three required rows as "Missing" and an "Upload Document" button. Upload a PDF as `Title Deed Report` and confirm the row flips to "Present" and the file lists with a working download link.

- [ ] **Step 7: Commit**

```bash
git add ViewModels/FileMasterDetailsViewModel.cs Controllers/FileMasterController.cs Views/Document/Upload.cshtml Views/FileMaster/_DocumentsPanel.cshtml Views/FileMaster/Details.cshtml
git commit -m "feat(docs): documents panel + upload view on FileMaster detail"
```

---

## Task 8: Align the external portal uploader to the shared vocabulary

**Files:**
- Modify: `Areas/ExternalPortal/ViewModels/DocumentUploadViewModel.cs`
- Modify: `Areas/ExternalPortal/Views/Document/Upload.cshtml`

- [ ] **Step 1: Default the external VM to a vocabulary code**

In `Areas/ExternalPortal/ViewModels/DocumentUploadViewModel.cs`, change the default:

```csharp
    public string DocumentType { get; set; } = DocumentTypes.TitleDeed;
```

and add `using dwa_ver_val.Services.Documents;` at the top.

- [ ] **Step 2: Drive the external dropdown from the vocabulary**

In `Areas/ExternalPortal/Views/Document/Upload.cshtml`, add `@using dwa_ver_val.Services.Documents` near the top and replace the document-type `<select>`'s hard-coded `<option>` list with:

```cshtml
            @foreach (var kvp in DocumentTypes.All)
            {
                <option value="@kvp.Key">@kvp.Value.Display</option>
            }
```

(If the existing view has no `<select>` and uses a free-text input, replace that input with a `<select asp-for="DocumentType" class="form-control">` wrapping the loop above.)

- [ ] **Step 3: Build + run external-portal document tests**

Run: `dotnet build`
Then: `dotnet test --filter FullyQualifiedName~DocumentControllerTests`
Expected: `Build succeeded`; both internal and external `DocumentControllerTests` PASS. (Note: the external test namespace is `dwa_ver_val.Tests.Areas.ExternalPortal`; the filter matches both — confirm all pass.)

- [ ] **Step 4: Commit**

```bash
git add Areas/ExternalPortal/ViewModels/DocumentUploadViewModel.cs Areas/ExternalPortal/Views/Document/Upload.cshtml
git commit -m "refactor(docs): external uploader uses shared document vocabulary"
```

---

## Task 9: Full verification

- [ ] **Step 1: Build the whole solution**

Run: `dotnet build`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 2: Run the entire test suite**

Run: `dotnet test`
Expected: all tests pass (previously ~339 plus the new DocumentTypes/DocumentRequirements/DocumentEvidenceGuard/DocumentController tests and the updated CP11 tests).

- [ ] **Step 3: Apply the migration to a dev database**

Run: `dotnet ef database update`
Expected: the `AddDocumentWorkflowAnnotationAndSync` migration applies cleanly (SQL Server in Docker must be running).

- [ ] **Step 4: Manual end-to-end check**

Run `dotnet run`. As a Validator on a case currently at `CP2_SpatialInfo`:
1. Confirm advancing the workflow is **blocked** with a reason naming the Title Deed report / SG Diagram (surfaced via the existing workflow `BlockingReasons`).
2. Upload `Title Deed Report` and `SG Diagram`; confirm the panel rows flip to Present and the case can now advance past CP2.
3. Repeat for `WARMS Report` at `CP3_WARMSEvaluation`.
4. As a `Capturer`, confirm Upload is available but Delete is not. As `ReadOnly`, confirm neither is available but download works.

- [ ] **Step 5: Final commit (if any view/polish tweaks were made)**

```bash
git add -A
git commit -m "test(docs): full document-upload evidence gating verified"
```

---

## Self-Review Notes (author)

- **Spec coverage:** §1 storage (Task 1 fields, reuse IFileStorage), §2 model+vocabulary (Tasks 1-2), §3 guard+map (Tasks 3-5), §4 controller+RBAC+audit (Task 6), §5 panel UI (Task 7), §6 wider checks + external alignment (Task 8), §9 tests throughout, §11 edge cases (infected-doc test in Task 4, null-virus-status test in Task 4, scope-leak test in Task 6). The S33(2) edge case needs no code — CP2/CP3 precede the skip and fire normally; documented, no task required.
- **Deviations:** prefix-keyed map (vs full state names) and no notification on internal upload — both stated at the top with rationale.
- **Type consistency:** `DocReq(DocumentType, DisplayName, AtControlPoint)`, `DocumentRequirementStatus(DocumentType, DisplayName, Mandatory, MandatoryAtCp, Present)`, `DocumentTypes.IsKnown/Display/All`, and `DocumentRequirements.Map/FileCompilationDocuments/StatusesFor` are used identically across Tasks 3-7.
- **Deferred (per spec §10, not in this plan):** eWULAAS push, AV scanning, Azure Blob impl, admin-configurable rules, Mark-N/A waiver.
