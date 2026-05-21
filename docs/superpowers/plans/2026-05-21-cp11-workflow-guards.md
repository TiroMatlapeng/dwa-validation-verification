# CP11 File Compilation + PAJA Guard + Letter Service Confirmation Guards Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add three missing workflow enforcement points: a CP11 File Compilation state with a 9-check guard, a PAJA checklist guard blocking Letter 3, and letter service confirmation guards blocking advance past each S35 *Issued state.

**Architecture:** All changes follow the existing `ITransitionGuard` / `FlagGuards.cs` pattern. `SeedDataService` inserts the new `CP11_FileCompiled` state at DisplayOrder 16 (all subsequent states shift by +1, corrected idempotently). Three new guard classes are appended to `FlagGuards.cs` and registered in `Program.cs`. No new models, no migrations.

**Tech Stack:** ASP.NET Core 10, EF Core 10 (in-memory for tests), xUnit, C# 13, SQL Server.

---

## File Structure

| File | Change |
|---|---|
| `Services/SeedDataService.cs` | Insert `CP11_FileCompiled` at DO 16; shift PrePublicReview→17, StakeholderWorkshop→18, all S35_/S33_ states →+1, Closed→37 |
| `Services/WorkflowService.cs` | Add `"CP11"` to `CpsSkippedOnS33_2` array |
| `Services/Workflow/Guards/FlagGuards.cs` | Append `Cp11FileCompilationGuard`, `Cp19PajaChecklistGuard`, `LetterServiceConfirmedGuard` |
| `Program.cs` | Register 3 new guards as `AddScoped<ITransitionGuard, XxxGuard>()` |
| `Tests/Services/Workflow/GuardTests.cs` | Append 3 new test classes (20 tests total) |

---

## Task A: Insert CP11_FileCompiled State and Update S33(2) Skip List

**Files:**
- Modify: `Services/SeedDataService.cs` (lines 565–595)
- Modify: `Services/WorkflowService.cs` (line 8)
- Test: `Tests/Services/Workflow/GuardTests.cs` (new class at end of file)

### Step A1: Write a failing test confirming CP11_FileCompiled exists in seed data

In `Tests/Services/Workflow/GuardTests.cs`, append this test class after the last closing brace of `GuardTests`:

```csharp
// -----------------------------------------------------------------------
// CP11 state seed verification
// -----------------------------------------------------------------------
public class Cp11SeedTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task SeedWorkflowStates_InsertsCP11FileCompiledAt16()
    {
        using var db = NewDb();
        var svc = new SeedDataService(db);
        await svc.SeedAsync();

        var state = await db.WorkflowStates
            .SingleOrDefaultAsync(s => s.StateName == "CP11_FileCompiled");

        Assert.NotNull(state);
        Assert.Equal(16, state!.DisplayOrder);
        Assert.Equal("Verification", state.Phase);
        Assert.False(state.IsTerminal);
    }

    [Fact]
    public async Task SeedWorkflowStates_PrePublicReviewIsAt17AfterCP11Inserted()
    {
        using var db = NewDb();
        var svc = new SeedDataService(db);
        await svc.SeedAsync();

        var state = await db.WorkflowStates
            .SingleOrDefaultAsync(s => s.StateName == "CP_PrePublicReview");

        Assert.NotNull(state);
        Assert.Equal(17, state!.DisplayOrder);
    }
}
```

- [ ] **Step A2: Run failing tests**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet test Tests/dwa_ver_val.Tests.csproj --filter "Cp11SeedTests" -v minimal
```

Expected: FAIL — `CP11_FileCompiled` does not exist yet.

- [ ] **Step A3: Insert CP11_FileCompiled into the seed array**

In `Services/SeedDataService.cs`, find line 565 (the `CP9_SFRACalculated` entry). Replace the block from `CP9_SFRACalculated` through `CP_StakeholderWorkshop` with:

```csharp
            ("CP9_SFRACalculated",       "Verification", 15, false),

            // Phase: Verification — PRD CP11 File Compilation
            ("CP11_FileCompiled",        "Verification", 16, false),

            // Phase: Verification — PRD CP12/CP13 (Pre-Public Review + Stakeholder Workshop)
            ("CP_PrePublicReview",       "Verification", 17, false),
            ("CP_StakeholderWorkshop",   "Verification", 18, false),
```

Then update every `DisplayOrder` from 18 onward in the same array — increment each by 1:

```csharp
            // Phase: Verification — Section 35 letter sub-states (Track A)
            ("S35_Letter1Issued",            "Verification", 19, false),
            ("S35_Letter1Responded",         "Verification", 20, false),
            ("S35_Letter1ARequired",         "Verification", 21, false),
            ("S35_Letter1AIssued",           "Verification", 22, false),
            ("S35_Letter1AResponded",        "Verification", 23, false),
            ("S35_AdditionalInfoRequired",   "Verification", 24, false),
            ("S35_Letter2Issued",            "Verification", 25, false),
            ("S35_Letter2Responded",         "Verification", 26, false),
            ("S35_Letter2ARequired",         "Verification", 27, false),
            ("S35_Letter2AIssued",           "Verification", 28, false),
            ("S35_Letter3Issued",            "Verification", 29, false),
            ("S35_ELUConfirmed",             "Verification", 30, false),
            ("S35_UnlawfulUseFound",         "Verification", 31, false),
            ("S35_Letter4AIssued",           "Verification", 32, false),
            ("S35_Letter4And5Issued",        "Verification", 33, false),

            // Phase: Verification — Section 33 declaration sub-states (Tracks B & C)
            ("S33_2_ReadyForDeclaration",    "Verification", 34, false),
            ("S33_2_DeclarationIssued",      "Verification", 35, false),
            ("S33_3_DeclarationIssued",      "Verification", 36, false),

            // Terminal states
            ("Closed",                       "Verification", 37, true),
```

- [ ] **Step A4: Add "CP11" to the S33(2) skip list**

In `Services/WorkflowService.cs`, line 8, replace:

```csharp
    private static readonly string[] CpsSkippedOnS33_2 = { "CP5", "CP6", "CP7", "CP8", "CP9", "CP_PrePublicReview", "CP_StakeholderWorkshop" };
```

with:

```csharp
    private static readonly string[] CpsSkippedOnS33_2 = { "CP5", "CP6", "CP7", "CP8", "CP9", "CP11", "CP_PrePublicReview", "CP_StakeholderWorkshop" };
```

- [ ] **Step A5: Run tests — expect green**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet test Tests/dwa_ver_val.Tests.csproj --filter "Cp11SeedTests" -v minimal
```

Expected: PASS — both tests green.

- [ ] **Step A6: Run full test suite — no regressions**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet test Tests/dwa_ver_val.Tests.csproj -v minimal
```

Expected: all tests green.

- [ ] **Step A7: Commit**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && git add Services/SeedDataService.cs Services/WorkflowService.cs Tests/Services/Workflow/GuardTests.cs && git commit -m "feat(workflow): add CP11_FileCompiled state at DO16; update S33(2) skip list"
```

---

## Task B: Cp11FileCompilationGuard (TDD)

**Files:**
- Modify: `Tests/Services/Workflow/GuardTests.cs` (append new class)
- Modify: `Services/Workflow/Guards/FlagGuards.cs` (append new class)

### Context

The guard fires when `CurrentState.StateName.StartsWith("CP11")` AND `TargetState.StateName` does not start with `"CP11"`. It checks nine DB conditions in order and returns the first failure. All queries hit the same `ApplicationDBContext` seeded via EF in-memory.

Key DB sets used:
- `db.Authorisations` — FK `FileMasterId`
- `db.FieldAndCrops` — FK `PropertyId`, field `SAPWATCalculationResult`
- `db.Mapbooks` — FK `FileMasterId`, field `MapType` ("Qualifying" or "Current")
- `db.DamCalculations` — FK `PropertyId`
- `db.Forestations` — FK `PropertyId`
- `ctx.FileMaster.WarmsReviewedAt` — DateTime?
- `ctx.FileMaster.Property.SGCode` — string? (must navigate; load separately if needed)
- `ctx.FileMaster.EntitlementId` — Guid?
- `ctx.FileMaster.DamMarkedNA` — bool
- `ctx.FileMaster.SfraMarkedNA` — bool

**Important**: `ctx.FileMaster.Property` is a navigation property. For the SGCode check, query `db.Properties.FindAsync(ctx.FileMaster.PropertyId)` — do not assume the navigation is loaded.

- [ ] **Step B1: Write failing tests — all 10 cases**

Append this class to `Tests/Services/Workflow/GuardTests.cs`:

```csharp
// -----------------------------------------------------------------------
// Cp11FileCompilationGuard
// -----------------------------------------------------------------------
public class Cp11FileCompilationGuardTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // Builds the minimal FileMaster + Property required to compile a full file.
    // Returns (db, fm, propertyId) with all 9 items seeded.
    private static async Task<(ApplicationDBContext db, FileMaster fm, Guid propId)> FullyCompiledCase()
    {
        var db = NewDb();
        var propId = Guid.NewGuid();
        var property = new Property { PropertyId = propId, PropertyReferenceNumber = "P-CP11", SGCode = "SG-001" };
        db.Properties.Add(property);

        var authType = new AuthorisationType { AuthorisationTypeId = Guid.NewGuid(), AuthorisationTypeName = "Permit" };
        db.AuthorisationTypes.Add(authType);

        var period = new Period { PeriodId = Guid.NewGuid(), PeriodName = "Qualifying" };
        var crop   = new Crop { CropId = Guid.NewGuid(), CropName = "Maize" };
        var ws     = new WaterSource { WaterSourceId = Guid.NewGuid(), WaterSourceName = "River" };
        db.Periods.Add(period); db.Crops.Add(crop); db.WaterSources.Add(ws);

        var fm = new FileMaster
        {
            FileMasterId       = Guid.NewGuid(),
            PropertyId         = propId,
            RegistrationNumber = "WARMS-001",
            SurveyorGeneralCode = "SG-001",
            PrimaryCatchment   = "A21",
            QuaternaryCatchment = "A21A",
            FarmName           = "Testfarm",
            FarmNumber         = 1,
            RegistrationDivision = "TD",
            FarmPortion        = "0",
            WarmsReviewedAt    = DateTime.UtcNow,
            EntitlementId      = Guid.NewGuid(),
            DamMarkedNA        = true,
            SfraMarkedNA       = true
        };
        db.FileMasters.Add(fm);

        db.Authorisations.Add(new Authorisation
        {
            AuthorisationId = Guid.NewGuid(),
            FileMasterId = fm.FileMasterId,
            AuthorisationTypeId = authType.AuthorisationTypeId
        });

        db.FieldAndCrops.Add(new FieldAndCrop
        {
            FieldAndCropId = Guid.NewGuid(),
            PropertyId = propId, Property = property,
            PeriodId = period.PeriodId, Period = period,
            CropId = crop.CropId, Crop = crop,
            WaterSourceId = ws.WaterSourceId, WaterSource = ws,
            FieldArea = 10m, CropArea = 8m, RotationFactor = 0.8m,
            SAPWATCalculationResult = 600m
        });

        db.Mapbooks.Add(new Mapbook { MapbookId = Guid.NewGuid(), FileMasterId = fm.FileMasterId, MapbookTitle = "Q-Map", MapType = "Qualifying" });
        db.Mapbooks.Add(new Mapbook { MapbookId = Guid.NewGuid(), FileMasterId = fm.FileMasterId, MapbookTitle = "C-Map", MapType = "Current" });

        await db.SaveChangesAsync();
        return (db, fm, propId);
    }

    private static GuardContext LeavingCp11(FileMaster fm) =>
        new(fm,
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP11_FileCompiled", DisplayOrder = 16, Phase = "Verification" },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP_PrePublicReview", DisplayOrder = 17, Phase = "Verification" });

    private static GuardContext NotLeavingCp11(FileMaster fm) =>
        new(fm,
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP9_SFRACalculated", DisplayOrder = 15, Phase = "Verification" },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP11_FileCompiled", DisplayOrder = 16, Phase = "Verification" });

    [Fact]
    public async Task Cp11_AllowsWhenAllNineItemsPresent()
    {
        var (db, fm, _) = await FullyCompiledCase();
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task Cp11_PassesWhenNotLeavingCp11()
    {
        var (db, fm, _) = await FullyCompiledCase();
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(NotLeavingCp11(fm));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task Cp11_DeniesWhenWarmsReviewMissing()
    {
        var (db, fm, _) = await FullyCompiledCase();
        fm.WarmsReviewedAt = null;
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("WARMS review", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenSGCodeMissing()
    {
        var (db, fm, propId) = await FullyCompiledCase();
        var prop = await db.Properties.FindAsync(propId);
        prop!.SGCode = null;
        await db.SaveChangesAsync();
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("SG code", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenNoAuthorisationRecord()
    {
        var (db, fm, _) = await FullyCompiledCase();
        db.Authorisations.RemoveRange(db.Authorisations.Where(a => a.FileMasterId == fm.FileMasterId));
        await db.SaveChangesAsync();
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("authorisation", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenNoSapwatResult()
    {
        var (db, fm, propId) = await FullyCompiledCase();
        var fc = db.FieldAndCrops.First(f => f.PropertyId == propId);
        fc.SAPWATCalculationResult = 0m;
        await db.SaveChangesAsync();
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("SAPWAT", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenNoQualifyingMapbook()
    {
        var (db, fm, _) = await FullyCompiledCase();
        var qm = db.Mapbooks.First(m => m.FileMasterId == fm.FileMasterId && m.MapType == "Qualifying");
        db.Mapbooks.Remove(qm);
        await db.SaveChangesAsync();
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("Qualifying", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenEntitlementMissing()
    {
        var (db, fm, _) = await FullyCompiledCase();
        fm.EntitlementId = null;
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("Entitlement", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenNoCurrentMapbook()
    {
        var (db, fm, _) = await FullyCompiledCase();
        var cm = db.Mapbooks.First(m => m.FileMasterId == fm.FileMasterId && m.MapType == "Current");
        db.Mapbooks.Remove(cm);
        await db.SaveChangesAsync();
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("Current", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenNoDamAndNotMarkedNA()
    {
        var (db, fm, _) = await FullyCompiledCase();
        fm.DamMarkedNA = false;
        // No DamCalculation seeded
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("Dam", result.Reason);
    }

    [Fact]
    public async Task Cp11_DeniesWhenNoSfraAndNotMarkedNA()
    {
        var (db, fm, _) = await FullyCompiledCase();
        fm.SfraMarkedNA = false;
        // No Forestation seeded
        var sut = new Cp11FileCompilationGuard(db);
        var result = await sut.CheckAsync(LeavingCp11(fm));
        Assert.False(result.Allowed);
        Assert.Contains("SFRA", result.Reason);
    }
}
```

- [ ] **Step B2: Run failing tests**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet test Tests/dwa_ver_val.Tests.csproj --filter "Cp11FileCompilationGuardTests" -v minimal
```

Expected: FAIL — `Cp11FileCompilationGuard` does not exist.

- [ ] **Step B3: Implement Cp11FileCompilationGuard**

Append to `Services/Workflow/Guards/FlagGuards.cs`:

```csharp
/// <summary>
/// Leaving CP11 requires all 9 Appendix A evidence items to be present in the case file.
/// </summary>
public class Cp11FileCompilationGuard : ITransitionGuard
{
    private readonly ApplicationDBContext _db;
    public Cp11FileCompilationGuard(ApplicationDBContext db) { _db = db; }

    public async Task<GuardResult> CheckAsync(GuardContext ctx)
    {
        if (!Cp2SpatialInfoGuard.IsLeaving(ctx, "CP11")) return GuardResult.Ok;

        if (!ctx.FileMaster.WarmsReviewedAt.HasValue)
            return GuardResult.Deny("WARMS review must be recorded before file can be compiled.");

        var property = await _db.Properties.FindAsync(ctx.FileMaster.PropertyId);
        if (property is null || string.IsNullOrWhiteSpace(property.SGCode))
            return GuardResult.Deny("Property SG code must be confirmed before file can be compiled.");

        var hasAuth = await _db.Authorisations.AnyAsync(a => a.FileMasterId == ctx.FileMaster.FileMasterId);
        if (!hasAuth)
            return GuardResult.Deny("At least one authorisation record must be captured before file can be compiled.");

        var hasSapwat = await _db.FieldAndCrops
            .AnyAsync(f => f.PropertyId == ctx.FileMaster.PropertyId && f.SAPWATCalculationResult > 0);
        if (!hasSapwat)
            return GuardResult.Deny("At least one field with a SAPWAT result must be captured before file can be compiled.");

        var hasQualifyingMap = await _db.Mapbooks
            .AnyAsync(m => m.FileMasterId == ctx.FileMaster.FileMasterId && m.MapType == "Qualifying");
        if (!hasQualifyingMap)
            return GuardResult.Deny("Qualifying period mapbook must be present before file can be compiled.");

        if (!ctx.FileMaster.EntitlementId.HasValue)
            return GuardResult.Deny("Entitlement must be linked before file can be compiled.");

        var hasCurrentMap = await _db.Mapbooks
            .AnyAsync(m => m.FileMasterId == ctx.FileMaster.FileMasterId && m.MapType == "Current");
        if (!hasCurrentMap)
            return GuardResult.Deny("Current period mapbook must be present before file can be compiled.");

        if (!ctx.FileMaster.DamMarkedNA)
        {
            var hasDam = await _db.DamCalculations.AnyAsync(d => d.PropertyId == ctx.FileMaster.PropertyId);
            if (!hasDam)
                return GuardResult.Deny("Dam volume calculation must be recorded or marked N/A before file can be compiled.");
        }

        if (!ctx.FileMaster.SfraMarkedNA)
        {
            var hasSfra = await _db.Forestations.AnyAsync(f => f.PropertyId == ctx.FileMaster.PropertyId);
            if (!hasSfra)
                return GuardResult.Deny("SFRA/Forestation record must be recorded or marked N/A before file can be compiled.");
        }

        return GuardResult.Ok;
    }
}
```

- [ ] **Step B4: Run tests — expect green**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet test Tests/dwa_ver_val.Tests.csproj --filter "Cp11FileCompilationGuardTests" -v minimal
```

Expected: all 10 tests PASS.

- [ ] **Step B5: Run full suite — no regressions**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet test Tests/dwa_ver_val.Tests.csproj -v minimal
```

Expected: all green.

- [ ] **Step B6: Commit**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && git add Services/Workflow/Guards/FlagGuards.cs Tests/Services/Workflow/GuardTests.cs && git commit -m "feat(workflow): add Cp11FileCompilationGuard with 9-item evidence check"
```

---

## Task C: Cp19PajaChecklistGuard (TDD)

**Files:**
- Modify: `Tests/Services/Workflow/GuardTests.cs` (append new class)
- Modify: `Services/Workflow/Guards/FlagGuards.cs` (append new class)

### Context

The guard fires when `ctx.TargetState.StateName == "S35_Letter3Issued"` (regardless of current state). It queries `db.PAJAChecklists` for a row with `FileMasterId == ctx.FileMaster.FileMasterId`. `IsComplete` is a computed property on the model — EF loads the row; C# evaluates it.

- [ ] **Step C1: Write failing tests**

Append to `Tests/Services/Workflow/GuardTests.cs`:

```csharp
// -----------------------------------------------------------------------
// Cp19PajaChecklistGuard
// -----------------------------------------------------------------------
public class Cp19PajaChecklistGuardTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static FileMaster MinimalCase() => new FileMaster
    {
        FileMasterId = Guid.NewGuid(),
        PropertyId = Guid.NewGuid(),
        RegistrationNumber = "WARMS-PAJA",
        SurveyorGeneralCode = "SG-PAJA",
        PrimaryCatchment = "A21",
        QuaternaryCatchment = "A21A",
        FarmName = "PAJAFarm",
        FarmNumber = 1,
        RegistrationDivision = "TD",
        FarmPortion = "0"
    };

    private static GuardContext TargetingLetter3(FileMaster fm) =>
        new(fm,
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_ELUConfirmed", DisplayOrder = 30, Phase = "Verification" },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_Letter3Issued", DisplayOrder = 29, Phase = "Verification" });

    private static GuardContext NotTargetingLetter3(FileMaster fm) =>
        new(fm,
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP_PrePublicReview", DisplayOrder = 17, Phase = "Verification" },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP_StakeholderWorkshop", DisplayOrder = 18, Phase = "Verification" });

    [Fact]
    public async Task Cp19_DeniesWhenNoChecklistExists()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        await db.SaveChangesAsync();

        var sut = new Cp19PajaChecklistGuard(db);
        var result = await sut.CheckAsync(TargetingLetter3(fm));
        Assert.False(result.Allowed);
        Assert.Contains("PAJA checklist", result.Reason);
    }

    [Fact]
    public async Task Cp19_DeniesWhenChecklistExistsButIncomplete()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        db.PAJAChecklists.Add(new PAJAChecklist
        {
            PAJAChecklistId = Guid.NewGuid(),
            FileMasterId = fm.FileMasterId,
            FactualBasis = "Present",
            LegalBasis = "Present",
            UserInputConsideration = "Present",
            FinalReasoning = null,  // missing — IsComplete == false
            CompletedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new Cp19PajaChecklistGuard(db);
        var result = await sut.CheckAsync(TargetingLetter3(fm));
        Assert.False(result.Allowed);
        Assert.Contains("incomplete", result.Reason);
    }

    [Fact]
    public async Task Cp19_AllowsWhenChecklistComplete()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        db.PAJAChecklists.Add(new PAJAChecklist
        {
            PAJAChecklistId = Guid.NewGuid(),
            FileMasterId = fm.FileMasterId,
            FactualBasis = "The water use existed during the qualifying period.",
            LegalBasis = "Authorised by riparian right under the old Water Act.",
            UserInputConsideration = "User confirmed use in writing.",
            FinalReasoning = "Use is lawful — ELU confirmed.",
            CompletedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new Cp19PajaChecklistGuard(db);
        var result = await sut.CheckAsync(TargetingLetter3(fm));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task Cp19_PassesWhenNotTargetingLetter3()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        // No PAJAChecklist — guard must short-circuit.
        var sut = new Cp19PajaChecklistGuard(db);
        var result = await sut.CheckAsync(NotTargetingLetter3(fm));
        Assert.True(result.Allowed);
    }
}
```

- [ ] **Step C2: Run failing tests**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet test Tests/dwa_ver_val.Tests.csproj --filter "Cp19PajaChecklistGuardTests" -v minimal
```

Expected: FAIL — `Cp19PajaChecklistGuard` does not exist.

- [ ] **Step C3: Implement Cp19PajaChecklistGuard**

Append to `Services/Workflow/Guards/FlagGuards.cs`:

```csharp
/// <summary>
/// Targeting S35_Letter3Issued (ELU certificate) requires the PAJA four-field
/// checklist to be completed and marked complete.
/// </summary>
public class Cp19PajaChecklistGuard : ITransitionGuard
{
    private readonly ApplicationDBContext _db;
    public Cp19PajaChecklistGuard(ApplicationDBContext db) { _db = db; }

    public async Task<GuardResult> CheckAsync(GuardContext ctx)
    {
        if (ctx.TargetState.StateName != "S35_Letter3Issued") return GuardResult.Ok;

        var checklist = await _db.PAJAChecklists
            .FirstOrDefaultAsync(c => c.FileMasterId == ctx.FileMaster.FileMasterId);

        if (checklist is null)
            return GuardResult.Deny("PAJA checklist must be completed before Letter 3 (ELU certificate) can be issued.");

        if (!checklist.IsComplete)
            return GuardResult.Deny("PAJA checklist is incomplete — all four sections must be filled and the checklist must be marked complete before Letter 3 can be issued.");

        return GuardResult.Ok;
    }
}
```

- [ ] **Step C4: Run tests — expect green**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet test Tests/dwa_ver_val.Tests.csproj --filter "Cp19PajaChecklistGuardTests" -v minimal
```

Expected: all 4 tests PASS.

- [ ] **Step C5: Run full suite**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet test Tests/dwa_ver_val.Tests.csproj -v minimal
```

Expected: all green.

- [ ] **Step C6: Commit**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && git add Services/Workflow/Guards/FlagGuards.cs Tests/Services/Workflow/GuardTests.cs && git commit -m "feat(workflow): add Cp19PajaChecklistGuard — blocks Letter 3 until PAJA checklist complete"
```

---

## Task D: LetterServiceConfirmedGuard (TDD)

**Files:**
- Modify: `Tests/Services/Workflow/GuardTests.cs` (append new class)
- Modify: `Services/Workflow/Guards/FlagGuards.cs` (append new class)

### Context

Guard fires when `CurrentState.StateName` is one of `S35_Letter1Issued`, `S35_Letter1AIssued`, `S35_Letter2Issued`, `S35_Letter2AIssued` AND the target state does NOT start with the same prefix. It looks up the most recent `LetterIssuance` for the case where `LetterType.LetterName` equals the matching code and checks `ServiceConfirmedDate.HasValue`.

Letter code mapping (from `SeedDataService.SeedLetterTypesAsync`):
- `S35_Letter1Issued` → `S35_L1`
- `S35_Letter1AIssued` → `S35_L1A`
- `S35_Letter2Issued` → `S35_L2`
- `S35_Letter2AIssued` → `S35_L2A`

Query pattern: `db.LetterIssuances.Include(l => l.LetterType).Where(l => l.FileMasterId == id && l.LetterType!.LetterName == code).OrderByDescending(l => l.IssuedDate).FirstOrDefaultAsync()`

- [ ] **Step D1: Write failing tests**

Append to `Tests/Services/Workflow/GuardTests.cs`:

```csharp
// -----------------------------------------------------------------------
// LetterServiceConfirmedGuard
// -----------------------------------------------------------------------
public class LetterServiceConfirmedGuardTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static FileMaster MinimalCase() => new FileMaster
    {
        FileMasterId = Guid.NewGuid(),
        PropertyId = Guid.NewGuid(),
        RegistrationNumber = "WARMS-LSC",
        SurveyorGeneralCode = "SG-LSC",
        PrimaryCatchment = "A21",
        QuaternaryCatchment = "A21A",
        FarmName = "LSCFarm",
        FarmNumber = 2,
        RegistrationDivision = "TD",
        FarmPortion = "0"
    };

    private static LetterType LetterTypeFor(string code) => new LetterType
    {
        LetterTypeId = Guid.NewGuid(),
        LetterName = code,
        LetterDescription = code,
        NWASection = "S35"
    };

    private static LetterIssuance IssuanceFor(Guid fileMasterId, LetterType lt, DateOnly? confirmedDate) =>
        new LetterIssuance
        {
            LetterIssuanceId = Guid.NewGuid(),
            FileMasterId = fileMasterId,
            LetterTypeId = lt.LetterTypeId,
            LetterType = lt,
            IssuedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ServiceConfirmedDate = confirmedDate
        };

    private static GuardContext LeavingIssuedState(FileMaster fm, string issuedState, string nextState) =>
        new(fm,
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = issuedState, DisplayOrder = 1, Phase = "Verification" },
            new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = nextState,   DisplayOrder = 2, Phase = "Verification" });

    // Letter 1 — deny without confirmed date
    [Fact]
    public async Task Letter1_DeniesWhenServiceNotConfirmed()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        var lt = LetterTypeFor("S35_L1");
        db.LetterTypes.Add(lt);
        db.LetterIssuances.Add(IssuanceFor(fm.FileMasterId, lt, confirmedDate: null));
        await db.SaveChangesAsync();

        var sut = new LetterServiceConfirmedGuard(db);
        var result = await sut.CheckAsync(LeavingIssuedState(fm, "S35_Letter1Issued", "S35_Letter1Responded"));
        Assert.False(result.Allowed);
        Assert.Contains("Letter 1", result.Reason);
    }

    // Letter 1 — allow when confirmed
    [Fact]
    public async Task Letter1_AllowsWhenServiceConfirmed()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        var lt = LetterTypeFor("S35_L1");
        db.LetterTypes.Add(lt);
        db.LetterIssuances.Add(IssuanceFor(fm.FileMasterId, lt, confirmedDate: DateOnly.FromDateTime(DateTime.UtcNow)));
        await db.SaveChangesAsync();

        var sut = new LetterServiceConfirmedGuard(db);
        var result = await sut.CheckAsync(LeavingIssuedState(fm, "S35_Letter1Issued", "S35_Letter1Responded"));
        Assert.True(result.Allowed);
    }

    // Letter 1A — deny without confirmed date
    [Fact]
    public async Task Letter1A_DeniesWhenServiceNotConfirmed()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        var lt = LetterTypeFor("S35_L1A");
        db.LetterTypes.Add(lt);
        db.LetterIssuances.Add(IssuanceFor(fm.FileMasterId, lt, confirmedDate: null));
        await db.SaveChangesAsync();

        var sut = new LetterServiceConfirmedGuard(db);
        var result = await sut.CheckAsync(LeavingIssuedState(fm, "S35_Letter1AIssued", "S35_Letter1AResponded"));
        Assert.False(result.Allowed);
        Assert.Contains("Letter 1A", result.Reason);
    }

    // Letter 1A — allow when confirmed
    [Fact]
    public async Task Letter1A_AllowsWhenServiceConfirmed()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        var lt = LetterTypeFor("S35_L1A");
        db.LetterTypes.Add(lt);
        db.LetterIssuances.Add(IssuanceFor(fm.FileMasterId, lt, confirmedDate: DateOnly.FromDateTime(DateTime.UtcNow)));
        await db.SaveChangesAsync();

        var sut = new LetterServiceConfirmedGuard(db);
        var result = await sut.CheckAsync(LeavingIssuedState(fm, "S35_Letter1AIssued", "S35_Letter1AResponded"));
        Assert.True(result.Allowed);
    }

    // Letter 2 — deny without confirmed date
    [Fact]
    public async Task Letter2_DeniesWhenServiceNotConfirmed()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        var lt = LetterTypeFor("S35_L2");
        db.LetterTypes.Add(lt);
        db.LetterIssuances.Add(IssuanceFor(fm.FileMasterId, lt, confirmedDate: null));
        await db.SaveChangesAsync();

        var sut = new LetterServiceConfirmedGuard(db);
        var result = await sut.CheckAsync(LeavingIssuedState(fm, "S35_Letter2Issued", "S35_Letter2Responded"));
        Assert.False(result.Allowed);
        Assert.Contains("Letter 2", result.Reason);
    }

    // Letter 2 — allow when confirmed
    [Fact]
    public async Task Letter2_AllowsWhenServiceConfirmed()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        var lt = LetterTypeFor("S35_L2");
        db.LetterTypes.Add(lt);
        db.LetterIssuances.Add(IssuanceFor(fm.FileMasterId, lt, confirmedDate: DateOnly.FromDateTime(DateTime.UtcNow)));
        await db.SaveChangesAsync();

        var sut = new LetterServiceConfirmedGuard(db);
        var result = await sut.CheckAsync(LeavingIssuedState(fm, "S35_Letter2Issued", "S35_Letter2Responded"));
        Assert.True(result.Allowed);
    }

    // Letter 2A — deny without confirmed date
    [Fact]
    public async Task Letter2A_DeniesWhenServiceNotConfirmed()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        var lt = LetterTypeFor("S35_L2A");
        db.LetterTypes.Add(lt);
        db.LetterIssuances.Add(IssuanceFor(fm.FileMasterId, lt, confirmedDate: null));
        await db.SaveChangesAsync();

        var sut = new LetterServiceConfirmedGuard(db);
        var result = await sut.CheckAsync(LeavingIssuedState(fm, "S35_Letter2AIssued", "S35_Letter3Issued"));
        Assert.False(result.Allowed);
        Assert.Contains("Letter 2A", result.Reason);
    }

    // Letter 2A — allow when confirmed
    [Fact]
    public async Task Letter2A_AllowsWhenServiceConfirmed()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        db.FileMasters.Add(fm);
        var lt = LetterTypeFor("S35_L2A");
        db.LetterTypes.Add(lt);
        db.LetterIssuances.Add(IssuanceFor(fm.FileMasterId, lt, confirmedDate: DateOnly.FromDateTime(DateTime.UtcNow)));
        await db.SaveChangesAsync();

        var sut = new LetterServiceConfirmedGuard(db);
        var result = await sut.CheckAsync(LeavingIssuedState(fm, "S35_Letter2AIssued", "S35_Letter3Issued"));
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task LetterService_PassesWhenNotInAnyIssuedState()
    {
        using var db = NewDb();
        var fm = MinimalCase();
        // No LetterIssuance seeded — guard must short-circuit.
        var sut = new LetterServiceConfirmedGuard(db);
        var result = await sut.CheckAsync(LeavingIssuedState(fm, "CP_StakeholderWorkshop", "S35_Letter1Issued"));
        Assert.True(result.Allowed);
    }
}
```

- [ ] **Step D2: Run failing tests**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet test Tests/dwa_ver_val.Tests.csproj --filter "LetterServiceConfirmedGuardTests" -v minimal
```

Expected: FAIL — `LetterServiceConfirmedGuard` does not exist.

- [ ] **Step D3: Implement LetterServiceConfirmedGuard**

Append to `Services/Workflow/Guards/FlagGuards.cs`:

```csharp
/// <summary>
/// Leaving an S35 *Issued state requires proof of service (ServiceConfirmedDate)
/// on the corresponding letter before the case can advance.
/// </summary>
public class LetterServiceConfirmedGuard : ITransitionGuard
{
    private readonly ApplicationDBContext _db;
    public LetterServiceConfirmedGuard(ApplicationDBContext db) { _db = db; }

    private static readonly Dictionary<string, (string Code, string Label)> _map = new()
    {
        ["S35_Letter1Issued"]  = ("S35_L1",  "Letter 1"),
        ["S35_Letter1AIssued"] = ("S35_L1A", "Letter 1A"),
        ["S35_Letter2Issued"]  = ("S35_L2",  "Letter 2"),
        ["S35_Letter2AIssued"] = ("S35_L2A", "Letter 2A"),
    };

    public async Task<GuardResult> CheckAsync(GuardContext ctx)
    {
        if (!_map.TryGetValue(ctx.CurrentState.StateName, out var entry)) return GuardResult.Ok;
        // Guard only fires when leaving the issued state (target is different)
        if (ctx.TargetState.StateName == ctx.CurrentState.StateName) return GuardResult.Ok;

        var issuance = await _db.LetterIssuances
            .Include(l => l.LetterType)
            .Where(l => l.FileMasterId == ctx.FileMaster.FileMasterId
                     && l.LetterType!.LetterName == entry.Code)
            .OrderByDescending(l => l.IssuedDate)
            .FirstOrDefaultAsync();

        if (issuance?.ServiceConfirmedDate.HasValue == true) return GuardResult.Ok;

        return GuardResult.Deny($"{entry.Label} service must be confirmed (proof of delivery recorded) before advancing.");
    }
}
```

- [ ] **Step D4: Run tests — expect green**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet test Tests/dwa_ver_val.Tests.csproj --filter "LetterServiceConfirmedGuardTests" -v minimal
```

Expected: all 9 tests PASS.

- [ ] **Step D5: Run full suite**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet test Tests/dwa_ver_val.Tests.csproj -v minimal
```

Expected: all green.

- [ ] **Step D6: Commit**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && git add Services/Workflow/Guards/FlagGuards.cs Tests/Services/Workflow/GuardTests.cs && git commit -m "feat(workflow): add LetterServiceConfirmedGuard — requires proof of service on S35 letters"
```

---

## Task E: Register All Three New Guards in Program.cs

**Files:**
- Modify: `Program.cs` (lines 119–128)

No new tests needed — guard registration is integration-level infrastructure; the guard unit tests already cover behaviour.

- [ ] **Step E1: Append the three guard registrations in Program.cs**

In `Program.cs`, after line 128 (`CpStakeholderWorkshopGuard` registration), add:

```csharp
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.Cp11FileCompilationGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.Cp19PajaChecklistGuard>();
builder.Services.AddScoped<dwa_ver_val.Services.Workflow.ITransitionGuard, dwa_ver_val.Services.Workflow.Guards.LetterServiceConfirmedGuard>();
```

- [ ] **Step E2: Build to verify no compile errors**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet build -v minimal
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step E3: Run full test suite**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet test Tests/dwa_ver_val.Tests.csproj -v minimal
```

Expected: all green.

- [ ] **Step E4: Commit**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && git add Program.cs && git commit -m "feat(workflow): register Cp11, Cp19Paja, and LetterServiceConfirmed guards in DI"
```

---

## Final Verification

After Task E, run the full suite one last time:

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet test Tests/dwa_ver_val.Tests.csproj -v minimal
```

All tests should be green. The feature is complete.
