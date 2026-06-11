# Task: WF-01 — concurrency control on workflow transitions (Slice 2)

**Start:** 2026-06-08
**Branch:** fix/remediation-wave4 (main working tree)
**Plan:** docs/2026-06-08-build-status-report.html → Tier 1 step 1 (WF-01). 3 June validation finding: "No concurrency control on workflow transitions. Concurrent advances both pass guards → double-transition / duplicate step records (no RowVersion on WorkflowInstance)."

**Goal:** Make concurrent workflow transitions safe — two simultaneous advances on the same case must not both apply. One wins; the other fails cleanly (no duplicate step record, no double state change).

## Design (controller, pre-implementation)
- **Chokepoint:** both `AdvanceAsync` and `TransitionToAsync` funnel through `WorkflowService.MoveToStateAsync` (Services/WorkflowService.cs:233), which mutates `instance.CurrentWorkflowStateId` + adds a `WorkflowStepRecord` and commits via a SINGLE `SaveChangesAsync` (line 286). `instance` is change-tracked (loaded via `LoadInstanceAsync` → `FirstOrDefaultAsync`).
- **Fix:** add a `RowVersion` concurrency token to `WorkflowInstance`; SQL Server `rowversion` is bumped on every UPDATE, so the single `SaveChangesAsync` will throw `DbUpdateConcurrencyException` when a concurrent transition already moved the instance. Catch it in `MoveToStateAsync` and rethrow a clear domain exception so the controller/UI can say "this case was just advanced by someone else — refresh and retry." The losing transition's audit log never fires (audit is after SaveChanges).
- **EF config:** per repo convention, configure in `OnModelCreating` (`.Property(x => x.RowVersion).IsRowVersion()`), NOT a `[Timestamp]` annotation. Add `byte[] RowVersion` to the model (store-generated; never set manually).

## Edge cases (enumerated up front — planning discipline)
1. **EF-InMemory ignores concurrency tokens.** Existing `WorkflowServiceTests` use `TestDbContextFactory` (InMemory) → they will NOT break (token ignored) but also CANNOT prove the fix. The race MUST be proven against real SQL Server.
2. **Do NOT add `Database.BeginTransactionAsync` ungated.** InMemory throws on BeginTransaction → would break existing InMemory tests. The single `SaveChangesAsync` is already atomic; rowversion on it is the WF-01 fix. If an explicit transaction is added at all, gate it on `_context.Database.IsRelational()`. (Multi-write transactionality is WF-02's concern, a later slice.)
3. **`RowVersion` nullability:** on InMemory the store-generated value stays null/empty — ensure the property/config doesn't force non-null in a way that breaks InMemory inserts (use `byte[] RowVersion { get; set; } = Array.Empty<byte>();` or `byte[]?`).
4. **Domain exception:** surface a specific exception (e.g. `WorkflowConcurrencyException : InvalidOperationException`) so callers can distinguish a concurrency conflict from a guard denial.

## Acceptance criteria
- `WorkflowInstance.RowVersion` added + `IsRowVersion()` configured in `OnModelCreating`.
- A new EF migration adds the rowversion column (`dotnet ef migrations add AddWorkflowInstanceRowVersion`).
- `MoveToStateAsync` catches `DbUpdateConcurrencyException` → throws the domain concurrency exception.
- NEW SQL-Server-backed test proves: two contexts load the same instance, both advance, the second `SaveChangesAsync` throws `DbUpdateConcurrencyException` (and/or `MoveToStateAsync` surfaces the domain exception) — exactly one step record / one state change results.
- FULL existing suite stays green (495/495) — InMemory tests unaffected.
- Solution builds clean.

## Off-limits
Production code unrelated to the transition path; the existing `Tests/` InMemory helpers' behaviour; `Tests.E2E/`; `.worktrees/`, `.claude/worktrees/`.

## Journal

### 2026-06-08 — controller — task setup + design
- Read WorkflowInstance.cs, WorkflowService (AdvanceAsync/TransitionToAsync/MoveToStateAsync/LoadInstanceAsync), ApplicationDBContext WorkflowInstance config, WorkflowServiceTests (InMemory), TestDbContextFactory, IntegrationTestFixture (real SQL path).
- Design above. Single serial implementer (EF specialist); controller verifies (build + full suite + new SQL test).
- Status: DONE (setup)

### 2026-06-08 — implementer — WF-01 implementation complete

**Files changed:**
- `Models/WorkflowInstance.cs` — added `byte[] RowVersion` with `= Array.Empty<byte>()` init
- `DatabaseContexts/ApplicationDBContext.cs` — added `.Property(e => e.RowVersion).IsRowVersion()` after `HasKey` for `WorkflowInstance`
- `Services/Workflow/WorkflowConcurrencyException.cs` — NEW; `sealed class WorkflowConcurrencyException : InvalidOperationException`; message: "This case was advanced by another user. Refresh and retry."
- `Services/WorkflowService.cs` — wrapped `await _context.SaveChangesAsync()` in `MoveToStateAsync` with `try/catch DbUpdateConcurrencyException` → rethrows as `WorkflowConcurrencyException` (with inner exception)
- `Migrations/20260608135805_AddWorkflowInstanceRowVersion.cs` — generated; `Up` adds `type: "rowversion", rowVersion: true, nullable: false`; `Down` drops the column
- `Migrations/ApplicationDBContextModelSnapshot.cs` — updated by EF; snapshot shows `IsConcurrencyToken()`, `ValueGeneratedOnAddOrUpdate()`, `HasColumnType("rowversion")`
- `Tests/Integration/WorkflowConcurrencyTests.cs` — NEW SQL-Server-backed concurrency test (database: `dwa_val_ver_wftest`); two tests: (1) DbContext-level: stale RowVersion → `DbUpdateConcurrencyException`; (2) service-level: two `WorkflowService` instances race on `AdvanceAsync` → exactly one wins, loser throws `WorkflowConcurrencyException`, step count = 2 (not 4)
- `dwa_ver_val.csproj` — added explicit `Microsoft.EntityFrameworkCore.Design 10.0.7` reference with `PrivateAssets=all`

**Migration name:** `AddWorkflowInstanceRowVersion`
**Domain exception type:** `dwa_ver_val.Services.Workflow.WorkflowConcurrencyException : InvalidOperationException`
**New tests:** `WorkflowConcurrencyTests.RowVersion_StaleContext_ThrowsDbUpdateConcurrencyException` and `ServiceAdvance_ConcurrentCall_ExactlyOneSucceeds_OtherThrowsWorkflowConcurrencyException`

**Surprise / non-obvious:** EF Tools 10.0.7 global tool updated fine, but the project's bin contained a stale `Microsoft.EntityFrameworkCore.Design 8.0.0` DLL from a prior EF 8 build. Incremental builds didn't replace it because `Tools` has `PrivateAssets=all` and doesn't ship a Design.dll — it relies on the `Design` package separately. The 8.0.0 version was being pulled in as a transitive dependency (QuestPDF or a related package). Fix: added an explicit `Microsoft.EntityFrameworkCore.Design 10.0.7` with `PrivateAssets=all` to `dwa_ver_val.csproj` — this pins the version and ensures clean builds from here. The pre-existing dotnet-ef tools 10.0.3→10.0.7 version mismatch also needed fixing; updated global tool.

**Step-count arithmetic (for the record):** after one `AdvanceAsync`, `MoveToStateAsync` updates the existing InProgress record to Completed (no insert) and inserts one new InProgress record. So 1 initial + 0 net from the update + 1 insert = 2 records total. A double-advance would produce 3.

**Build:** 0 errors, 15 warnings (all pre-existing).
**Test result:** 497 passed, 0 failed (baseline was 495; added 2 new SQL concurrency tests).

### 2026-06-08 — controller — verified handoff (Rule 4)
- Reviewed diffs myself: csproj EF.Design pin is PrivateAssets=all (build-only, no runtime ship) — accepted; MoveToStateAsync catch→WorkflowConcurrencyException clean; migration Up adds rowversion column. New files (exception + SQL test) present.
- Ran independently: `dotnet build` 0 errors; `dotnet test Tests` → **497 passed / 0 failed** (incl. the 2 new SQL concurrency tests); `dotnet test Tests.E2E` → **12 passed / 0 failed** (confirms the new migration applies cleanly to dwa_val_ver_e2e on startup). 509 green total.
- Status: DONE (WF-01 verified GREEN)

## Retro (on completion)
_Completed cleanly. All acceptance criteria met. The EF Design DLL staleness issue is the most important non-obvious finding — future maintainers should be aware that an `EnsureCreated`/migration command failure with `MissingMethodException` or `TypeLoadException` citing an old EF version is a stale-bin problem, not a code bug. The fix (explicit Design package pin) is now in the csproj._
