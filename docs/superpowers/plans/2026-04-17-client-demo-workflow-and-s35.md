# Client Demo Implementation Plan — Workflow + S35 Letter Flow

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a working, data-backed demo of a V&V case advancing through all control points and the Section 35 letter flow, all visible on `FileMaster/Details`.

**Architecture:** New `WorkflowService` (service layer) encapsulates state transitions. Extended `SeedDataService` adds sample cases pre-positioned at different workflow stages. Extended `FileMasterController` exposes advance/issue-letter/mark-response actions. Extended `FileMaster/Details.cshtml` adds two partial panels: workflow tracker and letters.

**Tech Stack:** ASP.NET Core 10 MVC, EF Core 10, SQL Server 2022 (Docker), xUnit + EF Core InMemory for tests.

**Spec:** `docs/superpowers/specs/2026-04-17-client-demo-workflow-and-s35-design.md`

**Important context discovered during planning:**
- `WorkflowState` table is **already seeded** with 33 states (CP1 sub-steps through S35 letter states). The plan uses these existing states.
- `LetterType` is also already seeded with Letters 1, 1A, 2, 2A, 3, 4A, 4&5, plus S33 declaration letters.
- Sample cases in seed will be positioned at `CP1_WARMSObtained` (fresh), `CP5_GISAnalysis` (mid), and `CP9_SFRACalculated` (ready for letters) using `DisplayOrder` 1, 11, 15 respectively.

**Known issue addressed:** In the existing seed, `S35_ELUConfirmed` sits at `DisplayOrder = 26` BEFORE `S35_Letter3Issued = 27` — that's backwards for the S35 flow (Letter 3 is issued, then ELU is confirmed). Task 1.5 fixes this by swapping the two values in `SeedWorkflowStatesAsync`.

---

## File Structure

```
Interfaces/
  IWorkflowService.cs            -- NEW. Service contract.

Services/
  WorkflowService.cs             -- NEW. Advance + issue-letter state logic.
  SeedDataService.cs             -- MODIFY. Append SeedSampleCasesAsync() step.

Controllers/
  FileMasterController.cs        -- MODIFY. Add AdvanceWorkflow, IssueLetter, MarkLetterResponse, Inject IWorkflowService. Auto-create WorkflowInstance on Create.

Interfaces/
  IFileMaster.cs                 -- MODIFY. Add GetWithWorkflowAsync overload that Includes WorkflowInstance, current state, step history, letters.

Repositories/
  FileMasterRepository.cs        -- MODIFY. Implement GetWithWorkflowAsync.

Views/FileMaster/
  Details.cshtml                 -- MODIFY. Render workflow panel + letters panel partials.
  _WorkflowPanel.cshtml          -- NEW. Phase tracker + advance button + transition history.
  _LettersPanel.cshtml           -- NEW. Existing letters table + buttons keyed to current state.
  _IssueLetterForm.cshtml        -- NEW. Inline form for recipient/method/date.

ViewModels/
  FileMasterDetailsViewModel.cs  -- NEW. Wraps FileMaster + workflow projection + available letter actions.

Program.cs                       -- MODIFY. Register IWorkflowService.

wwwroot/css/
  site.css                       -- MODIFY. Append phase-tracker, cp-pill, timeline, letters-panel styles.

Tests/Services/
  WorkflowServiceTests.cs        -- NEW. xUnit tests for StartWorkflow, Advance, IssueLetter transitions.
```

---

## Task 1: Verify baseline — app runs, DB is current, tests pass

**Files:** no changes, verification only.

- [ ] **Step 1: Ensure Docker SQL Server is up**

Run: `docker compose up -d`
Expected: `dwa_sqlserver` (or similar) container running on `localhost:1433`.

- [ ] **Step 2: Apply any pending migrations**

Run: `dotnet ef database update`
Expected: `Done.` or `No migrations were applied. The database is already up to date.`

- [ ] **Step 3: Build**

Run: `dotnet build --nologo`
Expected: `Build succeeded. 0 Error(s)` (warnings allowed).

- [ ] **Step 4: Run existing tests**

Run: `dotnet test --nologo`
Expected: `Passed!` with ~33 tests.

- [ ] **Step 5: Start the app and confirm Properties/FileMaster pages render**

Run: `dotnet run` (in a separate terminal)
Open: `https://localhost:7xxx/FileMaster` and `https://localhost:7xxx/Property`
Expected: sidebar renders, pages load (may be empty). Stop the app (Ctrl+C).

- [ ] **Step 6: No commit — baseline verification only**

---

## Task 1.5: Fix S35 state DisplayOrder (swap ELUConfirmed ↔ Letter3Issued)

**Files:**
- Modify: `Services/SeedDataService.cs`

**Reason:** Existing seed has `S35_ELUConfirmed = 26` before `S35_Letter3Issued = 27`. Letter 3 issues the ELU certificate, so Letter3Issued must come first in phase-tracker ordering. TransitionToAsync uses names (not order), so functional logic works either way — but the visual phase tracker reads `isPast = DisplayOrder < current`, which flips incorrectly with the backwards order.

- [ ] **Step 1: Swap the two `DisplayOrder` values in `SeedWorkflowStatesAsync`**

In `Services/SeedDataService.cs`, find these two lines (around lines 151-152):

```csharp
new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_ELUConfirmed",            Phase = "Verification", DisplayOrder = 26, IsTerminal = false },
new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_Letter3Issued",           Phase = "Verification", DisplayOrder = 27, IsTerminal = false },
```

Swap so `Letter3Issued` is 26 and `ELUConfirmed` is 27:

```csharp
new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_Letter3Issued",           Phase = "Verification", DisplayOrder = 26, IsTerminal = false },
new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "S35_ELUConfirmed",            Phase = "Verification", DisplayOrder = 27, IsTerminal = false },
```

- [ ] **Step 2: Build**

Run: `dotnet build --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 3: Commit**

```bash
git add Services/SeedDataService.cs
git commit -m "Swap S35 DisplayOrder: Letter3Issued before ELUConfirmed"
```

Note: existing database will not reflect the swap until re-seeded. Task 4 Step 4 does a full DB drop + update, which will apply the corrected order.

---

## Task 2: Define `IWorkflowService` and `WorkflowService` with core tests

**Files:**
- Create: `Interfaces/IWorkflowService.cs`
- Create: `Services/WorkflowService.cs`
- Create: `Tests/Services/WorkflowServiceTests.cs`

- [ ] **Step 1: Write the failing test file**

Create `Tests/Services/WorkflowServiceTests.cs`:

```csharp
using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Tests.Services;

public class WorkflowServiceTests
{
    private static async Task<(ApplicationDBContext ctx, Guid fileMasterId, Guid firstStateId)> SetupAsync()
    {
        var ctx = TestDbContextFactory.Create();

        var firstState = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP1_A", Phase = "Inception", DisplayOrder = 1, IsTerminal = false };
        var secondState = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "CP1_B", Phase = "Inception", DisplayOrder = 2, IsTerminal = false };
        var terminal = new WorkflowState { WorkflowStateId = Guid.NewGuid(), StateName = "Closed", Phase = "Verification", DisplayOrder = 3, IsTerminal = true };
        ctx.WorkflowStates.AddRange(firstState, secondState, terminal);

        var property = new Property { PropertyId = Guid.NewGuid(), PropertyReferenceNumber = "P1", SGCode = "SG1" };
        ctx.Properties.Add(property);

        var fileMaster = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            RegistrationNumber = "WARMS1",
            PropertyId = property.PropertyId,
            SurveyorGeneralCode = "SG1",
            PrimaryCatchment = "A",
            QuaternaryCatchment = "A21A",
            FarmName = "Doornhoek",
            FarmNumber = 1,
            RegistrationDivision = "JR",
            FarmPortion = "0",
            FileCreatedDate = DateOnly.FromDateTime(DateTime.Today),
        };
        ctx.FileMasters.Add(fileMaster);
        await ctx.SaveChangesAsync();

        return (ctx, fileMaster.FileMasterId, firstState.WorkflowStateId);
    }

    [Fact]
    public async Task StartWorkflowAsync_creates_instance_at_first_state_and_first_step_record()
    {
        var (ctx, fmId, firstStateId) = await SetupAsync();
        var svc = new WorkflowService(ctx);

        var instance = await svc.StartWorkflowAsync(fmId);

        Assert.Equal(firstStateId, instance.CurrentWorkflowStateId);
        Assert.Equal("Active", instance.Status);
        var step = await ctx.WorkflowStepRecords.SingleAsync(s => s.WorkflowInstanceId == instance.WorkflowInstanceId);
        Assert.Equal("InProgress", step.StepStatus);
        Assert.Equal(firstStateId, step.WorkflowStateId);
    }

    [Fact]
    public async Task AdvanceAsync_moves_to_next_state_and_completes_previous_step()
    {
        var (ctx, fmId, _) = await SetupAsync();
        var svc = new WorkflowService(ctx);
        await svc.StartWorkflowAsync(fmId);

        var instance = await svc.AdvanceAsync(fmId, userId: null, notes: null);

        var next = await ctx.WorkflowStates.SingleAsync(s => s.StateName == "CP1_B");
        Assert.Equal(next.WorkflowStateId, instance.CurrentWorkflowStateId);

        var steps = await ctx.WorkflowStepRecords.Where(s => s.WorkflowInstanceId == instance.WorkflowInstanceId).OrderBy(s => s.StartedDate).ToListAsync();
        Assert.Equal(2, steps.Count);
        Assert.Equal("Completed", steps[0].StepStatus);
        Assert.NotNull(steps[0].CompletedDate);
        Assert.Equal("InProgress", steps[1].StepStatus);
    }

    [Fact]
    public async Task AdvanceAsync_at_terminal_state_throws()
    {
        var (ctx, fmId, _) = await SetupAsync();
        var svc = new WorkflowService(ctx);
        await svc.StartWorkflowAsync(fmId);
        await svc.AdvanceAsync(fmId, null, null); // -> CP1_B
        await svc.AdvanceAsync(fmId, null, null); // -> Closed (terminal)

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AdvanceAsync(fmId, null, null));
    }

    [Fact]
    public async Task TransitionToAsync_moves_to_named_state_and_records_step()
    {
        var (ctx, fmId, _) = await SetupAsync();
        var svc = new WorkflowService(ctx);
        await svc.StartWorkflowAsync(fmId);

        var instance = await svc.TransitionToAsync(fmId, "Closed", userId: null, notes: "demo");

        var closed = await ctx.WorkflowStates.SingleAsync(s => s.StateName == "Closed");
        Assert.Equal(closed.WorkflowStateId, instance.CurrentWorkflowStateId);
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail (class does not compile yet)**

Run: `dotnet test --nologo --filter "FullyQualifiedName~WorkflowServiceTests"`
Expected: compile errors referencing `WorkflowService` and `IWorkflowService` — that's expected.

- [ ] **Step 3: Create `IWorkflowService`**

Create `Interfaces/IWorkflowService.cs`:

```csharp
public interface IWorkflowService
{
    Task<WorkflowInstance> StartWorkflowAsync(Guid fileMasterId);
    Task<WorkflowInstance> AdvanceAsync(Guid fileMasterId, Guid? userId, string? notes);
    Task<WorkflowInstance> TransitionToAsync(Guid fileMasterId, string targetStateName, Guid? userId, string? notes);
    Task<IReadOnlyList<WorkflowStepRecord>> GetHistoryAsync(Guid workflowInstanceId);
    Task<WorkflowInstance?> GetInstanceForFileAsync(Guid fileMasterId);
}
```

- [ ] **Step 4: Implement `WorkflowService`**

Create `Services/WorkflowService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

public class WorkflowService : IWorkflowService
{
    private readonly ApplicationDBContext _context;

    public WorkflowService(ApplicationDBContext context)
    {
        _context = context;
    }

    public async Task<WorkflowInstance> StartWorkflowAsync(Guid fileMasterId)
    {
        var fileMaster = await _context.FileMasters.FindAsync(fileMasterId)
            ?? throw new InvalidOperationException($"FileMaster {fileMasterId} not found.");

        if (fileMaster.WorkflowInstanceId.HasValue)
            throw new InvalidOperationException("Workflow already started for this case.");

        var firstState = await _context.WorkflowStates
            .OrderBy(s => s.DisplayOrder)
            .FirstAsync();

        var instance = new WorkflowInstance
        {
            WorkflowInstanceId = Guid.NewGuid(),
            FileMasterId = fileMasterId,
            CurrentWorkflowStateId = firstState.WorkflowStateId,
            Status = "Active",
            CreatedDate = DateTime.UtcNow,
        };
        _context.WorkflowInstances.Add(instance);

        _context.WorkflowStepRecords.Add(new WorkflowStepRecord
        {
            WorkflowStepRecordId = Guid.NewGuid(),
            WorkflowInstanceId = instance.WorkflowInstanceId,
            WorkflowStateId = firstState.WorkflowStateId,
            StepStatus = "InProgress",
            StartedDate = DateTime.UtcNow,
        });

        fileMaster.WorkflowInstanceId = instance.WorkflowInstanceId;
        await _context.SaveChangesAsync();
        return instance;
    }

    public async Task<WorkflowInstance> AdvanceAsync(Guid fileMasterId, Guid? userId, string? notes)
    {
        var instance = await LoadInstanceAsync(fileMasterId);
        var currentState = await _context.WorkflowStates.FindAsync(instance.CurrentWorkflowStateId)
            ?? throw new InvalidOperationException("Current state not found.");

        if (currentState.IsTerminal)
            throw new InvalidOperationException($"Case is at terminal state '{currentState.StateName}'.");

        var nextState = await _context.WorkflowStates
            .Where(s => s.DisplayOrder > currentState.DisplayOrder)
            .OrderBy(s => s.DisplayOrder)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("No further states available.");

        return await MoveToStateAsync(instance, nextState, userId, notes);
    }

    public async Task<WorkflowInstance> TransitionToAsync(Guid fileMasterId, string targetStateName, Guid? userId, string? notes)
    {
        var instance = await LoadInstanceAsync(fileMasterId);
        var target = await _context.WorkflowStates.SingleOrDefaultAsync(s => s.StateName == targetStateName)
            ?? throw new InvalidOperationException($"Workflow state '{targetStateName}' not found.");
        return await MoveToStateAsync(instance, target, userId, notes);
    }

    public async Task<IReadOnlyList<WorkflowStepRecord>> GetHistoryAsync(Guid workflowInstanceId)
    {
        return await _context.WorkflowStepRecords
            .Include(s => s.WorkflowState)
            .Where(s => s.WorkflowInstanceId == workflowInstanceId)
            .OrderBy(s => s.StartedDate)
            .ToListAsync();
    }

    public async Task<WorkflowInstance?> GetInstanceForFileAsync(Guid fileMasterId)
    {
        return await _context.WorkflowInstances
            .Include(w => w.CurrentWorkflowState)
            .FirstOrDefaultAsync(w => w.FileMasterId == fileMasterId);
    }

    private async Task<WorkflowInstance> LoadInstanceAsync(Guid fileMasterId)
    {
        return await _context.WorkflowInstances
            .FirstOrDefaultAsync(w => w.FileMasterId == fileMasterId)
            ?? throw new InvalidOperationException($"No workflow instance for FileMaster {fileMasterId}.");
    }

    private async Task<WorkflowInstance> MoveToStateAsync(WorkflowInstance instance, WorkflowState target, Guid? userId, string? notes)
    {
        var currentStep = await _context.WorkflowStepRecords
            .Where(s => s.WorkflowInstanceId == instance.WorkflowInstanceId && s.StepStatus == "InProgress")
            .OrderByDescending(s => s.StartedDate)
            .FirstOrDefaultAsync();

        if (currentStep != null)
        {
            currentStep.StepStatus = "Completed";
            currentStep.CompletedDate = DateTime.UtcNow;
            currentStep.CompletedById = userId;
            currentStep.Notes = notes;
        }

        instance.CurrentWorkflowStateId = target.WorkflowStateId;
        if (target.IsTerminal)
        {
            instance.Status = "Completed";
            instance.CompletedDate = DateTime.UtcNow;
        }

        _context.WorkflowStepRecords.Add(new WorkflowStepRecord
        {
            WorkflowStepRecordId = Guid.NewGuid(),
            WorkflowInstanceId = instance.WorkflowInstanceId,
            WorkflowStateId = target.WorkflowStateId,
            StepStatus = target.IsTerminal ? "Completed" : "InProgress",
            StartedDate = DateTime.UtcNow,
            CompletedDate = target.IsTerminal ? DateTime.UtcNow : null,
            CompletedById = target.IsTerminal ? userId : null,
        });

        await _context.SaveChangesAsync();
        return instance;
    }
}
```

- [ ] **Step 5: Run the tests — expect PASS**

Run: `dotnet test --nologo --filter "FullyQualifiedName~WorkflowServiceTests"`
Expected: `Passed! - Failed: 0, Passed: 4`

- [ ] **Step 6: Commit**

```bash
git add Interfaces/IWorkflowService.cs Services/WorkflowService.cs Tests/Services/WorkflowServiceTests.cs
git commit -m "Add WorkflowService with start/advance/transition logic and tests"
```

---

## Task 3: Register `WorkflowService` in DI and auto-start workflow on `FileMaster` Create

**Files:**
- Modify: `Program.cs`
- Modify: `Controllers/FileMasterController.cs`

- [ ] **Step 1: Register the service in `Program.cs`**

In `Program.cs`, add after the existing `AddScoped<IForestation, ...>` line:

```csharp
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
```

- [ ] **Step 2: Inject into `FileMasterController`**

Modify `Controllers/FileMasterController.cs`. Change the constructor to inject `IWorkflowService`:

```csharp
public class FileMasterController : Controller
{
    private readonly IFileMaster _fileMasterRepository;
    private readonly ApplicationDBContext _context;
    private readonly IWorkflowService _workflow;

    public FileMasterController(IFileMaster fileMasterRepository, ApplicationDBContext context, IWorkflowService workflow)
    {
        _fileMasterRepository = fileMasterRepository;
        _context = context;
        _workflow = workflow;
    }
```

- [ ] **Step 3: Call `StartWorkflowAsync` after successful `Create`**

In the existing `Create(FileMaster)` POST action, replace the body inside `if (ModelState.IsValid)` with:

```csharp
if (ModelState.IsValid)
{
    var created = await _fileMasterRepository.AddAsync(fileMaster);
    await _workflow.StartWorkflowAsync(created.FileMasterId);
    return RedirectToAction(nameof(Details), new { id = created.FileMasterId });
}
```

- [ ] **Step 4: Build and confirm no compile errors**

Run: `dotnet build --nologo`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Run existing tests to confirm nothing broke**

Run: `dotnet test --nologo`
Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add Program.cs Controllers/FileMasterController.cs
git commit -m "Wire WorkflowService into DI and start workflow on FileMaster Create"
```

---

## Task 4: Seed sample properties, FileMasters, and workflow instances for demo

**Files:**
- Modify: `Services/SeedDataService.cs`

- [ ] **Step 1: Add `SeedSampleCasesAsync` call at the end of `SeedAsync`**

In `Services/SeedDataService.cs`, edit the `SeedAsync` method to append:

```csharp
public async Task SeedAsync()
{
    await SeedProvincesAsync();
    await SeedWaterManagementAreasAsync();
    await SeedWorkflowStatesAsync();
    await SeedLetterTypesAsync();
    await SeedAuthorisationTypesAsync();
    await SeedPeriodsAsync();
    await SeedGwcaProclamationRulesAsync();
    await SeedSampleCasesAsync(); // NEW
}
```

- [ ] **Step 2: Implement `SeedSampleCasesAsync` at the bottom of the class**

Append this method before the closing `}` of `SeedDataService`:

```csharp
// ── 8. Sample cases for demo ────────────────────────────────────

private async Task SeedSampleCasesAsync()
{
    if (await _context.FileMasters.AnyAsync())
        return;

    var mpumalanga = await _context.Provinces.SingleAsync(p => p.ProvinceCode == "MP");
    var inkomati = await _context.WaterManagementAreas.SingleAsync(w => w.WmaName == "Inkomati-Usuthu");

    // Catchment area if none exists
    var catchment = await _context.CatchmentAreas.FirstOrDefaultAsync(c => c.CatchmentCode == "X21A");
    if (catchment == null)
    {
        catchment = new CatchmentArea
        {
            CatchmentAreaId = Guid.NewGuid(),
            CatchmentCode = "X21A",
            CatchmentName = "Upper Komati Quaternary",
            WmaId = inkomati.WmaId,
        };
        _context.CatchmentAreas.Add(catchment);
    }

    // Org unit if none exists
    var orgUnit = await _context.OrganisationalUnits.FirstOrDefaultAsync(o => o.Name == "Mpumalanga Regional Office");
    if (orgUnit == null)
    {
        orgUnit = new OrganisationalUnit
        {
            OrgUnitId = Guid.NewGuid(),
            Name = "Mpumalanga Regional Office",
            Type = "Regional",
            ProvinceId = mpumalanga.ProvinceId,
            WmaId = inkomati.WmaId,
        };
        _context.OrganisationalUnits.Add(orgUnit);
    }

    var prop1 = new Property
    {
        PropertyId = Guid.NewGuid(),
        PropertyReferenceNumber = "DRN-123",
        SGCode = "T0HT00000000012300000",
        QuaternaryDrainage = "X21A",
        WmaId = inkomati.WmaId,
        CatchmentAreaId = catchment.CatchmentAreaId,
    };
    var prop2 = new Property
    {
        PropertyId = Guid.NewGuid(),
        PropertyReferenceNumber = "LWF-456",
        SGCode = "T0HT00000000045600000",
        QuaternaryDrainage = "X21A",
        WmaId = inkomati.WmaId,
        CatchmentAreaId = catchment.CatchmentAreaId,
    };
    _context.Properties.AddRange(prop1, prop2);

    await _context.SaveChangesAsync();

    var samples = new[]
    {
        new { Reg = "WARMS-2024-001", Farm = "Doornhoek",  FarmNo = 123, Portion = "0", Prop = prop1, TargetState = "CP1_WARMSObtained" },
        new { Reg = "WARMS-2024-002", Farm = "Leeuwfontein", FarmNo = 456, Portion = "1", Prop = prop2, TargetState = "CP5_GISAnalysis" },
        new { Reg = "WARMS-2024-003", Farm = "Doornhoek",  FarmNo = 123, Portion = "2", Prop = prop1, TargetState = "CP9_SFRACalculated" },
    };

    foreach (var s in samples)
    {
        var fm = new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            RegistrationNumber = s.Reg,
            CaseNumber = $"VV-2026-{s.Reg.Substring(s.Reg.Length - 3)}",
            PropertyId = s.Prop.PropertyId,
            OrgUnitId = orgUnit.OrgUnitId,
            CatchmentAreaId = catchment.CatchmentAreaId,
            SurveyorGeneralCode = s.Prop.SGCode!,
            PrimaryCatchment = "X",
            QuaternaryCatchment = "X21A",
            FarmName = s.Farm,
            FarmNumber = s.FarmNo,
            RegistrationDivision = "JR",
            FarmPortion = s.Portion,
            FileCreatedDate = DateOnly.FromDateTime(DateTime.Today),
            AssessmentTrack = "S35_Verification",
            ValidationStatusName = s.TargetState == "CP1_WARMSObtained" ? "Not Commenced" : "In Process",
            RegisteredForTakingWater = true,
            RegisteredForStoring = false,
            RegisteredForForestation = false,
        };
        _context.FileMasters.Add(fm);
        await _context.SaveChangesAsync();

        // Build workflow instance inline (avoid calling WorkflowService from here)
        var targetState = await _context.WorkflowStates.SingleAsync(w => w.StateName == s.TargetState);
        var firstState = await _context.WorkflowStates.OrderBy(w => w.DisplayOrder).FirstAsync();

        var instance = new WorkflowInstance
        {
            WorkflowInstanceId = Guid.NewGuid(),
            FileMasterId = fm.FileMasterId,
            CurrentWorkflowStateId = targetState.WorkflowStateId,
            Status = "Active",
            CreatedDate = DateTime.UtcNow.AddDays(-10),
        };
        _context.WorkflowInstances.Add(instance);

        // Synthesise history: Completed step per state from first up to (but not including) target, then InProgress for target.
        var traversed = await _context.WorkflowStates
            .Where(w => w.DisplayOrder < targetState.DisplayOrder)
            .OrderBy(w => w.DisplayOrder)
            .ToListAsync();

        var baseTime = DateTime.UtcNow.AddDays(-10);
        for (int i = 0; i < traversed.Count; i++)
        {
            _context.WorkflowStepRecords.Add(new WorkflowStepRecord
            {
                WorkflowStepRecordId = Guid.NewGuid(),
                WorkflowInstanceId = instance.WorkflowInstanceId,
                WorkflowStateId = traversed[i].WorkflowStateId,
                StepStatus = "Completed",
                StartedDate = baseTime.AddHours(i),
                CompletedDate = baseTime.AddHours(i + 1),
            });
        }
        _context.WorkflowStepRecords.Add(new WorkflowStepRecord
        {
            WorkflowStepRecordId = Guid.NewGuid(),
            WorkflowInstanceId = instance.WorkflowInstanceId,
            WorkflowStateId = targetState.WorkflowStateId,
            StepStatus = "InProgress",
            StartedDate = baseTime.AddHours(traversed.Count + 1),
        });

        fm.WorkflowInstanceId = instance.WorkflowInstanceId;
        await _context.SaveChangesAsync();
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build --nologo`
Expected: `0 Error(s)`. If errors about `CatchmentArea` or `OrganisationalUnit` property names, open the model files to confirm exact property names and adjust.

- [ ] **Step 4: Wipe local DB and re-seed**

Run:
```bash
dotnet ef database drop -f
dotnet ef database update
```
Expected: DB recreated, migrations applied.

- [ ] **Step 5: Start app and verify 3 cases appear**

Run: `dotnet run`
Open: `https://localhost:7xxx/FileMaster`
Expected: 3 sample cases — WARMS-2024-001/002/003 — visible in table. Stop app.

- [ ] **Step 6: Commit**

```bash
git add Services/SeedDataService.cs
git commit -m "Seed 3 sample cases at varying workflow stages for demo"
```

---

## Task 5: Add `FileMasterDetailsViewModel` and `GetWithWorkflowAsync` repository method

**Files:**
- Create: `ViewModels/FileMasterDetailsViewModel.cs`
- Modify: `Interfaces/IFileMaster.cs`
- Modify: `Repositories/FileMasterRepository.cs`

- [ ] **Step 1: Create the view model**

Create `ViewModels/FileMasterDetailsViewModel.cs`:

```csharp
public class FileMasterDetailsViewModel
{
    public required FileMaster FileMaster { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public List<WorkflowState> AllStates { get; set; } = new();
    public List<WorkflowStepRecord> History { get; set; } = new();
    public List<LetterIssuance> Letters { get; set; } = new();

    public WorkflowState? CurrentState => WorkflowInstance?.CurrentWorkflowState;

    public bool IsReadyForLetters =>
        CurrentState is { } s && (s.StateName == "CP9_SFRACalculated" || s.StateName.StartsWith("S35_"));

    public List<string> AvailableLetterActions
    {
        get
        {
            if (CurrentState == null) return new();
            return CurrentState.StateName switch
            {
                "CP9_SFRACalculated"         => new() { "IssueLetter1" },
                "S35_Letter1Issued"          => new() { "MarkLetter1Responded" },
                "S35_Letter1Responded"       => new() { "IssueLetter2", "IssueLetter3" },
                "S35_Letter2Issued"          => new() { "MarkLetter2Responded" },
                "S35_Letter2Responded"       => new() { "IssueLetter3" },
                "S35_Letter3Issued"          => new() { "MarkELUConfirmed" },
                "S35_ELUConfirmed"           => new() { "CloseCase" },
                _                            => new()
            };
        }
    }
}
```

- [ ] **Step 2: Add `GetWithWorkflowAsync` to `IFileMaster`**

In `Interfaces/IFileMaster.cs`, append this method:

```csharp
Task<FileMaster?> GetWithWorkflowAsync(Guid id);
```

- [ ] **Step 3: Implement in `FileMasterRepository`**

In `Repositories/FileMasterRepository.cs`, add:

```csharp
public async Task<FileMaster?> GetWithWorkflowAsync(Guid id)
{
    return await _context.FileMasters
        .Include(fm => fm.Property)
        .Include(fm => fm.OrgUnit)
        .Include(fm => fm.CatchmentArea)
        .Include(fm => fm.Validator)
        .Include(fm => fm.CapturePerson)
        .Include(fm => fm.Entitlement)
        .Include(fm => fm.LetterIssuances)
            .ThenInclude(l => l.LetterType)
        .FirstOrDefaultAsync(fm => fm.FileMasterId == id);
}
```

- [ ] **Step 4: Build**

Run: `dotnet build --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 5: Commit**

```bash
git add ViewModels/FileMasterDetailsViewModel.cs Interfaces/IFileMaster.cs Repositories/FileMasterRepository.cs
git commit -m "Add FileMasterDetailsViewModel and GetWithWorkflowAsync"
```

---

## Task 6: Extend `FileMasterController.Details` to return the view model, and add `AdvanceWorkflow` action

> **Atomicity warning:** Tasks 6 and 7 must NOT be left committed-but-deployed separately. Task 6 changes the `Details` action to return `FileMasterDetailsViewModel`; Task 7 changes the view to accept that model. Between the two, the app will throw a runtime cast error on every `/FileMaster/Details` hit. Commit both, or neither — or keep them in the same session without restarting the app between.

**Files:**
- Modify: `Controllers/FileMasterController.cs`

- [ ] **Step 1: Locate the existing `Details` action and replace it**

In `Controllers/FileMasterController.cs`, replace the current `Details(Guid id)` action with:

```csharp
[HttpGet]
public async Task<IActionResult> Details(Guid id)
{
    var fileMaster = await _fileMasterRepository.GetWithWorkflowAsync(id);
    if (fileMaster == null) return NotFound();

    var vm = new FileMasterDetailsViewModel { FileMaster = fileMaster };

    if (fileMaster.WorkflowInstanceId.HasValue)
    {
        vm.WorkflowInstance = await _context.WorkflowInstances
            .Include(w => w.CurrentWorkflowState)
            .FirstOrDefaultAsync(w => w.WorkflowInstanceId == fileMaster.WorkflowInstanceId);

        vm.History = await _workflow.GetHistoryAsync(fileMaster.WorkflowInstanceId.Value) is IReadOnlyList<WorkflowStepRecord> list
            ? list.ToList()
            : new List<WorkflowStepRecord>();
    }

    vm.AllStates = await _context.WorkflowStates.OrderBy(s => s.DisplayOrder).ToListAsync();
    vm.Letters = fileMaster.LetterIssuances.OrderBy(l => l.IssuedDate).ToList();

    return View(vm);
}
```

- [ ] **Step 2: Add the `AdvanceWorkflow` POST action**

Append to `FileMasterController`:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AdvanceWorkflow(Guid id, string? notes)
{
    try
    {
        await _workflow.AdvanceAsync(id, userId: null, notes: notes);
    }
    catch (InvalidOperationException ex)
    {
        TempData["WorkflowError"] = ex.Message;
    }
    return RedirectToAction(nameof(Details), new { id });
}
```

- [ ] **Step 3: Build**

Run: `dotnet build --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 4: Commit**

```bash
git add Controllers/FileMasterController.cs
git commit -m "Details returns ViewModel; add AdvanceWorkflow action"
```

---

## Task 7: Rewrite `Details.cshtml` and add `_WorkflowPanel.cshtml` partial

**Files:**
- Replace: `Views/FileMaster/Details.cshtml`
- Create: `Views/FileMaster/_WorkflowPanel.cshtml`

- [ ] **Step 1: Replace `Details.cshtml` to use the new view model**

Replace the full contents of `Views/FileMaster/Details.cshtml` with:

```cshtml
@model FileMasterDetailsViewModel

@{
    ViewData["Title"] = "Case Details";
    var fm = Model.FileMaster;
    var badgeClass = fm.ValidationStatusName switch
    {
        "Completed" => "badge-green",
        "In Process" => "badge-blue",
        _ => "badge-grey"
    };
}

<a asp-controller="FileMaster" asp-action="Index" class="btn-back">&larr; Back to V&amp;V Cases</a>

<div class="detail-header">
    <div>
        <div class="page-title">@fm.RegistrationNumber</div>
        <div class="page-subtitle">@fm.FarmName · Case @fm.CaseNumber</div>
    </div>
    <div>
        <span class="badge @badgeClass">@(fm.ValidationStatusName ?? "Not Commenced")</span>
    </div>
</div>

@if (TempData["WorkflowError"] != null)
{
    <div class="alert alert-error">@TempData["WorkflowError"]</div>
}

@await Html.PartialAsync("_WorkflowPanel", Model)

@if (Model.IsReadyForLetters)
{
    @await Html.PartialAsync("_LettersPanel", Model)
}

<div class="card" style="max-width: 900px; margin-top: 20px;">
    <div class="form-section-title">Case Information</div>
    <div class="form-row">
        <div class="form-group">
            <div class="kv"><div class="form-label">Surveyor General Code</div><div>@fm.SurveyorGeneralCode</div></div>
            <div class="kv"><div class="form-label">Primary Catchment</div><div>@fm.PrimaryCatchment</div></div>
            <div class="kv"><div class="form-label">Quaternary Catchment</div><div>@fm.QuaternaryCatchment</div></div>
            <div class="kv"><div class="form-label">Assessment Track</div><div>@(fm.AssessmentTrack ?? "--")</div></div>
            <div class="kv"><div class="form-label">Farm Number</div><div>@fm.FarmNumber</div></div>
            <div class="kv"><div class="form-label">Registration Division</div><div>@fm.RegistrationDivision</div></div>
            <div class="kv"><div class="form-label">Farm Portion</div><div>@fm.FarmPortion</div></div>
        </div>
        <div class="form-group">
            <div class="kv"><div class="form-label">Property</div><div>@(fm.Property?.PropertyReferenceNumber ?? "--")</div></div>
            <div class="kv"><div class="form-label">Organisational Unit</div><div>@(fm.OrgUnit?.Name ?? "--")</div></div>
            <div class="kv"><div class="form-label">Catchment Area</div><div>@(fm.CatchmentArea?.CatchmentCode ?? "--")</div></div>
            <div class="kv"><div class="form-label">File Created</div><div>@fm.FileCreatedDate.ToString("dd MMM yyyy")</div></div>
        </div>
    </div>

    <div style="margin-top: 20px; display: flex; gap: 8px;">
        <a asp-action="Edit" asp-route-id="@fm.FileMasterId" class="btn btn-primary">Edit</a>
        <a asp-action="Delete" asp-route-id="@fm.FileMasterId" class="btn btn-danger">Delete</a>
    </div>
</div>
```

- [ ] **Step 2: Create `_WorkflowPanel.cshtml`**

Create `Views/FileMaster/_WorkflowPanel.cshtml`:

```cshtml
@model FileMasterDetailsViewModel

@if (Model.WorkflowInstance == null)
{
    <div class="card" style="max-width: 900px; margin-top: 16px;">
        <div class="form-section-title">Workflow</div>
        <div style="color:#6c757d; font-size:13px;">No workflow started for this case.</div>
    </div>
}
else
{
    <div class="card" style="max-width: 900px; margin-top: 16px;">
        <div class="flex-between">
            <div class="form-section-title" style="margin:0;">V&amp;V Workflow</div>
            @if (Model.CurrentState != null && !Model.CurrentState.IsTerminal && !Model.IsReadyForLetters)
            {
                <form asp-action="AdvanceWorkflow" asp-route-id="@Model.FileMaster.FileMasterId" method="post" style="margin:0;">
                    @Html.AntiForgeryToken()
                    <button type="submit" class="btn btn-primary btn-sm">Advance to Next CP &rarr;</button>
                </form>
            }
        </div>

        <div class="phase-tracker">
            @foreach (var phase in new[] { "Inception", "Validation", "Verification" })
            {
                var statesInPhase = Model.AllStates.Where(s => s.Phase == phase).OrderBy(s => s.DisplayOrder).ToList();
                var currentInPhase = Model.CurrentState != null && Model.CurrentState.Phase == phase;
                <div class="phase-segment @(currentInPhase ? "phase-active" : "")">
                    <div class="phase-label">Phase @(Array.IndexOf(new[] { "Inception", "Validation", "Verification" }, phase) + 1): @phase</div>
                    <div class="cp-row">
                        @foreach (var st in statesInPhase)
                        {
                            var isCurrent = Model.CurrentState?.WorkflowStateId == st.WorkflowStateId;
                            var isPast = Model.CurrentState != null && st.DisplayOrder < Model.CurrentState.DisplayOrder;
                            var cls = isCurrent ? "cp-pill current" : isPast ? "cp-pill past" : "cp-pill future";
                            <span class="@cls" title="@st.StateName">@st.StateName</span>
                        }
                    </div>
                </div>
            }
        </div>

        <div class="current-state-line">
            <strong>Current:</strong>
            @(Model.CurrentState?.StateName ?? "--")
            @if (Model.CurrentState?.IsTerminal == true)
            {
                <span class="badge badge-green" style="margin-left:8px;">Closed</span>
            }
        </div>

        <div class="form-section-title" style="margin-top:20px;">History</div>
        <div class="timeline">
            @foreach (var step in Model.History.OrderByDescending(h => h.StartedDate))
            {
                <div class="timeline-item @(step.StepStatus == "InProgress" ? "in-progress" : "")">
                    <div class="timeline-state">@(step.WorkflowState?.StateName ?? "--")</div>
                    <div class="timeline-meta">
                        @step.StepStatus ·
                        Started @step.StartedDate?.ToString("dd MMM yyyy HH:mm")
                        @if (step.CompletedDate.HasValue)
                        {
                            <span> · Completed @step.CompletedDate.Value.ToString("dd MMM yyyy HH:mm")</span>
                        }
                        @if (!string.IsNullOrWhiteSpace(step.Notes))
                        {
                            <span> · @step.Notes</span>
                        }
                    </div>
                </div>
            }
        </div>
    </div>
}
```

- [ ] **Step 3: Append styles to `wwwroot/css/site.css`**

Append to the end of `wwwroot/css/site.css`:

```css
/* ── Workflow Panel ───────────────────────────────────────── */
.phase-tracker { display: flex; gap: 8px; margin: 16px 0; flex-wrap: wrap; }
.phase-segment { flex: 1; min-width: 260px; padding: 10px 12px; border: 1px solid #d0d5db; border-radius: 6px; background: #fafbfc; }
.phase-segment.phase-active { border-color: var(--dws-blue); background: #eef4fb; }
.phase-label { font-size: 11px; font-weight: 700; color: var(--dws-blue); letter-spacing: 0.5px; margin-bottom: 8px; text-transform: uppercase; }
.cp-row { display: flex; flex-wrap: wrap; gap: 4px; }
.cp-pill { display: inline-block; padding: 3px 8px; font-size: 10px; border-radius: 10px; border: 1px solid #c7ccd1; background: #fff; color: #495057; }
.cp-pill.past { background: #e9ecef; color: #6c757d; }
.cp-pill.current { background: var(--dws-blue); color: #fff; border-color: var(--dws-blue); font-weight: 700; }
.cp-pill.future { background: #fff; color: #adb5bd; }
.current-state-line { padding: 10px 12px; background: #f8f9fa; border-left: 3px solid var(--dws-blue); font-size: 13px; margin-top: 4px; }

/* Timeline */
.timeline { border-left: 2px solid #e1e4e8; padding-left: 16px; margin-top: 8px; }
.timeline-item { padding: 8px 0; position: relative; }
.timeline-item::before { content: ""; position: absolute; left: -21px; top: 14px; width: 8px; height: 8px; border-radius: 50%; background: #adb5bd; }
.timeline-item.in-progress::before { background: var(--dws-blue); box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.2); }
.timeline-state { font-weight: 600; font-size: 13px; }
.timeline-meta { font-size: 11px; color: #6c757d; margin-top: 2px; }

/* Alerts */
.alert { padding: 10px 14px; border-radius: 4px; font-size: 13px; margin: 10px 0; }
.alert-error { background: #fdecea; color: #b91c1c; border: 1px solid #f5c6cb; }

/* Key-value helper */
.kv { margin-bottom: 16px; }
```

- [ ] **Step 4: Run the app and verify the workflow panel**

Run: `dotnet run`
Open: `https://localhost:7xxx/FileMaster`, click into **WARMS-2024-001**.
Expected:
- Phase tracker visible with 3 phase segments
- Current state highlighted (e.g. `CP1_WARMSObtained`)
- "Advance to Next CP" button
- History shows 1 `InProgress` step

Click **Advance to Next CP** — state should move to `CP1_SatelliteImagery`. History should grow.
Click into **WARMS-2024-003** — should be at `CP9_SFRACalculated`. **Advance button should NOT appear** (IsReadyForLetters is true, and Letters panel will render in Task 8). For now a missing `_LettersPanel` partial will throw — move straight to Task 8.

Stop app.

- [ ] **Step 5: Commit**

```bash
git add Views/FileMaster/Details.cshtml Views/FileMaster/_WorkflowPanel.cshtml wwwroot/css/site.css
git commit -m "Add workflow panel to FileMaster Details with phase tracker and history"
```

---

## Task 8: Add `_LettersPanel.cshtml` and letter-issuance controller actions

**Files:**
- Create: `Views/FileMaster/_LettersPanel.cshtml`
- Modify: `Controllers/FileMasterController.cs`

- [ ] **Step 1: Create the Letters partial**

Create `Views/FileMaster/_LettersPanel.cshtml`:

```cshtml
@model FileMasterDetailsViewModel

<div class="card" style="max-width: 900px; margin-top: 16px;">
    <div class="form-section-title">Section 35 Letters</div>

    <div class="letter-actions">
        @foreach (var action in Model.AvailableLetterActions)
        {
            var (label, cssClass) = action switch
            {
                "IssueLetter1"          => ("Issue Letter 1 (S35(1) Notice)", "btn btn-primary"),
                "MarkLetter1Responded"  => ("Mark Letter 1 Responded", "btn btn-outline"),
                "IssueLetter2"          => ("Issue Letter 2 (S35(3)(a) Info Request)", "btn btn-primary"),
                "MarkLetter2Responded"  => ("Mark Letter 2 Responded", "btn btn-outline"),
                "IssueLetter3"          => ("Issue Letter 3 (S35(4) ELU Confirmation)", "btn btn-primary"),
                "MarkELUConfirmed"      => ("Confirm ELU Determination", "btn btn-primary"),
                "CloseCase"             => ("Close Case", "btn btn-outline"),
                _                       => (action, "btn btn-outline")
            };

            if (action.StartsWith("Issue"))
            {
                <form asp-action="IssueLetter" asp-route-id="@Model.FileMaster.FileMasterId" method="post" class="letter-inline-form">
                    @Html.AntiForgeryToken()
                    <input type="hidden" name="letterAction" value="@action" />
                    <input type="text" name="recipient" placeholder="Recipient" class="form-input form-input-sm" required />
                    <select name="deliveryMethod" class="form-select form-select-sm">
                        <option value="RegisteredPost">Registered Post</option>
                        <option value="InPerson">In Person (S35(2)(d))</option>
                        <option value="Email">Email</option>
                    </select>
                    <input type="date" name="issuedDate" value="@DateTime.Today.ToString("yyyy-MM-dd")" class="form-input form-input-sm" />
                    <button type="submit" class="@cssClass btn-sm">@label</button>
                </form>
            }
            else
            {
                <form asp-action="MarkLetterResponse" asp-route-id="@Model.FileMaster.FileMasterId" method="post" style="display:inline-block; margin-right:8px;">
                    @Html.AntiForgeryToken()
                    <input type="hidden" name="letterAction" value="@action" />
                    <button type="submit" class="@cssClass btn-sm">@label</button>
                </form>
            }
        }
    </div>

    @if (Model.Letters.Any())
    {
        <div class="data-table-wrap" style="margin-top: 16px;">
            <table>
                <thead>
                    <tr>
                        <th>Letter</th><th>NWA</th><th>Issued</th><th>Method</th><th>Recipient</th><th>Response</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var l in Model.Letters)
                    {
                        <tr>
                            <td>@(l.LetterType?.LetterName ?? "--")</td>
                            <td>@(l.LetterType?.NWASection ?? "--")</td>
                            <td>@(l.IssuedDate?.ToString("dd MMM yyyy") ?? "--")</td>
                            <td>@(l.IssueMethod ?? "--")</td>
                            <td>@(l.ServingOfficialName ?? "--")</td>
                            <td>@(l.ResponseStatus ?? "Pending")</td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    }
</div>
```

- [ ] **Step 2: Append styles to `wwwroot/css/site.css`**

```css
/* ── Letters Panel ────────────────────────────────────────── */
.letter-actions { display: flex; flex-direction: column; gap: 10px; margin: 12px 0; }
.letter-inline-form { display: flex; gap: 6px; align-items: center; flex-wrap: wrap; }
.form-input-sm, .form-select-sm { padding: 4px 8px; font-size: 12px; border: 1px solid #d0d5db; border-radius: 4px; }
.btn-sm { padding: 4px 10px; font-size: 12px; }
```

- [ ] **Step 3: Add `IssueLetter` and `MarkLetterResponse` actions to `FileMasterController`**

Append to `FileMasterController`:

```csharp
private static readonly Dictionary<string, (string LetterName, string TargetState)> LetterActionMap = new()
{
    ["IssueLetter1"] = ("Letter 1", "S35_Letter1Issued"),
    ["IssueLetter2"] = ("Letter 2", "S35_Letter2Issued"),
    ["IssueLetter3"] = ("Letter 3", "S35_Letter3Issued"),
};

private static readonly Dictionary<string, string> ResponseActionMap = new()
{
    ["MarkLetter1Responded"] = "S35_Letter1Responded",
    ["MarkLetter2Responded"] = "S35_Letter2Responded",
    ["MarkELUConfirmed"]     = "S35_ELUConfirmed",
    ["CloseCase"]            = "Closed",
};

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> IssueLetter(Guid id, string letterAction, string recipient, string deliveryMethod, DateTime issuedDate)
{
    if (!LetterActionMap.TryGetValue(letterAction, out var map))
    {
        TempData["WorkflowError"] = $"Unknown letter action '{letterAction}'.";
        return RedirectToAction(nameof(Details), new { id });
    }

    var letterType = await _context.LetterTypes.SingleOrDefaultAsync(t => t.LetterName == map.LetterName);
    if (letterType == null)
    {
        TempData["WorkflowError"] = $"Letter type '{map.LetterName}' not seeded.";
        return RedirectToAction(nameof(Details), new { id });
    }

    _context.LetterIssuances.Add(new LetterIssuance
    {
        LetterIssuanceId = Guid.NewGuid(),
        FileMasterId = id,
        LetterTypeId = letterType.LetterTypeId,
        IssuedDate = DateOnly.FromDateTime(issuedDate),
        IssueMethod = deliveryMethod,
        ServingOfficialName = recipient,
        ResponseStatus = "Pending",
        DueDate = DateOnly.FromDateTime(issuedDate.AddDays(60)),
    });
    await _context.SaveChangesAsync();

    try
    {
        await _workflow.TransitionToAsync(id, map.TargetState, userId: null, notes: $"{map.LetterName} issued to {recipient}");
    }
    catch (InvalidOperationException ex)
    {
        TempData["WorkflowError"] = ex.Message;
    }

    return RedirectToAction(nameof(Details), new { id });
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> MarkLetterResponse(Guid id, string letterAction)
{
    if (!ResponseActionMap.TryGetValue(letterAction, out var targetState))
    {
        TempData["WorkflowError"] = $"Unknown response action '{letterAction}'.";
        return RedirectToAction(nameof(Details), new { id });
    }

    var fm = await _context.FileMasters
        .Include(f => f.LetterIssuances)
        .FirstOrDefaultAsync(f => f.FileMasterId == id);
    if (fm == null) return NotFound();

    var latestPending = fm.LetterIssuances
        .Where(l => l.ResponseStatus == "Pending")
        .OrderByDescending(l => l.IssuedDate)
        .FirstOrDefault();
    if (latestPending != null)
    {
        latestPending.ResponseDate = DateOnly.FromDateTime(DateTime.Today);
        latestPending.ResponseStatus = "Agreed";
        latestPending.AgreedWithFindings = true;
    }
    await _context.SaveChangesAsync();

    try
    {
        await _workflow.TransitionToAsync(id, targetState, userId: null, notes: $"State transitioned to {targetState}");
    }
    catch (InvalidOperationException ex)
    {
        TempData["WorkflowError"] = ex.Message;
    }

    return RedirectToAction(nameof(Details), new { id });
}
```

- [ ] **Step 4: Build**

Run: `dotnet build --nologo`
Expected: `0 Error(s)`.

- [ ] **Step 5: Full smoke test in the browser**

Run: `dotnet run`

Demo walkthrough — uses case **2** (mid-flow) for speed, then case **3** (letters):

Part 1 — watch workflow advance (5 clicks, ~30s):
1. Open `https://localhost:7xxx/FileMaster`
2. Click **WARMS-2024-002** → workflow panel shows state `CP5_GISAnalysis`
3. Click **Advance to Next CP** five times → state moves through `CP6_FieldCropSAPWAT`, `CP7_ELUCalculated`, `CP8_DamVolumes`, arriving at `CP9_SFRACalculated`
4. At `CP9_SFRACalculated` the Advance button disappears and the Letters panel appears
5. (Optional) Show the tracker: current pill is highlighted, past pills are greyed, phase 3 segment is active

Part 2 — Section 35 letter flow on case **3** (already at CP9):
6. Navigate back and click **WARMS-2024-003** → state is `CP9_SFRACalculated`, Letters panel shows Issue Letter 1 form
7. Fill form (recipient: "J. Smith", delivery: "In Person", today's date) → submit
8. State transitions to `S35_Letter1Issued`; row appears in Letters table
9. Click **Mark Letter 1 Responded** → state moves to `S35_Letter1Responded`
10. Click **Issue Letter 3 (S35(4) ELU Confirmation)** (skipping Letter 2 for brevity) → state becomes `S35_Letter3Issued`
11. Click **Confirm ELU Determination** → state becomes `S35_ELUConfirmed`
12. Click **Close Case** → state becomes `Closed`, terminal badge appears

Quick fallback for case 1: if the client wants to see the full walk starting at CP1, click into **WARMS-2024-001** — 14 advances to CP9, then step into letter flow.

Stop app.

- [ ] **Step 6: Run all tests**

Run: `dotnet test --nologo`
Expected: all tests pass (existing 33 + 4 new WorkflowService tests = 37).

- [ ] **Step 7: Commit**

```bash
git add Views/FileMaster/_LettersPanel.cshtml wwwroot/css/site.css Controllers/FileMasterController.cs
git commit -m "Add Letters panel and issue/response actions for S35 flow"
```

---

## Task 9: Final smoke test and demo rehearsal

**Files:** none (dry-run only).

- [ ] **Step 1: Fresh DB reset to guarantee clean demo state**

Run:
```bash
dotnet ef database drop -f
dotnet ef database update
```

- [ ] **Step 2: Run the app and walk the demo script once, start to finish**

Start app, execute the 10-step demo walkthrough from Task 8 Step 5. Time it — should be under 5 minutes of clicking.

- [ ] **Step 3: Check for UI glitches on each page**

- Properties list renders
- V&V Cases list shows 3 rows
- Case detail shows workflow panel and (for case 3) letters panel
- No visible console errors (open browser devtools)

- [ ] **Step 4: Reset DB one more time before the client meeting**

Run: `dotnet ef database drop -f && dotnet ef database update`
Keep app running for the meeting.

- [ ] **Step 5: No commit — demo ready**

---

## Success Criteria

After all tasks complete:
- `dotnet test` passes with 37+ tests
- A freshly-seeded DB produces 3 sample cases at 3 different workflow stages
- Clicking "Advance" transitions state and writes a `WorkflowStepRecord`
- Clicking "Issue Letter 1" creates a `LetterIssuance` row and transitions to `S35_Letter1Issued`
- The end-to-end demo reaches terminal state `Closed` without errors

## Fallback if time runs out

If Task 8 can't complete in time: stop at Task 7. Demo story becomes "watch the workflow advance through 30+ states". Letters panel gets cut — still a credible demo.

If Task 4 sample data breaks due to property-name mismatch on `CatchmentArea` or `OrganisationalUnit`: fall back to just creating properties + FileMasters without those FKs, let them be `null`.
