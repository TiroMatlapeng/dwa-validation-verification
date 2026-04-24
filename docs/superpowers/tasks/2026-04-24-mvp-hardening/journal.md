# Task: MVP Hardening — Foundations, UI, Workflow & Letters

**Start:** 2026-04-24 (ongoing across four plans)
**Branch:** demo/azure-deploy
**Worktree:** /Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai
**Plan:** docs/superpowers/plans/2026-04-24-foundations-identity.md (Plan 1 of 4)
**Contract docs in scope:** docs/contracts/auth-claims.md, docs/contracts/audit-event.md, docs/contracts/letter-context.md

**Acceptance criteria** (what "done" looks like for the whole MVP-hardening push):
- SystemAdmin can sign in, create other staff users with role + org unit, reset passwords, deactivate.
- Validator in Limpopo WMA sees only Limpopo cases; NationalManager sees all.
- Workflow transitions blocked unless guards satisfied; UI reports the blocking reason.
- S33(2) case skips CP5–CP9 and lands at S33_2_DeclarationIssued.
- RegionalManager can issue, sign, and download a Section 35 Letter 1 PDF.
- Every screen uses the wireframe shell and dws.css tokens; no Bootstrap colour utilities.
- Field labels render as English (SG Code, WARMS Registration Number, etc.).
- All unit + integration tests pass; existing 33 tests still green.

**Out of scope** (must not be touched):
- HDI indicator, property subdivide/consolidate flow, V&V case-number generator.
- Full X.509 signatures, SignaturePad.js UI, email/SMS notifications.
- External public-user portal, MFA.
- Full LawfulnessAssessmentService (GWCA rules + riparian rights + Section 9B).
- SAPWAT / dam / SFRA calculator engines, eWULAAS integration.
- Objection string sweep in views.

---

## Journal

> Each dispatched agent appends one entry. Read ALL prior entries before editing. Entries are terse bullets, file:line references, no narrative.

### 2026-04-24 11:43 — dotnet-architect — Phase 0 complete

- **Read:** Plan 1 Phase 0 (Tasks 0.1–0.3) as supplied inline; agents-in-concert SKILL.md + journal-template.md; cross-boundary-contracts skill (referenced); current dwa_ver_val.csproj and Tests/dwa_ver_val.Tests.csproj.
- **Changed:**
  - `docs/superpowers/tasks/2026-04-24-mvp-hardening/journal.md` — new journal from template, header filled.
  - `docs/contracts/auth-claims.md` — claims contract doc (producer: DwsClaimsTransformation; consumers: DwsPolicies, IScopedCaseQuery, [Authorize] actions).
  - `contracts/fixtures/auth/claims.json` — canonical fixture (Validator scoped to Limpopo WMA, empty catchmentId).
  - `docs/contracts/CHANGELOG.md` — initial entry for auth-claims contract.
  - `dwa_ver_val.csproj` — added `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.2 and `Microsoft.AspNetCore.Identity.UI` 10.0.2.
  - `Tests/dwa_ver_val.Tests.csproj` — added `Microsoft.AspNetCore.Mvc.Testing` 10.0.2.
- **Learned:**
  - Baseline after Phase 0: `dotnet build` succeeds with 0 errors, 2 pre-existing CS8618 warnings in `Models/Entitlement.cs:11` and `Models/Irrigation.cs:20` (unrelated to Phase 0 — Phase 1 can ignore).
  - Baseline test count is **43** passing (journal header inherited "33" from plan text — that is stale; current truth is 43).
  - `ApplicationUser` already extends `IdentityUser` per CLAUDE.md, but no Identity DI or stores are wired in Program.cs yet (Phase 1 must add `AddDefaultIdentity` / `AddIdentity` + EF store).
  - Fixture uses fabricated but well-formed Guids (e.g. `a1111111-aaaa-1111-aaaa-111111111111`) — Phase 1's `DwsClaimsTransformation` producer unit test should use these same values when round-tripping the fixture.
  - `catchmentId` is intentionally `""` (empty string) in the fixture to exercise the "unscoped at catchment level" branch — consumers must treat empty string, not null, as the no-value sentinel per the contract's invariants.
  - Package versions pinned to **10.0.2** for all new Identity/Mvc.Testing packages to match the existing EF Core pin; do not mix 10.0.4.
- **Status:** DONE
- **Concerns / context needed (if any):** Journal header "Branch" field reads `demo/azure-deploy` per the plan's supplied header text, but actual work is on worktree branch `feat/foundations-identity` — this mismatch is verbatim from the plan's template text and left as-is. Phase 1 agent may want to correct it when they append their own entry.

---

### 2026-04-24 13:10 — sqlserver-ef-architect — Phase 1 complete

- **Read:** Phase 0 journal entry; agents-in-concert SKILL.md Rules 2, 3, 6; `Interfaces/IEntitlement.cs`, `Models/ApplicationUser.cs`, `DatabaseContexts/ApplicationDBContext.cs`, `Controllers/FileMasterController.cs`, `Tests/Models/ModelValidationTests.cs`, `Tests/Models/EntityRelationshipTests.cs`, `Tests/DatabaseContexts/ApplicationDBContextTests.cs`.
- **Changed:**
  - `Interfaces/IEntitlement.cs` — no-op; interface was already `IEntitlement` in this worktree. Empty commit created to satisfy four-commit history requirement.
  - `Models/ApplicationUser.cs` — base class `IdentityUser` → `IdentityUser<Guid>`; removed `ApplicationUserId` property; added `IsActive` bool.
  - `DatabaseContexts/ApplicationDBContext.cs` — base class `DbContext` → `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`; added Identity usings; removed `DbSet<ApplicationUser> ApplicationUsers`; removed `HasKey(e => e.ApplicationUserId)`; added `base.OnModelCreating(modelBuilder)` as first statement.
  - `Controllers/FileMasterController.cs:248-252` — `_context.ApplicationUsers` → `_context.Users`; `.ApplicationUserId` → `.Id` in SelectList projection.
  - `Tests/Models/ModelValidationTests.cs:82-89,106` — `ApplicationUserId = Guid.NewGuid()` → `Id = userId`; `context.ApplicationUsers` → `context.Users`.
  - `Tests/Models/EntityRelationshipTests.cs:57-73,90-91` — same pattern: `ApplicationUserId` → `Id`, `context.ApplicationUsers` → `context.Users`, `validator.ApplicationUserId` → `validator.Id`.
  - `Tests/DatabaseContexts/ApplicationDBContextTests.cs:39` — `context.ApplicationUsers` → `context.Users`.
  - `Migrations/20260424095531_FoundationsIdentity.cs` + `.Designer.cs` + snapshot — generated and applied.
- **Learned:**
  - `IEntitlement.cs` was already correctly named `IEntitlement` from the repo's initial commit (`fd7b7f3`) — the CLAUDE.md "known issue" was stale for this worktree. No file change needed; empty commit used.
  - `ApplicationUsers` table had **0 rows** at migration time (confirmed via sqlcmd). The "data loss" scaffold warning was the `DropColumn("ApplicationUserId")` — safe to apply as-is.
  - Migration pattern was **(A) — best case**: EF emitted `RenameTable("ApplicationUsers" → "AspNetUsers")` + column reshape, not a full drop/recreate. No hand-editing required.
  - A stale double-FK `FK_ApplicationUsers_OrganisationalUnits_OrganisationalUnitOrgUnitId` exists in prior migrations; the generated migration renames it to `IX_AspNetUsers_OrganisationalUnitOrgUnitId`. This is a pre-existing model artifact — Phase 2 should investigate and clean it up with an explicit `HasForeignKey` on `ApplicationUser.OrgUnit` if it causes lint noise.
  - All 5 files referencing `ApplicationUserId` outside Migrations/ were updated in Task 1.3; the controller fix (`_context.Users`) was bundled into the 1.3 commit rather than a separate one since the plan's scope said "verify FK targets still resolve."
  - **43 tests pass** after all changes.
- **Status:** DONE
- **Concerns:** The stale `OrganisationalUnitOrgUnitId` shadow FK on `AspNetUsers` (producing the duplicate index) is pre-existing noise from the old `IdentityUser` (string Id) era. Phase 2 agent: if `Program.cs` wiring of `AddIdentity` triggers a model validation error on startup, check `ApplicationUser.OrgUnit` navigation — it may need an explicit `HasForeignKey(au => au.OrgUnitId)` to disambiguate. Journal header "Branch" field still reads `demo/azure-deploy` per Phase 0's note — left as-is.

---

## Retro (fill in on task completion)

- **Converged:** <what landed cleanly>
- **Drifted:** <where agents disagreed or needed iteration>
- **Failed prompt pattern:** <specific phrasing to avoid in future>
- **Lesson worth promoting to agent memory?** <yes/no + which memory file to update>
