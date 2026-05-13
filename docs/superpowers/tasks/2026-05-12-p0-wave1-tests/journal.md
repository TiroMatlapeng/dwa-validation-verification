# Task: P0 Wave 1 — Repository Tests (FieldAndCrop, Forestation, DamCalculation)

**Start:** 2026-05-12 10:30 SAST
**Branch:** demo/azure-deploy
**Worktree:** none — all agents work directly on the branch in /Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai
**Plan:** docs/superpowers/specs/2026-05-12-p0-wave1-design.md
**Contract docs in scope:** none

**Acceptance criteria** (what "done" looks like):
- `Tests/Repositories/FieldAndCropRepositoryTests.cs` exists with ≥3 passing xUnit tests covering AddFieldAndCrop, GetByPropertyIdAsync, DeleteAsync
- `Tests/Repositories/ForestationRepositoryTests.cs` exists with ≥3 passing xUnit tests covering RegisterForestation, GetByPropertyIdAsync, DeleteAsync
- `Tests/Repositories/DamCalculationRepositoryTests.cs` exists with ≥3 passing xUnit tests covering AddCalculationAsync, GetByPropertyIdAsync, DeleteAsync
- `dotnet test` passes all three new test classes (tests must not depend on a live SQL Server — use TestDbContextFactory with InMemory)
- Total failing test count stays at 25 (no regressions introduced)

**Out of scope** (must not be touched):
- Any existing test files
- Any production code (Controllers, Repositories, Interfaces, Models, Views, Program.cs)
- Any other test class
- The FileMaster, Property, Workflow, or portal test infrastructure

---

## Journal

> Each dispatched agent appends one entry. Read ALL prior entries before editing. Entries are terse bullets, file:line references, no narrative.

<!-- Agent entries go here -->

### 2026-05-12 — dotnet-master (fieldandcrop-tests)
- Branch verified: `demo/azure-deploy`, cwd `/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai`
- Created: `Tests/Repositories/FieldAndCropRepositoryTests.cs` (4 [Fact] tests)
- Tests: `AddFieldAndCrop_Persists_To_Database`, `GetByPropertyIdAsync_Returns_Only_Matching_Property`, `DeleteAsync_Removes_Entity`, `GetByIdAsync_Returns_Correct_Entity`
- Pattern: plain class (no IClassFixture), private static `SeedLookups` + `BuildFieldAndCrop` helpers, `TestDbContextFactory.Create()` per test (unique InMemory DB per test)
- Build: `dotnet build Tests/dwa_ver_val.Tests.csproj` — 0 errors, 6 pre-existing warnings (not introduced here)
- Run: `dotnet test --filter "FullyQualifiedName~FieldAndCropRepositoryTests"` — 4/4 passed, 910ms
- Note: `FieldAndCrop` model uses `required` for `Property`, `Period`, `Crop`, `WaterSource` navs — setting the nav property is mandatory at construction; the `PropertyId`/`PeriodId` scalars are also set explicitly so `GetByPropertyIdAsync` filters work under InMemory.
- Production code: untouched. No edits outside `Tests/Repositories/FieldAndCropRepositoryTests.cs`.

### 2026-05-12 — dotnet-master (forestation-tests)
- Branch verified: `demo/azure-deploy`, cwd `/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai`
- Created: `Tests/Repositories/ForestationRepositoryTests.cs` (3 [Fact] tests)
- Tests: `RegisterForestation_Persists_To_Database`, `GetByPropertyIdAsync_Returns_Only_Matching_Property`, `DeleteAsync_Removes_Entity`
- Pattern: plain class (no `IClassFixture`); private static `SeedProperty` helper; fresh `TestDbContextFactory.Create()` per `[Fact]`; types referenced via global namespace (matches `Models/Forestation.cs`, `Repositories/ForestationRepository.cs` — neither declares a namespace)
- `Forestation` requires `Property` nav + `RegisteredHectares` + `RegisteredVolume`; `PropertyId` scalar set explicitly so `GetByPropertyIdAsync` filter works under InMemory
- Build: `dotnet build Tests/dwa_ver_val.Tests.csproj` — 0 errors, 1 pre-existing warning at `Tests/Services/Auth/DwsClaimsTransformationTests.cs:119` (xUnit2031, not introduced here)
- Run: `dotnet test --filter "FullyQualifiedName~ForestationRepositoryTests"` — 3/3 passed, 727ms
- Production code: untouched. No edits outside `Tests/Repositories/ForestationRepositoryTests.cs` and this journal.

### 2026-05-12 — dotnet-master (damcalc-tests)
- Branch verified: `demo/azure-deploy`, cwd `/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai`
- Created: `Tests/Repositories/DamCalculationRepositoryTests.cs` (4 [Fact] tests; acceptance required ≥3)
- Tests: `AddCalculationAsync_Persists_To_Database`, `GetByPropertyIdAsync_Returns_Only_Matching_Property`, `DeleteAsync_Removes_Entity`, `GetByIdAsync_Returns_Matching_Entity`
- Pattern: plain class (no `IClassFixture`); private static `SeedLookups` + `BuildDam` helpers; fresh `TestDbContextFactory.Create()` per `[Fact]`; types referenced via global namespace (matches `Repositories/PropertyRepositoryTests.cs` convention)
- `DamCalculation` `required` members satisfied: `Property` nav, `River` nav, `SateliteSurveyDate`, `DamCapacity`; explicit `PropertyId`/`RiverId` scalars set so `GetByPropertyIdAsync` filter works under InMemory
- Build: `dotnet build` — 0 errors, 1 pre-existing warning at `Tests/Services/Auth/DwsClaimsTransformationTests.cs:119` (xUnit2031, not introduced here)
- Run: `dotnet test --filter "FullyQualifiedName~DamCalculationRepositoryTests"` — 4/4 passed, 757ms
- Production code: untouched. No edits outside `Tests/Repositories/DamCalculationRepositoryTests.cs` and this journal.

---

## Retro (fill in on task completion)

- **Converged:** All three agents independently settled on the same pattern (plain class, no IClassFixture, private static seed helpers, fresh InMemory DB per test, set both nav property and scalar FK explicitly). No reconciliation needed. 11 new tests passing, 25 pre-existing failures unchanged.
- **Drifted:** Nothing significant. FieldAndCrop and DamCalculation each added a bonus 4th test (GetByIdAsync) beyond the required 3; Forestation wrote exactly 3. Minor asymmetry but all within spec.
- **Failed prompt pattern (previous session):** Prompts that asked agents to "read these files first" before writing caused stream watchdog stalls at 600s. Fix: pre-load all model/interface signatures and test patterns verbatim into the prompt so agents write immediately without file exploration.
- **Lesson worth promoting to agent memory?** Yes — update `feedback_subagent_execution.md`: "Pre-load model signatures, interface contracts, and test patterns verbatim into agent prompts. Prompts that ask agents to discover context via file reads before writing reliably stall the stream watchdog at 600s."
