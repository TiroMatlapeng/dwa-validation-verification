# Project Handoff

## Current Focus
WorkflowEngine gap-fill complete. Next priority: Wave 2 (CalculatorEngine, LawfulnessAssessmentService) — needs a separate design session before implementation.

## Settled Decisions
- WorkflowController, Property subdivision/consolidation, FileMasterController CRUD: already built — do not rebuild — decided by: orchestrator discovery, date: 2026-05-12
- P0 Wave 1 scope: Field & Crop, Forestation, Dam Calculation CRUD views only — CalculatorEngine and LawfulnessAssessmentService deferred to Wave 2 — decided by: user, date: 2026-05-12
- Authorization: `[Authorize(Policy = DwsPolicies.CanTransitionWorkflow)]` on new workflow controllers — decided by: design spec, date: 2026-05-12
- DWS brand palette only — no Tailwind colors — decided by: feedback, ongoing
- LetterType has NO `LetterCode` property — short codes stored in `LetterName`. Any reference to `LetterCode` is a bug — discovered: 2026-05-19
- PAJAChecklist.IsComplete is computed from 4 text fields AND CompletedAt.HasValue — must stamp CompletedAt before evaluating IsComplete — discovered: 2026-05-19
- Roles are ASP.NET Identity roles (AspNetRoles table) — ApplicationUser has NO UserRole property. Guards load roles via `_context.UserRoles.Join(_context.Roles...)` — discovered: 2026-05-19

## Completed Work
- [x] External Portal shell (registration, login, dashboard placeholder) — agent: spring-java-architect/dotnet-master — date: 2026-05-04
- [x] Stage 2a: External Portal registration + login (merged) — date: 2026-05-04
- [x] P0 Wave 1 design spec authored and committed — agent: orchestrator — date: 2026-05-12
- [x] P0 Wave 1 implemented: FieldAndCrop, Forestation, DamCalculation CRUD (controllers, repos, viewmodels, 9 views, Details.cshtml updated) — agent: orchestrator (inline) — date: 2026-05-12
- [x] WorkflowEngine gap-fill (9 tasks): 2 new CP states, 4 new guards, PAJA gate on Letter 3, GetBlockingReasonsAsync, blocking reasons UI, per-CP evidence forms, 60-day countdown, PAJAChecklist view — agent: dotnet-master (2 waves) — date: 2026-05-19

## In-Flight Work
<!-- nothing currently in flight -->

## What the Next Orchestrator Must Know
- WorkflowEngine gap-fill is COMPLETE. Build: 0 errors. 193/193 tests pass (local SQL Server running). Migration `WorkflowGapFill` applied.
- Two new workflow states: CP_PrePublicReview (DisplayOrder 16), CP_StakeholderWorkshop (DisplayOrder 17). All letter/declaration/terminal states shifted +2 (S35_Letter1Issued=18 ... Closed=35). The seed is idempotent — existing seeded DBs pick up the 2 new states and renumber on next app startup.
- CpsSkippedOnS33_2 now includes "CP_PrePublicReview" and "CP_StakeholderWorkshop" — S33(2) track skips both.
- 4 new guards in FlagGuards.cs: Cp6FieldCropGuard, Cp7EluGuard, CpPrePublicReviewGuard (role-checks via UserRoles list, NOT ApplicationUser.UserRole), CpStakeholderWorkshopGuard.
- GuardContext record extended with `ApplicationUser? User` and `IReadOnlyList<string>? UserRoles`.
- FileMasterController.Details now populates BlockingReasons and PAJAChecklist on the ViewModel.
- FileMasterController.AdvanceWorkflow now passes actual signed-in user GUID (was null).
- New endpoints: RecordCpEvidence (POST), PAJAChecklist GET ([ActionName]), PAJAChecklist POST ([ActionName]).
- New ViewModels: CpEvidenceForm.cs, PAJAChecklistForm.cs.
- New view: Views/FileMaster/PAJAChecklist.cshtml.
- Wave 2 (CalculatorEngine, LawfulnessAssessmentService) still deferred — needs a separate design session.
- Branch: `demo/azure-deploy`
