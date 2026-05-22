# Task: RBAC UI Visibility — hide buttons/actions from roles that can't use them
**Start:** 2026-05-22T14:00+02:00
**Branch:** main
**Worktree:** /Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai

**Context:**
The backend (controller [Authorize] attributes) is already fully enforced.
Unauthorised users who click restricted buttons already get a 403.
This task adds the UI layer: buttons/links that a role cannot use must not be visible at all.
The existing `DwsPolicies` constants are the single source of truth — views must check policies, not role names directly.

**Policy → role mapping (for reference):**
- CanRead           → everyone (ReadOnly and above)
- CanCapture        → Capturer, Validator, RegionalManager, NationalManager, SystemAdmin
- CanCreateCase     → Validator, RegionalManager, NationalManager, SystemAdmin
- CanTransitionWorkflow → Validator, RegionalManager, NationalManager, SystemAdmin
- CanIssueLetter    → RegionalManager, NationalManager, SystemAdmin
- CanAdminister     → SystemAdmin only

**Acceptance criteria:**
- `Views/_ViewImports.cshtml` injects `IAuthorizationService Auth`
- ReadOnly user sees NO create/edit/delete/advance/issue-letter buttons anywhere in the internal portal
- Capturer sees create/edit/delete on data records but NOT new case, workflow advance, or issue letter
- Validator sees create/edit/delete and workflow advance but NOT issue letter
- RegionalManager+ sees everything including issue letter
- SystemAdmin sees everything including user management
- `dotnet build` passes with 0 errors

## Journal

### 2026-05-22T14:00+02:00 — Controller — pre-flight scan
- Existing role checks in views: FileMaster/Index ✓, Property/Index ✓, _Layout.cshtml (admin menu) ✓
- Views with NO role checks: DamCalculation/Index, FieldAndCrop/Index, Forestation/Index,
  Owner/Index (controller is CanAdminister so moot, but nav link visibility matters),
  FileMaster/Details, FileMaster/Edit, FileMaster/Delete, FileMaster/_WorkflowPanel (advance CP),
  FileMaster/_LettersPanel (issue letter), Property/Details, Property/Edit, Property/Delete,
  Property/Consolidate, Property/Subdivide, Validation/Index, Users/Index
- DwsPolicies.cs and DwsRoles.cs confirmed at Services/Auth/
- Status: DISPATCHING dotnet-master

### 2026-05-22 — dotnet-master — RBAC UI visibility implemented

**Setup**
- `Views/_ViewImports.cshtml`: added `@inject IAuthorizationService Auth`. Also had to add
  `@using Microsoft.AspNetCore.Authorization` — the 2-arg `AuthorizeAsync(user, policyName)`
  overload is an EXTENSION method on `IAuthorizationService`; without the using directive the
  build failed across all 17 views with `CS1501: No overload ... takes 2 arguments`. Single
  fix in _ViewImports resolved all of them.

**Views changed (policy applied):**
- DamCalculation/Index, FieldAndCrop/Index, Forestation/Index → `canCapture` on Create + per-row Edit/Delete
- Owner/Index → `canAdmin` on Register + per-row Edit/Delete
- Validation/Index → `canCreate` on "Start a New Case" (the "Open" link is read-only nav, left alone)
- Users/Index → `canAdmin` on Add User + all row actions (Edit/Reset/Deactivate/Reactivate)
- FileMaster/Details → `canCapture` on Edit, `canCreate` on Delete; ELU "Run/Re-Assess" form gated
  `canCapture` (verified: AssessLawfulness controller action is CanCapture, NOT CanTransition).
  Data-capture nav links left visible (target Index pages self-guard).
- FileMaster/Delete → whole form gated `canCreate` with read-only message fallback
- FileMaster/_WorkflowPanel → "Advance to Next CP" gated `canTransition`; the RecordCpEvidence
  evidence forms gated `canCapture` (verified controller policy); PAJAChecklist link gated
  `canCreate` (verified PAJAChecklist GET is CanCreateCase). IsTrackRelevant / S33(2) skip logic
  untouched.
- FileMaster/_LettersPanel → entire `letter-actions` block (Issue* and Mark* forms) gated
  `canIssue` (verified IssueLetter + MarkLetterResponse are both CanIssueLetter). Letters table +
  PDF download links left visible (read-only).
- Property/Details → action button row (Edit/Delete/+New Case/Subdivide) gated `canCreate`
- Property/Delete → whole form gated `canCreate` with message fallback
- Property/Consolidate, Property/Subdivide → submit gated `canCreate`, message fallback when denied

**Surprising / notable findings:**
- FileMaster/Edit and Property/Edit have ONLY Save + Cancel — no embedded delete/transition
  buttons — so NO changes were needed (controller already restricts the page to Capturer+/
  Validator+). Left untouched per spec ("if there are any... wrap them"; there are none).
- _Layout already gates BOTH the User-management AND Property-Owners nav links inside one
  `@if (User.IsInRole("SystemAdmin"))` block. Already correct — no change needed.
- The pre-existing "already done" Index views (FileMaster/Index, Property/Index) use raw
  `User.IsInRole(...)` rather than the policy pattern. Left untouched per spec. Mild
  inconsistency with the new policy-based approach, but out of scope.
- Two extra guarded actions beyond the literal spec list were gated to satisfy the acceptance
  criteria ("ReadOnly sees NO advance buttons anywhere"): RecordCpEvidence (canCapture) and the
  ELU AssessLawfulness button (canCapture). Both confirmed against controller `[Authorize]` attrs.

**Build:** `dotnet build` → 0 Errors, 24 Warnings (all pre-existing, unrelated to this change).

**Status:** DONE

## Retro (on completion)
_To be filled on sprint close._
