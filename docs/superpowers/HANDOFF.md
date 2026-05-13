# Project Handoff

## Current Focus
Implementing P0 Wave 1 — three parallel CRUD workstreams: Field & Crop views, Forestation views, Dam Calculation views (design spec: `docs/superpowers/specs/2026-05-12-p0-wave1-design.md`).

## Settled Decisions
- WorkflowController, Property subdivision/consolidation, FileMasterController CRUD: already built — do not rebuild — decided by: orchestrator discovery, date: 2026-05-12
- P0 Wave 1 scope: Field & Crop, Forestation, Dam Calculation CRUD views only — CalculatorEngine and LawfulnessAssessmentService deferred to Wave 2 — decided by: user, date: 2026-05-12
- No new EF migrations expected (models already in schema) — verify with `dotnet ef migrations list` before starting — decided by: design spec, date: 2026-05-12
- Authorization: `[Authorize(Policy = DwsPolicies.CanTransitionWorkflow)]` on all new controllers — decided by: design spec, date: 2026-05-12
- DWS brand palette only — no Tailwind colors — decided by: feedback, ongoing

## Completed Work
- [x] External Portal shell (registration, login, dashboard placeholder) — agent: spring-java-architect/dotnet-master — date: 2026-05-04
- [x] Stage 2a: External Portal registration + login (merged) — date: 2026-05-04
- [x] P0 Wave 1 design spec authored and committed — agent: orchestrator — date: 2026-05-12
- [x] P0 Wave 1 implemented: FieldAndCrop, Forestation, DamCalculation CRUD (controllers, repos, viewmodels, 9 views, Details.cshtml updated) — agent: orchestrator (inline) — date: 2026-05-12

## In-Flight Work
<!-- nothing currently in flight -->

## What the Next Orchestrator Must Know
- P0 Wave 1 is COMPLETE. Build: 0 errors. Tests: 25 failures (all pre-existing; none related to new code).
- All three models (FieldAndCrop, Forestation, DamCalculation) are scoped by PropertyId — NOT FileMasterId as the design spec assumed. UI flow: FileMaster/Details → child Index via PropertyId.
- DamCalculation model stores DamCapacity directly — no Method1/Method2 formula input fields exist on the entity. The Appendix D computation must happen offline; user enters the result.
- Forestation model is far richer than the design spec described — all fields are captured in the form.
- FieldAndCrop has no explicit FK scalar properties for Crop/WaterSource/IrrigationSystem — nav properties are loaded by PK in the controller before saving.
- New DI registrations added to Program.cs: IFieldAndCrop→FieldAndCropRepository, IDamCalculation→DamCalculationRepository.
- Wave 2 (CalculatorEngine, LawfulnessAssessmentService) still deferred — needs a separate design session.
- Test baseline: 25 failures pre-existing (spec said 19; count drifted during Stage 2a). No new failures from P0 Wave 1.
- Branch: `demo/azure-deploy`
