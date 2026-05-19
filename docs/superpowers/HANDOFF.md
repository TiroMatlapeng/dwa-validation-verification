# Project Handoff

## Current Focus
Bug-fix sprint complete. Three-phase review found and fixed 7 bugs + 5 architectural issues. 229 tests pass. Next priority: Wave 2b (LawfulnessAssessmentService) — needs a separate design session.

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

## Completed Work
- [x] External Portal shell (registration, login, dashboard placeholder) — agent: spring-java-architect/dotnet-master — date: 2026-05-04
- [x] Stage 2a: External Portal registration + login (merged) — date: 2026-05-04
- [x] P0 Wave 1 design spec authored and committed — agent: orchestrator — date: 2026-05-12
- [x] P0 Wave 1 implemented: FieldAndCrop, Forestation, DamCalculation CRUD (controllers, repos, viewmodels, 9 views, Details.cshtml updated) — agent: orchestrator (inline) — date: 2026-05-12
- [x] WorkflowEngine gap-fill (9 tasks): 2 new CP states, 4 new guards, PAJA gate on Letter 3, GetBlockingReasonsAsync, blocking reasons UI, per-CP evidence forms, 60-day countdown, PAJAChecklist view — agent: dotnet-master (2 waves) — date: 2026-05-19
- [x] Wave 2a CalculatorEngine (9 tasks): CropWaterRate + SfraSpeciesRate models + migration, pure static calculators (SAPWAT, dam volume, SFRA), CalculatorService DI orchestrator, seeded reference data, Dam/FieldAndCrop/Forestation UI calculate buttons — agent: dotnet-master (9 tasks, subagent-driven) — date: 2026-05-19
- [x] Bug-fix sprint (7 bugs + 5 arch issues): 4 parallel agents (calculator, filemaster, controllers, workflow) + DamCalculationController direct patch + Batch 2 test agent — agents: dotnet-master, code-validator, dotnet-architect + 4 fix agents + test-batch2 — date: 2026-05-19. Commits: b5c0cba, f4f0d2e, 6fa8a5f, 08facd9, eaa6de1

## In-Flight Work
<!-- nothing currently in flight -->

## What the Next Orchestrator Must Know
- Bug-fix sprint COMPLETE. Build: 0 errors. 229/229 tests pass. Branch: `demo/azure-deploy`.
- Auth policy corrected: sub-controllers (FieldAndCrop, Forestation, DamCalculation) use `CanCapture` at class level; `CanTransitionWorkflow` restricted to Delete actions only.
- IDOR fix: all three Calculate actions now load FileMaster via PropertyId and call `_scope.IsInScope(fileMaster, User)` before computing.
- `IssueLetterRequest.SignedByUserId` is `Guid?` (nullable) — pass null if no signed-in user Guid available.
- `SfraResult` is now a property-init record (not positional) — use named properties: `EluHa`, `EluVolume`, `LawfulHa`, `LawfulVolume`, `UnlawfulHa`, `UnlawfulVolume`.
- `LastCalculatedAt` audit stamp added to `FieldAndCrop`, `DamCalculation`, `Forestation` — migration `CalculatorAuditStamps` (20260519161809) applied.
- ForestationController.Create POST: ELU fields initialized to 0m (not from user input) — correct.
- Views/Forestation/Create.cshtml: ELU Determination section removed (computed-only, not editable on create).
- New static calculators in `Services/Calculator/`: `SapwatCalculator`, `DamVolumeCalculator`, `SfraCalculator`.
- `ICalculatorService` / `CalculatorService` registered in Program.cs. Injected into: DamCalculationController, FieldAndCropController, ForestationController.
- Wave 2b (LawfulnessAssessmentService) still deferred — needs a separate design session.
- docs/DWA_VV_Test_Data.xlsx Wave 2a rows not updated yet (low priority).
- Branch: `demo/azure-deploy`
