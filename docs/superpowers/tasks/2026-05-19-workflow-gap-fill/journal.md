# Task: WorkflowEngine Gap-Fill — PRD Alignment
**Start:** 2026-05-19
**Branch:** demo/azure-deploy
**Worktree:** /Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai
**Plan:** /Users/edwinmatlapeng/.claude/plans/flickering-coalescing-star.md

**Acceptance criteria:**
- CP_PrePublicReview and CP_StakeholderWorkshop states seeded and guarded
- PAJA checklist entity exists and Letter 3 is blocked until complete
- CP6 and CP7 guards block advancement when data is absent
- GuardContext carries the acting user for role-based checks
- GetBlockingReasonsAsync surfaced on IWorkflowService
- RecordCpEvidence and PAJAChecklist endpoints exist
- _WorkflowPanel shows inline blocking reasons and per-CP evidence forms
- 60-day countdown badge in _LettersPanel
- dotnet build: 0 errors
- dotnet test: no new failures vs baseline (25 pre-existing failures)

## Journal

### 2026-05-19T00:00 — orchestrator — session start / plan review requested

- Read: HANDOFF.md (stale — last entry was P0 Wave 1, 2026-05-12)
- Read: sessions/2026-05-12-10-00.md
- Confirmed: WorkflowService, ITransitionGuard, 6 guards, 33 seeded states, LetterService (10 templates), FileMaster Details with _WorkflowPanel and _LettersPanel already exist
- Plan (flickering-coalescing-star.md) approved by user — 9 tasks
- User raised concern: work gets rediscovered during implementation; handoff/agents-in-concert not being used
- Action: dispatching rigorous plan-vs-codebase review agent before implementation starts
- Status: NEEDS_CONTEXT (review pending)

### 2026-05-19T14:30 — dotnet-master — Tasks 1–5 backend implementation

- Env confirmed: branch demo/azure-deploy, CWD /Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai
- Read: plan flickering-coalescing-star.md, FileMaster.cs, LetterIssuance.cs, ApplicationDBContext.cs, ITransitionGuard.cs, FlagGuards.cs (existing 6 guards), WorkflowService.cs, IWorkflowService.cs, SeedDataService.cs lines 140–195, LetterService.cs, FileMasterDetailsViewModel.cs, Program.cs, DwsRoles.cs, FieldAndCrop.cs, ApplicationUser.cs
- Changed:
  - Models/FileMaster.cs: added 6 gap-fill columns (PrePublicReviewApprovedAt + ById/By nav, StakeholderWorkshopDate/Venue/Attendance)
  - Models/PAJAChecklist.cs: NEW — 1:1 per FileMaster with computed IsComplete
  - Models/LetterIssuance.cs: added ServiceConfirmedDate (DateOnly?)
  - DatabaseContexts/ApplicationDBContext.cs: added DbSet<PAJAChecklist>, NonRestrictForeignKeys entry (Cascade on FileMasterId), unique index on PAJAChecklist.FileMasterId, two HasOne configs (FileMaster→PrePublicReviewApprovedBy, PAJAChecklist→FileMaster/CompletedBy)
  - Migrations/20260519142851_WorkflowGapFill.cs: created and applied cleanly (dotnet ef database update succeeded)
  - Services/SeedDataService.cs: split into virgin-DB block (35 states, new CPs at 16/17, letter states 18–35) and gap-fill block (idempotent — adds 2 new states + renumbers existing letter/declaration/terminal rows for previously-seeded DBs)
  - Services/WorkflowService.cs: CpsSkippedOnS33_2 includes CP_PrePublicReview + CP_StakeholderWorkshop; MoveToStateAsync loads ApplicationUser + roles via Identity UserRoles/Roles join (Guid keys); added GetBlockingReasonsAsync
  - Services/Workflow/ITransitionGuard.cs: GuardContext gains ApplicationUser? User and IReadOnlyList<string>? UserRoles (both default null)
  - Services/Workflow/Guards/FlagGuards.cs: added Cp6FieldCropGuard (SAPWAT>0 check), Cp7EluGuard (EntitlementId.HasValue), CpPrePublicReviewGuard (timestamp + AtLeastRegionalManager role), CpStakeholderWorkshopGuard (date + attendance>0)
  - Services/Letters/LetterService.cs: PAJA gate on S35_L3 (throws if checklist missing/incomplete)
  - Interfaces/IWorkflowService.cs: added GetBlockingReasonsAsync signature
  - ViewModels/FileMasterDetailsViewModel.cs: added BlockingReasons + PAJAChecklist; IsReadyForLetters now includes CP_StakeholderWorkshop; AvailableLetterActions fires Letter 1 / S33_3 declarations at CP_StakeholderWorkshop (and legacy CP9_SFRACalculated retained for in-flight cases)
  - Program.cs: registered Cp6FieldCropGuard, Cp7EluGuard, CpPrePublicReviewGuard, CpStakeholderWorkshopGuard
- Verification:
  - dotnet build → 0 errors, 8 warnings (all pre-existing unrelated CS8602/CS8618/ASP0026/xUnit2031)
  - dotnet ef migrations add WorkflowGapFill → succeeded
  - dotnet ef database update → succeeded, all schema applied (PAJAChecklists table + indexes + 6 FileMaster columns + ServiceConfirmedDate)
  - dotnet test --no-build → 193 passed / 0 failed (better than baseline note of 25 expected failures; the integration tests must be running clean against the local SQL Server)
- Learned (for next agent on Tasks 6–9):
  - Cascade override loop in ApplicationDBContext.cs (lines 781–796) downgrades any non-whitelisted FK to Restrict. SetNull configs for nullable FKs (PrePublicReviewApprovedById, PAJAChecklist.CompletedById) end up Restrict in the migration — that's the established codebase pattern, not a bug. Only Cascade on PAJAChecklist.FileMasterId is preserved because we whitelisted it.
  - SeedDataService runs every startup via Program.cs. The new gap-fill block is idempotent: re-running on a fresh DB (which already got the 35-state seed) does nothing because the AnyAsync check on the two new state names returns true.
  - The user/roles join in WorkflowService uses _context.UserRoles and _context.Roles (Identity tables) with Guid keys — confirmed by IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>.
  - DwsRoles.AtLeastRegionalManager = { SystemAdmin, NationalManager, RegionalManager } — used directly via .Contains in CpPrePublicReviewGuard.
  - Tasks 6–9 (controller actions, view forms, PAJAChecklist.cshtml) still pending — Out of scope for this dispatch per task brief.
- Status: DONE

### 2026-05-19T16:00 — dotnet-master — Tasks 6–9 controller + views

- Env confirmed: branch demo/azure-deploy, CWD /Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai
- Read: prior journal entries (Tasks 1–5 backend), FileMasterController.cs, FileMasterRepository.cs, _WorkflowPanel.cshtml, _LettersPanel.cshtml, FileMasterDetailsViewModel.cs, Models/PAJAChecklist.cs, Models/LetterType.cs, Services/Auth/DwsPolicies.cs, Services/SeedDataService.cs (letter seeds), Views/_ViewImports.cshtml, ViewModels/ConsolidateViewModel.cs (namespace check)
- Changed:
  - ViewModels/CpEvidenceForm.cs: NEW — global namespace (matches FileMasterDetailsViewModel convention), 7 nullable fields for per-CP evidence posts
  - ViewModels/PAJAChecklistForm.cs: NEW — global namespace, 4 string? narrative fields
  - Controllers/FileMasterController.cs:
    - Added `using System.Security.Claims;` (required for ClaimsPrincipal.FindFirstValue extension method — implicit usings do not cover it in this project)
    - Details: populates `vm.BlockingReasons` (via _workflow.GetBlockingReasonsAsync with signed-in user GUID) and `vm.PAJAChecklist` (DB lookup by FileMasterId)
    - AdvanceWorkflow: now passes actual signed-in user GUID instead of `userId: null` — role-based guards (CpPrePublicReviewGuard requires AtLeastRegionalManager) can now evaluate
    - NEW POST RecordCpEvidence: [CanCapture] policy; scope-guarded; stamps SpatialInfoConfirmedAt / WarmsReviewedAt / AdditionalInfoReviewedAt / PrePublicReviewApprovedAt (+ ApprovedById) / StakeholderWorkshopDate / Venue / Attendance. Uses `??=` so first-set wins.
    - NEW GET PAJAChecklistGet + POST PAJAChecklistPost: both use `[ActionName("PAJAChecklist")]` so URL/asp-action="PAJAChecklist" works; method names suffixed to avoid C# error CS0542 (member cannot have same name as containing type — would clash with `PAJAChecklist` model type referenced inside)
  - Views/FileMaster/_WorkflowPanel.cshtml: rewrote to add 4 enhancements
    - Step counter (Step N of M, excluding terminal states)
    - Amber blocking-reasons panel rendered above the phase tracker when GetBlockingReasonsAsync returned any reasons
    - Per-CP inline evidence form for CP2_SpatialInfo / CP3_WARMSEvaluation / CP4_AdditionalInfo / CP_PrePublicReview / CP_StakeholderWorkshop (the last has date+venue+attendance inputs; others are single confirmation buttons)
    - PAJA-required nag link (red) when CurrentState is S35_Letter1Responded or S35_Letter2Responded AND PAJAChecklist not complete
    - `csn`, `totalSteps`, `currentStep` declared at top of `else` block as direct C# statements (NOT inside `@{ }` — Razor in ASP.NET Core 10 throws RZ1010 when nesting `@{` inside an already-Razor block)
  - Views/FileMaster/_LettersPanel.cshtml: prepended a 60-day-countdown card before letter-actions
    - Filters letters by `LetterType.LetterName == "S35_L1"` (NOT LetterCode — LetterType has no LetterCode property; SeedDataService stores the short code in LetterName)
    - Clock starts on ServiceConfirmedDate if set (S35(2)(d) in-person service confirmation), else IssuedDate
    - Colour graduates green (>15d) → amber (≤15d) → red (expired) using C# 9 switch expression on days-left
  - Views/FileMaster/PAJAChecklist.cshtml: NEW — 4-section form (FactualBasis, LegalBasis, UserInputConsideration, FinalReasoning) with completion-state banner (green when IsComplete, amber otherwise)
- Deviations from brief (documented for traceability):
  - Brief snippet for PAJAChecklist POST used `if (existing.IsComplete && !existing.CompletedAt.HasValue) { CompletedAt = ... }` — this is a deadlock because IsComplete *requires* CompletedAt.HasValue to return true, so it would never stamp. Fixed by computing `willBeComplete` from the four text fields first, then setting CompletedAt, then IsComplete becomes true on next read. This is a correctness fix, not a scope deviation.
  - Brief noted LetterType.LetterCode might be wrong — confirmed: there is no LetterCode property on LetterType; used LetterName.
  - Brief had two `PAJAChecklist` action patterns (`PAJAChecklistAction` rename vs `[ActionName("PAJAChecklist")]`). Used the `[ActionName(...)]` approach because the brief explicitly preferred it ("to keep the URL clean").
- Verification:
  - dotnet build → 0 errors, 8 warnings (all pre-existing CS8602/CS8618/ASP0026/xUnit2031 — none introduced by this change)
  - dotnet test --no-build → 193 passed / 0 failed (matches baseline from Tasks 1–5)
- Acceptance criteria check:
  - [x] dotnet build passes with 0 errors
  - [x] Details action loads vm.BlockingReasons and vm.PAJAChecklist
  - [x] AdvanceWorkflow passes actual signed-in userId
  - [x] RecordCpEvidence POST exists and updates FileMaster evidence columns
  - [x] PAJAChecklist GET/POST endpoints exist and upsert the PAJAChecklist entity
  - [x] ViewModels/CpEvidenceForm.cs and ViewModels/PAJAChecklistForm.cs exist
  - [x] _WorkflowPanel.cshtml shows blocking reasons amber panel, per-CP evidence forms, progress counter, PAJA checklist link
  - [x] _LettersPanel.cshtml shows 60-day countdown badge when Letter 1 has been issued
  - [x] Views/FileMaster/PAJAChecklist.cshtml exists as a proper form view
- Learned (for next agent / future work):
  - Razor in ASP.NET Core 10 is stricter about nested code blocks — `@{ ... }` inside the body of `@if/@else { ... }` produces RZ1010. Declare variables directly in the C#-code context, or move them outside.
  - System.Security.Claims is NOT in the implicit using set for Microsoft.NET.Sdk.Web in net10.0 — must be explicit.
  - PAJAChecklist.IsComplete depends on CompletedAt.HasValue being set, so the stamp-on-completion logic must check the text fields directly, not IsComplete.
  - LetterType has `LetterName` (which holds short codes like "S35_L1"); there is no `LetterCode` property despite the naming in FileMasterController.LetterActionMap suggesting otherwise.
- Status: DONE

## Retro (on completion)
<!-- fill in when task closes -->
