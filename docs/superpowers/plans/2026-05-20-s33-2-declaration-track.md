# S33(2) Kader Asmal Declaration Track — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the S33(2) "Kader Asmal Declaration" assessment track so that when a FileMaster is assigned `AssessmentTrack = "S33_2_Declaration"`, the system: (a) captures the required S33(2) data (irrigation board, scheduled area, rates-paid confirmation), (b) routes the workflow through the mandatory CP1–CP4 steps then skips to a ready-for-declaration holding state, and (c) allows issuance of the S33(2) PDF declaration letter with full idempotency and legal record integrity.

**Architecture:** Four targeted changes to an already-scaffolded feature: (1) insert an intermediate non-terminal workflow state `S33_2_ReadyForDeclaration` so the letter can be issued before the case closes; (2) add three S33(2)-specific data columns to `FileMaster`; (3) wire the letter issuance action through `LetterActionMap`/`OneTimeLetterCodes` with a rates-paid guard; (4) surface the data fields on the Edit and Details views.

**Tech Stack:** ASP.NET Core 10 MVC, EF Core 10 / SQL Server 2022 (InMemory for tests), xUnit, Moq, QuestPDF (existing).

---

## Why this matters — current gap

The workflow already skips CP5–CP9 for S33(2) cases (in `WorkflowService.ResolveNextStateAsync`) and lands directly on `S33_2_DeclarationIssued`. But:

- `S33_2_DeclarationIssued` is **not terminal** — only `Closed` is — so the state name is misleading and the transition from it is just "Close Case" with no letter ever issued.
- There is **no `IssueS33_2` action** in `LetterActionMap`, no `S33_2_Decl` in `OneTimeLetterCodes`, and no button in `_LettersPanel.cshtml`.
- **No S33(2) data fields** on `FileMaster`; CLAUDE.md requires tracking "irrigation board membership and rates-paid-up-to date".
- `LetterContext.IrrigationBoardName` is populated only if passed in from a form; for S33(2) it must come from the case record.

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| **Modify** | `Services/SeedDataService.cs` | Insert `S33_2_ReadyForDeclaration` state; update DisplayOrders for 33→36 |
| **Modify** | `Services/WorkflowService.cs` | Rename skip-target constant; update its value to `S33_2_ReadyForDeclaration` |
| **Modify** | `Tests/Services/Workflow/AssessmentTrackTests.cs` | Update assertion + seed for new intermediate state |
| **Modify** | `Models/FileMaster.cs` | Add `S33_2_IrrigationBoardId`, `S33_2_RatesPaidConfirmed`, `S33_2_ScheduledAreaName` + navigation |
| **Modify** | `DatabaseContexts/ApplicationDBContext.cs` | Configure FK (SetNull), bool default, string MaxLength |
| **Create** | `Migrations/<timestamp>_S33_2_DeclarationFields.cs` | EF Core migration for three new columns |
| **Modify** | `Controllers/FileMasterController.cs` | `LetterActionMap` + `OneTimeLetterCodes` + rates-paid guard + board auto-load; Edit GET/POST |
| **Modify** | `ViewModels/FileMasterDetailsViewModel.cs` | `AvailableLetterActions` for `S33_2_ReadyForDeclaration` |
| **Modify** | `Views/FileMaster/_LettersPanel.cshtml` | Add `IssueS33_2` label/button switch case |
| **Modify** | `Views/FileMaster/Details.cshtml` | S33(2) info summary block |
| **Modify** | `Views/FileMaster/Edit.cshtml` | Conditional S33(2) data section |
| **Modify** | `Tests/Controllers/FileMasterControllerLetterTests.cs` | Three new tests + update OneTimeLetter InlineData |

---

## Task 1 — Intermediate workflow state `S33_2_ReadyForDeclaration`

**Files:**
- **Modify:** `Services/SeedDataService.cs`
- **Modify:** `Services/WorkflowService.cs`
- **Modify:** `Tests/Services/Workflow/AssessmentTrackTests.cs`

**Design notes:**
- `S33_2_ReadyForDeclaration` (DisplayOrder = 33, non-terminal) is inserted before the existing 33–35 states, which shift to 34, 35, 36.
- The SeedDataService drift-correction loop already handles updating DisplayOrder for states that already exist in the DB — no special migration step needed; the seeder corrects on first startup.
- `WorkflowService.S33_2_TerminalStateName` is misleading since the skip target is NOT terminal; rename it to `S33_2_SkipTargetStateName`.
- Existing `AssessmentTrackTests` seeds `S33_2_DeclarationIssued` with `IsTerminal = true` — this contradicts production seeding (`false`). Fix the test seed to `IsTerminal = false` and update the assertion to expect `S33_2_ReadyForDeclaration`.

- [ ] **Step 1.1 — Write the failing test (updated assertion)**

Open `Tests/Services/Workflow/AssessmentTrackTests.cs`. Replace the two seed states in `AdvanceAsync_OnS33_2Track_SkipsCp5_LandsOnDeclarationIssued` with three:

```csharp
var cp4  = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP4_AdditionalInfo",          DisplayOrder = 10,  Phase = "Validation",    IsTerminal = false };
var ready = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S33_2_ReadyForDeclaration",  DisplayOrder = 33, Phase = "Verification",   IsTerminal = false };
var decl  = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S33_2_DeclarationIssued",   DisplayOrder = 34, Phase = "Verification",   IsTerminal = false };
db.WorkflowStates.AddRange(cp4, ready, decl);
```

Change the assertion:
```csharp
Assert.Equal(ready.WorkflowStateId, result.CurrentWorkflowStateId);
Assert.Equal("Active", result.Status);   // non-terminal → still Active
```

Also update `AdvanceAsync_OnS35Track_FollowsDefaultOrder_DoesNotSkip`: replace `cp4` StateName from `"CP4_Additional"` to `"CP4_AdditionalInfo"` to match production seeding (prevents a subtle name-drift bug from invalidating the guard's `StartsWith("CP5", ...)` check — not the test itself, but good hygiene).

- [ ] **Step 1.2 — Run tests to confirm failure**

```bash
dotnet test --filter "FullyQualifiedName~AssessmentTrackTests" --no-build 2>&1 | tail -10
```
Expected: `FAIL` — assertion fails because skip still lands on `S33_2_DeclarationIssued`.

- [ ] **Step 1.3 — Update `WorkflowService.cs`**

Change:
```csharp
private const string S33_2_TerminalStateName = "S33_2_DeclarationIssued";
```
To:
```csharp
private const string S33_2_SkipTargetStateName = "S33_2_ReadyForDeclaration";
```

Update the one usage inside `ResolveNextStateAsync`:
```csharp
var declaration = await _context.WorkflowStates.SingleOrDefaultAsync(s => s.StateName == S33_2_SkipTargetStateName);
```

- [ ] **Step 1.4 — Update `SeedDataService.cs`**

In `SeedWorkflowStatesAsync`, find the declaration states block and replace:
```csharp
// Before (two rows, orders 33–34):
("S33_2_DeclarationIssued",      "Verification", 33, false),
("S33_3_DeclarationIssued",      "Verification", 34, false),

// Terminal states
("Closed",                       "Verification", 35, true),
```
With:
```csharp
// After (three rows, orders 33–35; Closed shifts to 36):
("S33_2_ReadyForDeclaration",    "Verification", 33, false),
("S33_2_DeclarationIssued",      "Verification", 34, false),
("S33_3_DeclarationIssued",      "Verification", 35, false),

// Terminal states
("Closed",                       "Verification", 36, true),
```

- [ ] **Step 1.5 — Run tests to confirm green**

```bash
dotnet test --filter "FullyQualifiedName~AssessmentTrackTests" --no-build 2>&1 | tail -10
```
Expected: `PASS` (2/2).

- [ ] **Step 1.6 — Run full suite to confirm no regressions**

```bash
dotnet test --no-build 2>&1 | tail -5
```
Expected: same pass count as before (292), 0 failures.

- [ ] **Step 1.7 — Commit**

```bash
git add Services/SeedDataService.cs Services/WorkflowService.cs Tests/Services/Workflow/AssessmentTrackTests.cs
git commit -m "feat(s33-2): add S33_2_ReadyForDeclaration intermediate workflow state; workflow skip now lands before letter issuance"
```

---

## Task 2 — S33(2) data fields on `FileMaster`

**Files:**
- **Modify:** `Models/FileMaster.cs`
- **Modify:** `DatabaseContexts/ApplicationDBContext.cs`
- **Create:** migration

**Design notes:**
- Three new nullable properties: `S33_2_IrrigationBoardId` (Guid?), `S33_2_RatesPaidConfirmed` (bool, default false), `S33_2_ScheduledAreaName` (string?).
- Navigation property `IrrigationBoard? S33_2_IrrigationBoard` (no data annotation — configured in `OnModelCreating`).
- FK: `SetNull` on delete (if an IrrigationBoard record is deleted, the FileMaster retains its other data).
- `S33_2_RatesPaidConfirmed` must default to `false` in EF: `.HasDefaultValue(false)`.
- The migration body is non-empty (real schema change — three new columns).
- The existing test `FileMaster_AssessmentTrack_And_CatchmentArea` already rounds-trips `AssessmentTrack = "S33_2_Declaration"`. Extend it (or add a new `[Fact]`) to also round-trip the three new fields.

- [ ] **Step 2.1 — Write the failing test**

In `Tests/Models/EntityRelationshipTests.cs`, add after the existing `FileMaster_AssessmentTrack_And_CatchmentArea` test:

```csharp
[Fact]
public async Task FileMaster_S33_2_Fields_RoundTrip()
{
    using var db = TestDbContextFactory.Create();

    var board = new IrrigationBoard
    {
        IrrigationBoardId = Guid.NewGuid(),
        IrrigationBoardName = "Blyde River Irrigation Board"
    };
    var property = new Property
    {
        PropertyId = Guid.NewGuid(),
        SGCode = "T0AB00000000AB",
        PropertyReferenceNumber = "T0AB00000000AB",
        FarmName = "Testfarm",
        FarmNumber = 99,
        FarmPortion = "0",
        RegistrationDivision = "AB",
        SurveyorGeneralCode = "T0AB00000000AB"
    };
    db.IrrigationBoards.Add(board);
    db.Properties.Add(property);

    var fm = new FileMaster
    {
        FileMasterId = Guid.NewGuid(),
        PropertyId = property.PropertyId,
        RegistrationNumber = "S33-TEST-001",
        SurveyorGeneralCode = "T0AB00000000AB",
        PrimaryCatchment = "A",
        QuaternaryCatchment = "A21A",
        FarmName = "Testfarm",
        FarmNumber = 99,
        FarmPortion = "0",
        RegistrationDivision = "AB",
        AssessmentTrack = "S33_2_Declaration",
        S33_2_IrrigationBoardId = board.IrrigationBoardId,
        S33_2_RatesPaidConfirmed = true,
        S33_2_ScheduledAreaName = "Scheme A"
    };
    db.FileMasters.Add(fm);
    await db.SaveChangesAsync();

    var retrieved = await db.FileMasters
        .Include(f => f.S33_2_IrrigationBoard)
        .SingleAsync(f => f.FileMasterId == fm.FileMasterId);

    Assert.Equal(board.IrrigationBoardId, retrieved.S33_2_IrrigationBoardId);
    Assert.True(retrieved.S33_2_RatesPaidConfirmed);
    Assert.Equal("Scheme A", retrieved.S33_2_ScheduledAreaName);
    Assert.Equal("Blyde River Irrigation Board", retrieved.S33_2_IrrigationBoard!.IrrigationBoardName);
}
```

- [ ] **Step 2.2 — Run to confirm failure**

```bash
dotnet test --filter "FullyQualifiedName~FileMaster_S33_2_Fields_RoundTrip" --no-build 2>&1 | tail -10
```
Expected: compile error — `S33_2_IrrigationBoardId` etc. do not exist yet.

- [ ] **Step 2.3 — Add properties to `Models/FileMaster.cs`**

Insert after the `AssessmentTrack` line:

```csharp
// S33(2) Kader Asmal Declaration fields — only populated on S33_2_Declaration track.
// CLAUDE.md: "System must track irrigation board membership and rates-paid-up-to date."
public Guid? S33_2_IrrigationBoardId { get; set; }
public IrrigationBoard? S33_2_IrrigationBoard { get; set; }
public bool S33_2_RatesPaidConfirmed { get; set; }
public string? S33_2_ScheduledAreaName { get; set; }
```

- [ ] **Step 2.4 — Configure FK in `DatabaseContexts/ApplicationDBContext.cs`**

After the `IrrigationBoard` HasKey line (line ~154), add:

```csharp
// FileMaster → IrrigationBoard (S33(2) scheduled area membership)
modelBuilder.Entity<FileMaster>()
    .HasOne(fm => fm.S33_2_IrrigationBoard)
    .WithMany()
    .HasForeignKey(fm => fm.S33_2_IrrigationBoardId)
    .OnDelete(DeleteBehavior.SetNull);

modelBuilder.Entity<FileMaster>()
    .Property(fm => fm.S33_2_RatesPaidConfirmed)
    .HasDefaultValue(false);

modelBuilder.Entity<FileMaster>()
    .Property(fm => fm.S33_2_ScheduledAreaName)
    .HasMaxLength(200);
```

- [ ] **Step 2.5 — Create migration**

```bash
dotnet ef migrations add S33_2_DeclarationFields
```

Verify the generated migration has three `AddColumn` calls (non-empty `Up`):
- `S33_2_IrrigationBoardId` (uniqueidentifier, nullable)
- `S33_2_RatesPaidConfirmed` (bit, defaultValue: false)
- `S33_2_ScheduledAreaName` (nvarchar(200), nullable)

And a FK `FK_FileMasters_IrrigationBoards_S33_2_IrrigationBoardId` with `onDelete: ReferentialAction.SetNull`.

- [ ] **Step 2.6 — Apply migration to dev database**

```bash
dotnet ef database update
```

- [ ] **Step 2.7 — Run failing test to confirm green**

```bash
dotnet test --filter "FullyQualifiedName~FileMaster_S33_2_Fields_RoundTrip" --no-build 2>&1 | tail -10
```
Expected: PASS.

- [ ] **Step 2.8 — Full suite**

```bash
dotnet test --no-build 2>&1 | tail -5
```
Expected: 293 passed (292 + 1 new), 0 failures.

- [ ] **Step 2.9 — Commit**

```bash
git add Models/FileMaster.cs DatabaseContexts/ApplicationDBContext.cs Migrations/ Tests/Models/EntityRelationshipTests.cs
git commit -m "feat(s33-2): add S33_2_IrrigationBoardId, S33_2_RatesPaidConfirmed, S33_2_ScheduledAreaName to FileMaster"
```

---

## Task 3 — Letter issuance wiring

**Files:**
- **Modify:** `Controllers/FileMasterController.cs`
- **Modify:** `ViewModels/FileMasterDetailsViewModel.cs`
- **Modify:** `Views/FileMaster/_LettersPanel.cshtml`
- **Modify:** `Tests/Controllers/FileMasterControllerLetterTests.cs`

**Design notes:**

**`LetterActionMap` addition:**
```csharp
["IssueS33_2"] = ("S33_2_Decl", "S33_2_DeclarationIssued"),
```

**`OneTimeLetterCodes` addition:**  
`"S33_2_Decl"` — declaration may only be issued once; re-issuance would undermine the legal record.

**Guard (in `IssueLetter`, before calling `_letters.IssueAsync`):**  
When `map.LetterCode == "S33_2_Decl"`:
1. Load `caseFm` (already loaded earlier in the action via `FileMasters.Include(...)`)
2. If `caseFm.S33_2_RatesPaidConfirmed == false` → `TempData["Error"] = "S33(2) declaration cannot be issued until irrigation board rates paid up to 30 September 1998 are confirmed on the case record."` → redirect.

**IrrigationBoardName auto-population:**  
When `map.LetterCode == "S33_2_Decl"`, load `caseFm.S33_2_IrrigationBoard` (include it in the query or do a targeted load) and pass `IrrigationBoardName: caseFm.S33_2_IrrigationBoard?.IrrigationBoardName` to `IssueLetterRequest`.

**LawfulVolumeM3:**  
Pass `caseFm.Entitlement?.AuthorisedVolume` when `letterCode == "S33_2_Decl"` (the template renders a blue summary box if non-null).

**`AvailableLetterActions` for `S33_2_ReadyForDeclaration`:**
```csharp
"S33_2_ReadyForDeclaration"  => new() { "IssueS33_2" },
```

**`_LettersPanel` switch case:**
```csharp
"IssueS33_2" => ("Issue S33(2) Kader Asmal Declaration", "btn btn-primary"),
```

**Tests to write:**
1. `IssueLetter_S33_2Decl_BlockedWhenRatesNotConfirmed` — seeds `S33_2_RatesPaidConfirmed = false`; expects redirect with TempData error, no `LetterIssuance` row.
2. `IssueLetter_S33_2Decl_SucceedsAndTransitionsToDeclarationIssued` — seeds `S33_2_RatesPaidConfirmed = true`, IrrigationBoard; expects `LetterIssuance` row + `S33_2_DeclarationIssued` workflow state.
3. Update the existing `OneTimeLetter_WhenAlreadyResolved_GuardDetectsExistingIssuance` Theory — add `[InlineData("S33_2_Decl")]`.

**Edge case — query in `IssueLetter`:** The existing action loads `caseFm` with `Include(f => f.OrgUnit)` but not `Include(f => f.S33_2_IrrigationBoard)`. The simplest fix: after loading `caseFm`, do a targeted load:
```csharp
if (string.Equals(map.LetterCode, "S33_2_Decl", StringComparison.OrdinalIgnoreCase))
    await _context.Entry(caseFm).Reference(f => f.S33_2_IrrigationBoard).LoadAsync(ct);
```

- [ ] **Step 3.1 — Write the three failing tests**

In `Tests/Controllers/FileMasterControllerLetterTests.cs`, add:

```csharp
[Fact]
public async Task IssueLetter_S33_2Decl_BlockedWhenRatesNotConfirmed()
{
    var (sut, db) = BuildController(db =>
    {
        var board = new IrrigationBoard { IrrigationBoardId = Guid.NewGuid(), IrrigationBoardName = "Test Board" };
        db.IrrigationBoards.Add(board);
        var fm = FileMasterBuilder.AtState(db, "S33_2_ReadyForDeclaration");
        fm.AssessmentTrack = "S33_2_Declaration";
        fm.S33_2_IrrigationBoardId = board.IrrigationBoardId;
        fm.S33_2_RatesPaidConfirmed = false;   // ← not confirmed
    });

    var result = await sut.IssueLetter(
        _fileMasterId, "IssueS33_2", "Water User A", "RegisteredPost",
        DateTime.Today);

    var redirect = Assert.IsType<RedirectToActionResult>(result);
    Assert.Equal("Details", redirect.ActionName);
    Assert.True(sut.TempData.ContainsKey("Error"));
    Assert.Empty(db.LetterIssuances.ToList());
}

[Fact]
public async Task IssueLetter_S33_2Decl_SucceedsAndTransitionsToDeclarationIssued()
{
    var (sut, db) = BuildController(db =>
    {
        var board = new IrrigationBoard { IrrigationBoardId = Guid.NewGuid(), IrrigationBoardName = "Blyde River Board" };
        db.IrrigationBoards.Add(board);
        var fm = FileMasterBuilder.AtState(db, "S33_2_ReadyForDeclaration");
        fm.AssessmentTrack = "S33_2_Declaration";
        fm.S33_2_IrrigationBoardId = board.IrrigationBoardId;
        fm.S33_2_RatesPaidConfirmed = true;   // ← confirmed
        // Seed S33_2_DeclarationIssued state for the transition target
        db.WorkflowStates.Add(new WorkflowState
        {
            WorkflowStateId = Guid.NewGuid(),
            StateName = "S33_2_DeclarationIssued",
            DisplayOrder = 34,
            Phase = "Verification",
            IsTerminal = false
        });
    });

    var result = await sut.IssueLetter(
        _fileMasterId, "IssueS33_2", "Water User A", "RegisteredPost",
        DateTime.Today);

    Assert.IsType<RedirectToActionResult>(result);
    var issuance = db.LetterIssuances.Single();
    Assert.Equal("S33_2_Decl", db.LetterTypes.Single(t => t.LetterTypeId == issuance.LetterTypeId).LetterName);

    var instance = db.WorkflowInstances.Single();
    var state = db.WorkflowStates.Single(s => s.WorkflowStateId == instance.CurrentWorkflowStateId);
    Assert.Equal("S33_2_DeclarationIssued", state.StateName);
}
```

Add `"S33_2_Decl"` as a new `[InlineData]` to `OneTimeLetter_WhenAlreadyResolved_GuardDetectsExistingIssuance`:
```csharp
[InlineData("S33_2_Decl")]
```
(Confirm the existing `InlineData` list has 6 entries; this becomes the 7th.)

- [ ] **Step 3.2 — Run to confirm failure**

```bash
dotnet test --filter "FullyQualifiedName~IssueLetter_S33_2" --no-build 2>&1 | tail -10
```
Expected: compile errors (`IssueS33_2` not in map) or runtime assertion failures.

- [ ] **Step 3.3 — Update `FileMasterController.cs` — `LetterActionMap`**

Add to the dictionary (after the S33_3b entry):
```csharp
["IssueS33_2"]     = ("S33_2_Decl",    "S33_2_DeclarationIssued"),
```

- [ ] **Step 3.4 — Update `FileMasterController.cs` — `OneTimeLetterCodes`**

Add to the HashSet:
```csharp
"S33_2_Decl",  // S33(2) Kader Asmal Declaration — issued exactly once
```

- [ ] **Step 3.5 — Update `FileMasterController.cs` — `IssueLetter` action (rates guard + board load)**

In the `IssueLetter` action, locate the section after the existing idempotency guard and before the `IssueLetterRequest` build. Add:

```csharp
// S33(2) declaration requires rates-paid confirmation on the case record.
if (string.Equals(map.LetterCode, "S33_2_Decl", StringComparison.OrdinalIgnoreCase)
    && !caseFm.S33_2_RatesPaidConfirmed)
{
    TempData["Error"] = "S33(2) declaration cannot be issued until irrigation board rates paid up to " +
                        "30 September 1998 are confirmed on the case record.";
    return RedirectToAction(nameof(Details), new { id });
}

// For S33(2), auto-populate the irrigation board name from the case record (not from the form).
if (string.Equals(map.LetterCode, "S33_2_Decl", StringComparison.OrdinalIgnoreCase))
    await _context.Entry(caseFm).Reference(f => f.S33_2_IrrigationBoard).LoadAsync(ct);
```

Then in the `IssueLetterRequest` construction, change `IrrigationBoardName: null` to:
```csharp
IrrigationBoardName: string.Equals(map.LetterCode, "S33_2_Decl", StringComparison.OrdinalIgnoreCase)
    ? caseFm.S33_2_IrrigationBoard?.IrrigationBoardName
    : null,
LawfulVolumeM3: string.Equals(map.LetterCode, "S33_2_Decl", StringComparison.OrdinalIgnoreCase)
    ? caseFm.Entitlement?.AuthorisedVolume
    : null,
```

Note: `caseFm` is the `FileMaster` loaded earlier in the action. Verify it is loaded with `.Include(f => f.Entitlement)` or add that include.

- [ ] **Step 3.6 — Update `ViewModels/FileMasterDetailsViewModel.cs`**

In the `AvailableLetterActions` `return CurrentState.StateName switch`, add before the `_` default case:

```csharp
"S33_2_ReadyForDeclaration"  => new() { "IssueS33_2" },
```

- [ ] **Step 3.7 — Update `Views/FileMaster/_LettersPanel.cshtml`**

In the `var (label, cssClass) = action switch` block, add before the `_` default:

```csharp
"IssueS33_2" => ("Issue S33(2) Kader Asmal Declaration", "btn btn-primary"),
```

- [ ] **Step 3.8 — Run failing tests to confirm green**

```bash
dotnet test --filter "FullyQualifiedName~IssueLetter_S33_2" --no-build 2>&1 | tail -10
```
Expected: PASS.

```bash
dotnet test --filter "FullyQualifiedName~OneTimeLetter" --no-build 2>&1 | tail -10
```
Expected: PASS (7 InlineData entries now).

- [ ] **Step 3.9 — Full suite**

```bash
dotnet test --no-build 2>&1 | tail -5
```
Expected: 296 passed (293 + 3 new), 0 failures.

- [ ] **Step 3.10 — Commit**

```bash
git add Controllers/FileMasterController.cs ViewModels/FileMasterDetailsViewModel.cs \
        Views/FileMaster/_LettersPanel.cshtml Tests/Controllers/FileMasterControllerLetterTests.cs
git commit -m "feat(s33-2): wire IssueS33_2 letter action with rates-paid guard and irrigation board auto-population"
```

---

## Task 4 — S33(2) data capture UI (Edit + Details)

**Files:**
- **Modify:** `Views/FileMaster/Edit.cshtml`
- **Modify:** `Views/FileMaster/Details.cshtml`
- **Modify:** `Controllers/FileMasterController.cs` (Edit GET + POST)

**Design notes:**
- The S33(2) section in Edit must only render when `Model.AssessmentTrack == "S33_2_Declaration"` — use a Razor `@if`.
- The irrigation board dropdown is seeded from `IrrigationBoards` table; pass as `ViewBag.IrrigationBoards` (SelectList) in Edit GET.
- Edit POST must bind `S33_2_IrrigationBoardId`, `S33_2_RatesPaidConfirmed`, `S33_2_ScheduledAreaName` and save them alongside the existing FileMaster fields.
- Details view: show a compact S33(2) block (irrigation board name, scheduled area, rates-paid status) when `AssessmentTrack == "S33_2_Declaration"`.
- No new tests needed for pure Razor rendering, but the Edit POST binding must be covered. If `FileMasterControllerEditTests.cs` exists, add a `[Fact]` there; otherwise add to `FileMasterControllerLetterTests.cs`.

- [ ] **Step 4.1 — Write failing test for Edit POST binding**

```csharp
[Fact]
public async Task Edit_Post_SavesS33_2Fields()
{
    var (sut, db) = BuildController(db =>
    {
        var board = new IrrigationBoard { IrrigationBoardId = Guid.NewGuid(), IrrigationBoardName = "Board A" };
        db.IrrigationBoards.Add(board);
        // seed a FileMaster at CP1 (any state; edit is track-agnostic)
        var fm = FileMasterBuilder.AtState(db, "CP1_WARMSObtained");
        fm.AssessmentTrack = "S33_2_Declaration";
    });

    var boardId = db.IrrigationBoards.Single().IrrigationBoardId;

    // Simulate the Edit POST form with S33_2 fields
    var formFm = db.FileMasters.AsNoTracking().Single();
    formFm.S33_2_IrrigationBoardId = boardId;
    formFm.S33_2_RatesPaidConfirmed = true;
    formFm.S33_2_ScheduledAreaName = "Upper Blyde Scheme";

    var result = await sut.Edit(formFm.FileMasterId, formFm);

    var saved = db.FileMasters.Single();
    Assert.Equal(boardId, saved.S33_2_IrrigationBoardId);
    Assert.True(saved.S33_2_RatesPaidConfirmed);
    Assert.Equal("Upper Blyde Scheme", saved.S33_2_ScheduledAreaName);
}
```

- [ ] **Step 4.2 — Run to confirm failure**

```bash
dotnet test --filter "FullyQualifiedName~Edit_Post_SavesS33_2Fields" --no-build 2>&1 | tail -10
```
Expected: FAIL — fields not yet bound in Edit POST.

- [ ] **Step 4.3 — Update `FileMasterController.cs` Edit GET**

In the Edit GET action, after the existing ViewBag assignments, add:

```csharp
ViewBag.IrrigationBoards = new SelectList(
    await _context.IrrigationBoards.OrderBy(b => b.IrrigationBoardName).ToListAsync(),
    nameof(IrrigationBoard.IrrigationBoardId),
    nameof(IrrigationBoard.IrrigationBoardName),
    fileMaster.S33_2_IrrigationBoardId);
```

- [ ] **Step 4.4 — Update `FileMasterController.cs` Edit POST**

In the Edit POST action, find where the existing FileMaster fields are updated (e.g., `existingFm.FarmName = model.FarmName;` block). Add:

```csharp
existingFm.S33_2_IrrigationBoardId  = model.S33_2_IrrigationBoardId;
existingFm.S33_2_RatesPaidConfirmed = model.S33_2_RatesPaidConfirmed;
existingFm.S33_2_ScheduledAreaName  = model.S33_2_ScheduledAreaName;
```

- [ ] **Step 4.5 — Add S33(2) section to `Views/FileMaster/Edit.cshtml`**

After the `AssessmentTrack` dropdown in the form, add:

```html
@if (Model?.AssessmentTrack == "S33_2_Declaration")
{
    <fieldset class="form-section" style="margin-top:16px; border:1px solid var(--dws-border); border-radius:6px; padding:16px;">
        <legend style="font-size:var(--dws-fs-sm); font-weight:600; padding:0 8px;">S33(2) Kader Asmal Declaration</legend>
        <div class="form-group">
            <label asp-for="S33_2_ScheduledAreaName" class="form-label"></label>
            <input asp-for="S33_2_ScheduledAreaName" class="form-input" placeholder="e.g. Upper Blyde Irrigation Scheme" />
        </div>
        <div class="form-group">
            <label class="form-label">Irrigation Board</label>
            <select name="S33_2_IrrigationBoardId" class="form-select">
                <option value="">-- Select irrigation board --</option>
                @Html.DropDownList("S33_2_IrrigationBoardId", ViewBag.IrrigationBoards as SelectList, new { @class = "form-select" })
            </select>
        </div>
        <div class="form-group" style="display:flex; align-items:center; gap:8px;">
            <input asp-for="S33_2_RatesPaidConfirmed" type="checkbox" id="RatesPaid" />
            <label for="RatesPaid" class="form-label" style="margin:0;">
                Prescribed rates were paid up to <strong>30 September 1998</strong>
            </label>
        </div>
    </fieldset>
}
```

- [ ] **Step 4.6 — Add S33(2) info block to `Views/FileMaster/Details.cshtml`**

In the case information section (near where `AssessmentTrack` is displayed), add:

```html
@if (fm.AssessmentTrack == "S33_2_Declaration")
{
    <div class="card" style="margin-top:12px; border-left:4px solid var(--dws-primary); padding:12px 16px;">
        <div class="form-section-title" style="margin-bottom:8px;">S33(2) Kader Asmal Declaration Data</div>
        <div class="kv"><div class="form-label">Irrigation Board</div><div>@(fm.S33_2_IrrigationBoard?.IrrigationBoardName ?? "--")</div></div>
        <div class="kv"><div class="form-label">Scheduled Area</div><div>@(fm.S33_2_ScheduledAreaName ?? "--")</div></div>
        <div class="kv">
            <div class="form-label">Rates paid to 30 Sep 1998</div>
            <div style="color:@(fm.S33_2_RatesPaidConfirmed ? "var(--dws-success)" : "var(--dws-danger)"); font-weight:600;">
                @(fm.S33_2_RatesPaidConfirmed ? "Confirmed" : "Not yet confirmed")
            </div>
        </div>
    </div>
}
```

Ensure the Details GET action loads `S33_2_IrrigationBoard` on the `FileMaster`. Find the existing `Include` chain in the Details action and add `.ThenInclude` or a separate `.Include(f => f.S33_2_IrrigationBoard)`.

- [ ] **Step 4.7 — Run test to confirm green**

```bash
dotnet test --filter "FullyQualifiedName~Edit_Post_SavesS33_2Fields" --no-build 2>&1 | tail -10
```
Expected: PASS.

- [ ] **Step 4.8 — Full suite**

```bash
dotnet test --no-build 2>&1 | tail -5
```
Expected: 297 passed, 0 failures.

- [ ] **Step 4.9 — Build to catch Razor errors**

```bash
dotnet build 2>&1 | grep -E "error|Error" | head -10
```
Expected: 0 errors.

- [ ] **Step 4.10 — Commit**

```bash
git add Views/FileMaster/Edit.cshtml Views/FileMaster/Details.cshtml \
        Controllers/FileMasterController.cs Tests/Controllers/FileMasterControllerLetterTests.cs
git commit -m "feat(s33-2): S33(2) data capture fields on FileMaster Edit + Details views"
```

---

## Self-Review Checklist

**Spec coverage:**

| CLAUDE.md requirement | Task covering it |
|---|---|
| S33(2) Kader Asmal Declaration — irrigation board membership tracked | Task 2 + Task 4 |
| Rates paid up to 30 September 1998 tracked | Task 2 + Task 4 |
| Dormant but paid-for volumes considered ELU | `LawfulVolumeM3` from `Entitlement.AuthorisedVolume` in Task 3 |
| Full SAPWAT/digitising pipeline skipped for S33(2) | Task 1 — `S33_2_ReadyForDeclaration` skip |
| S33(2) letter type distinct from S33(3) | Task 3 — separate `IssueS33_2` action, `S33_2_Decl` code |
| Declaration letter one-time only | Task 3 — `OneTimeLetterCodes` |

**Edge cases surfaced:**

- `S33_2_RatesPaidConfirmed = false` → guard blocks letter issuance (Task 3).
- Navigation property `S33_2_IrrigationBoard` loaded lazily (targeted `Reference.LoadAsync`) only when needed, avoiding N+1 on all other letter paths (Task 3).
- `AvailableLetterActions` for `S33_2_ReadyForDeclaration` returns exactly `["IssueS33_2"]` — no other buttons show, preventing accidental S35 letter issuance on a declaration-track case (Task 3).
- DisplayOrder drift: seeder auto-corrects existing DB rows when orders shift from 33/34/35 to 34/35/36 (Task 1).
- Test seed used `IsTerminal = true` for `S33_2_DeclarationIssued` — production seeding uses `false`. Fixed in Task 1.
- `caseFm.Entitlement` may be null (no ELU calculated yet for S33(2) cases) — `?.AuthorisedVolume` handles this gracefully; template renders no blue box if null (Task 3).
- Two concurrent `IssueS33_2` requests: the `OneTimeLetterCodes` guard is a soft check (no DB unique constraint). Risk accepted as MVP — S33(2) issuance is a deliberate DWS action, not a rapid-fire endpoint. The existing audit trail records both attempts.
