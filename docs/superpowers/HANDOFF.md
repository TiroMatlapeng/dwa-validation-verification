# Project Handoff

## Current Focus
Wave 2b complete. LawfulnessAssessmentService implemented and wired. 243 tests pass. Next priority: Wave 3 (NotificationService / SignatureService) or demo prep — TBD with user.

## Settled Decisions
- WorkflowController, Property subdivision/consolidation, FileMasterController CRUD: already built — do not rebuild — decided by: orchestrator discovery, date: 2026-05-12
- P0 Wave 1 scope: Field & Crop, Forestation, Dam Calculation CRUD views only — CalculatorEngine and LawfulnessAssessmentService deferred to Wave 2 — decided by: user, date: 2026-05-12
- Authorization: `[Authorize(Policy = DwsPolicies.CanTransitionWorkflow)]` on new workflow controllers — decided by: design spec, date: 2026-05-12
- DWS brand palette only — no Tailwind colors — decided by: feedback, ongoing
- LetterType has NO `LetterCode` property — short codes stored in `LetterName`. Any reference to `LetterCode` is a bug — discovered: 2026-05-19
- PAJAChecklist.IsComplete is computed from 4 text fields AND CompletedAt.HasValue — must stamp CompletedAt before evaluating IsComplete — discovered: 2026-05-19
- Roles are ASP.NET Identity roles (AspNetRoles table) — ApplicationUser has NO UserRole property. Guards load roles via `_context.UserRoles.Join(_context.Roles...)` — discovered: 2026-05-19
- CalculatorEngine uses pure static classes (no DI, no DB) for arithmetic; `CalculatorService` (DI) orchestrates DB load + compute + save — decided: 2026-05-19
- ELU/Lawful/Unlawful output fields on Forestation and DamCapacity on DamCalculation and SAPWATCalculationResult on FieldAndCrop are computed-only — Edit POST must NOT overwrite them from form input — decided: 2026-05-19
- `CropWaterRate` lookup prefers specific IrrigationSystemId match; falls back to null (wildcard) row via `OrderByDescending(r => r.IrrigationSystemId.HasValue)` — decided: 2026-05-19
- **Cascade whitelist** (`NonRestrictForeignKeys` in ApplicationDBContext): any FK with Cascade/SetNull behavior MUST be added to this array — the global loop at end of `OnModelCreating` overwrites all non-whitelisted FKs to `Restrict` — discovered: 2026-05-19
- `LawfulnessCalculator` is pure static (no DI, no DB); `LawfulnessAssessmentService` is the DI orchestrator — same pattern as CalculatorEngine — decided: 2026-05-19
- `DamCalculation.DamCapacity` is `required decimal` (not nullable) — sum directly with `.Sum(dc => dc.DamCapacity)` — discovered: 2026-05-19
- S9B statutory limits (general path): storage = 250,000 m³; abstraction = 3,468,960 m³/year (110 l/s × 31,536 s/year) — confirmed: 2026-05-19
- GWCA rule codes: `MAX_HECTARES`, `MAX_IRRIGABLE_PCT`, `MAX_VOLUME_PER_HA`, `MAX_STORAGE_PER_HA`, `MAX_STORAGE_PER_PROPERTY` — established: 2026-05-19
- `Cp7EluGuard` checks only `ctx.FileMaster.EntitlementId.HasValue` — `AssessAsync` always sets it — confirmed: 2026-05-19
- EF Core project convention: audit timestamps use `datetime2(0)` — must call `HasColumnType("datetime2(0)")` in OnModelCreating — discovered: 2026-05-19

## Completed Work
- [x] External Portal shell (registration, login, dashboard placeholder) — agent: spring-java-architect/dotnet-master — date: 2026-05-04
- [x] Stage 2a: External Portal registration + login (merged) — date: 2026-05-04
- [x] P0 Wave 1 design spec authored and committed — agent: orchestrator — date: 2026-05-12
- [x] P0 Wave 1 implemented: FieldAndCrop, Forestation, DamCalculation CRUD (controllers, repos, viewmodels, 9 views, Details.cshtml updated) — agent: orchestrator (inline) — date: 2026-05-12
- [x] WorkflowEngine gap-fill (9 tasks): 2 new CP states, 4 new guards, PAJA gate on Letter 3, GetBlockingReasonsAsync, blocking reasons UI, per-CP evidence forms, 60-day countdown, PAJAChecklist view — agent: dotnet-master (2 waves) — date: 2026-05-19
- [x] Wave 2a CalculatorEngine (9 tasks): CropWaterRate + SfraSpeciesRate models + migration, pure static calculators (SAPWAT, dam volume, SFRA), CalculatorService DI orchestrator, seeded reference data, Dam/FieldAndCrop/Forestation UI calculate buttons — agent: dotnet-master (9 tasks, subagent-driven) — date: 2026-05-19
- [x] Bug-fix sprint (7 bugs + 5 arch issues): 4 parallel agents (calculator, filemaster, controllers, workflow) + DamCalculationController direct patch + Batch 2 test agent — agents: dotnet-master, code-validator, dotnet-architect + 4 fix agents + test-batch2 — date: 2026-05-19. Commits: b5c0cba, f4f0d2e, 6fa8a5f, 08facd9, eaa6de1
- [x] Wave 2b LawfulnessAssessmentService (5 tasks): data model + migration, pure LawfulnessCalculator (GWCA + general S9B), LawfulnessAssessmentService DI orchestrator, FileMasterController AssessLawfulness action, UI ELU Assessment panel + Property GWCA/IrrigableArea fields — agent: dotnet-master (5 tasks, subagent-driven) — date: 2026-05-19. Commits: b6a41d5, cbd8d2f, 06a6cb5, ac71900, babbd8c, b5555ba

## In-Flight Work
<!-- nothing currently in flight -->

## What the Next Orchestrator Must Know
- Wave 2b COMPLETE. Build: 0 errors. 243/243 tests pass. Branch: `demo/azure-deploy`.
- `LawfulnessAssessmentService` registered in Program.cs. Injected into `FileMasterController` as 6th constructor arg (`ILawfulnessAssessmentService _assessment`).
- `AssessLawfulness` POST action on FileMasterController runs the ELU assessment, sets `FileMaster.EntitlementId`, satisfies `Cp7EluGuard`.
- `LawfulnessAssessmentResult` is 1:1 with `FileMaster` (unique index on `FileMasterId`). Upsert pattern: re-running assessment updates the existing row in place.
- `Property` now has `WaterControlAreaId` (FK to `GovernmentWaterControlArea`) and `IrrigableAreaHa` (decimal). Both editable on Property Edit view.
- Migration `Wave2bLawfulness` (20260519171310) applied to local DB.
- 3 EntitlementType seed records added: ELU_Irrigation, ELU_Storage, ELU_SFRA (step 10 in SeedDataService).
- **Non-blocking follow-up items before production**: add AuditService call in AssessLawfulness; refresh Entitlement.Name on re-assessment; handle deleted Entitlement FK gracefully; verify GWCA 53% rule with John Malungani; hide "Run ELU Assessment" from ReadOnly users; 3 test coverage gaps.
- Auth policy: sub-controllers (FieldAndCrop, Forestation, DamCalculation) use `CanCapture` at class level; `CanTransitionWorkflow` restricted to Delete actions only.
- IDOR fix: all Calculate actions load FileMaster via PropertyId and call `_scope.IsInScope(fileMaster, User)` before computing.
- `IssueLetterRequest.SignedByUserId` is `Guid?` (nullable).
- `SfraResult` is property-init record — use named properties: `EluHa`, `EluVolume`, `LawfulHa`, `LawfulVolume`, `UnlawfulHa`, `UnlawfulVolume`.
- `LastCalculatedAt` audit stamp on `FieldAndCrop`, `DamCalculation`, `Forestation` — migration `CalculatorAuditStamps` (20260519161809) applied.
- New static calculators in `Services/Calculator/`: `SapwatCalculator`, `DamVolumeCalculator`, `SfraCalculator`, `LawfulnessCalculator`.
- `ICalculatorService` / `CalculatorService` registered in Program.cs. Injected into: DamCalculationController, FieldAndCropController, ForestationController.
- docs/DWA_VV_Test_Data.xlsx Wave 2a rows not updated yet (low priority).
- Branch: `demo/azure-deploy`
