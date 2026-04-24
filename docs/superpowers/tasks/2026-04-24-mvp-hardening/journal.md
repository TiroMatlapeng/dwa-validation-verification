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

### 2026-04-24 14:05 — dotnet-architect — Phase 2 complete

- **Read:** Phase 0 + Phase 1 journal entries; Plan 1 Phase 2 (Tasks 2.1–2.3) as supplied inline; current `Program.cs`, `Views/_ViewImports.cshtml`, `ViewModels/FileMasterDetailsViewModel.cs`, `Controllers/HomeController.cs`.
- **Changed:**
  - `Program.cs` — replaced entirely with AddIdentity<ApplicationUser, IdentityRole<Guid>> + cookie auth (LoginPath/AccessDeniedPath, 8h sliding expiry, SameSite=Lax, SecurePolicy=Always), IClaimsTransformation → DwsClaimsTransformation, AddAuthorization(DwsPolicies.Configure), IScopedCaseQuery DI, IdentitySeeder DI + startup invocation, UseAuthentication before UseAuthorization; appended `public partial class Program { }` for WebApplicationFactory.
  - `ViewModels/LoginViewModel.cs` — new; `namespace dwa_ver_val.ViewModels`; Email/Password/RememberMe/ReturnUrl with DataAnnotations.
  - `Controllers/AccountController.cs` — new; `[AllowAnonymous]`; SignInManager/UserManager injection; GET+POST Login (checks `user.IsActive` before PasswordSignInAsync, lockoutOnFailure: true, Url.IsLocalUrl returnUrl guard); POST Logout `[Authorize]` + antiforgery; GET AccessDenied.
  - `Views/Account/Login.cshtml` + `Views/Account/AccessDenied.cshtml` — new; use dws-card / dws-form-row / dws-btn primitives; Layout=_Layout.
- **Learned:**
  - `dotnet build` after Task 2.1 fails with **exactly** 6 error lines naming the 5 expected missing types (`DwsClaimsTransformation`, `DwsPolicies`, `IScopedCaseQuery`, `ScopedCaseQuery`, `IdentitySeeder` — the last referenced twice in Program.cs so appears on two lines). No other compile errors in the project — the plan's "intermediate red state" is clean as specified.
  - Pre-existing CS8618 warnings on `Models/Entitlement.cs:11` and `Models/Irrigation.cs:20` persist from Phase 0 baseline, unrelated to Phase 2.
  - `ViewModels/` directory already existed at repo root (holds `FileMasterDetailsViewModel.cs`, which does NOT declare a namespace and relies on implicit root namespace). Plan's `LoginViewModel` uses explicit `namespace dwa_ver_val.ViewModels` — kept as-is; Login view references `@model dwa_ver_val.ViewModels.LoginViewModel` fully qualified so no `_ViewImports.cshtml` change needed. If future ViewModels also use `dwa_ver_val.ViewModels`, consider adding `@using dwa_ver_val.ViewModels` to `_ViewImports.cshtml` later.
  - Controller file compiles cleanly in isolation (no errors attributable to `AccountController.cs` — only Program.cs has unresolved symbols). Confirms Task 2.2 acceptance criterion.
  - Views directory did not yet have `Views/Account/`; created via Write tool's auto-parent-create.
  - Shadow FK `OrganisationalUnitOrgUnitId` flagged by Phase 1 did NOT cause startup model validation concerns at this phase (not exercised — app not run). Deferred per plan guidance.
- **Status:** DONE
- **Concerns:** Build is intentionally RED — five types resolve in Phases 3/4/5. Do not run `dotnet test` against the web project until Phase 5 seeder lands (test project's own suite may or may not still pass with the main csproj red — did not attempt since plan forbids it). Journal header "Branch" still reads `demo/azure-deploy` from template; left as-is across phases for consistency with Phase 0/1 notes.

---

### 2026-04-24 15:20 — dotnet-architect — Phase 3 complete

- **Read:** Phase 0 + 1 + 2 journal entries; Plan 1 Phase 3 (Tasks 3.1–3.3) as supplied inline; `docs/contracts/auth-claims.md`; `contracts/fixtures/auth/claims.json`; `Models/ApplicationUser.cs`, `Models/OrganisationalUnit.cs`, `Models/Province.cs`, `Models/WaterManagementArea.cs`; current `Program.cs`; Tests/dwa_ver_val.Tests.csproj; agents-in-concert SKILL.md Rules 2, 3, 6; cross-boundary-contracts SKILL.md Rule 2 (fixture-driven tests).
- **Changed:**
  - `Tests/Services/Auth/DwsClaimsTransformationTests.cs` — new; two fixture-asserted tests (fixture round-trip for Limpopo Validator + idempotency re-call). Plan's seed snippet adjusted to populate required `Province.ProvinceCode` ("LP") and `WaterManagementArea.WmaCode` ("LIM") — without these the test project fails to compile because both are declared with C# `required` modifier.
  - `Services/Auth/DwsClaimsTransformation.cs` — new; `IClaimsTransformation` producer. Verbatim from plan. Uses `dws:augmented` sentinel claim for idempotency; projects user + org-unit fields via `.AsNoTracking()` single-round-trip query.
  - `Services/Auth/DwsRoles.cs` — new; 6 role constants + 7 hierarchy arrays (`All`, `AtLeastReadOnly`, `AtLeastCapturer`, `AtLeastValidator`, `AtLeastRegionalManager`, `AtLeastNationalManager`, `AdminOnly`). Verbatim.
  - `Services/Auth/DwsPolicies.cs` — new; 6 policy names + `Configure(AuthorizationOptions)` registration. Verbatim.
  - `Program.cs:54,58,71,72` — commented out 3 forward refs with `// TODO(Phase 4):` / `// TODO(Phase 5):` prefixes: `IScopedCaseQuery, ScopedCaseQuery` (Phase 4), `IdentitySeeder` DI registration and startup `SeedAsync()` invocation (Phase 5). Restores a green `dotnet build`.
- **Learned:**
  - Plan's seed snippet was missing the required `ProvinceCode` / `WmaCode` values — their `required` modifier on the models is enforced by the C# compiler at object-initialiser time, not by EF Core. Caught before first commit by grepping the model files.
  - Fixture path `Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "contracts", "fixtures", "auth", "claims.json")` resolves correctly. 4 `..` segments walk `bin/Debug/net10.0 → bin/Debug → bin → Tests → worktree-root`, then descend into `contracts/fixtures/auth/`. `Path.GetFullPath` flattens the traversal; no need to log for debugging — the test passed on first run.
  - The `_db.Users` query's projection materialises the `OrgUnit` nav into an anonymous record via `Select` — `AsNoTracking` has no effect on projection queries (they're never tracked), but keeping it is fine and idiomatic. EF translates the null-guard `u.OrgUnit == null ? null : new { ... }` into a `LEFT JOIN` + null-propagated select; works identically on the InMemory provider.
  - The `dws:augmented` sentinel pattern is cheap and correct: `identity.HasClaim(c => c.Type == Marker)` short-circuits on re-entry before any DB round-trip. Idempotency test verifies no duplicate `displayName` claim after second `TransformAsync` call.
  - Build warnings carried from Phase 0–2 baseline: CS8618 × 2 (`Models/Entitlement.cs:11`, `Models/Irrigation.cs:20`) plus ASP0026 × 1 (`Controllers/AccountController.cs:66`, from Phase 2's `[Authorize]` on `Logout` inside an `[AllowAnonymous]` controller — pre-existing). One new xUnit2031 warning on `DwsClaimsTransformationTests.cs:118` for `Assert.Single(...Where(...))` — verbatim from plan; non-blocking; Phase-next agent may switch to `Assert.Single(claims, predicate)` overload if they care.
  - `dotnet test` after bridge commit: **45 tests pass, 0 failures** (43 existing + 2 new). `Build succeeded. 0 Error(s).`
- **Status:** DONE
- **Concerns:**
  - Pre-existing shadow FK `OrganisationalUnitOrgUnitId` on `AspNetUsers` (flagged in Phase 1) is NOT touched — it doesn't affect the producer's projection because the projection goes through the navigational `u.OrgUnit` property (which uses the proper `OrgUnitId` FK), not the shadow column. Leave for a dedicated migration cleanup later.
  - Phase 4 agent: when you restore `IScopedCaseQuery` registration on line 54, note that the `DwsClaimsTransformation` already emits empty-string sentinels for `provinceId`/`wmaId`/`catchmentId` — your scope filter should treat empty string (not null/missing) as the "unscoped" branch, per the contract.
  - Phase 5 agent: the two commented lines at 71–72 must be restored TOGETHER (registration-free `GetRequiredService<IdentitySeeder>()` will throw). The existing `SeedDataService` registration on line 57 is untouched.

---

### 2026-04-24 16:15 — dotnet-architect — Phase 4 complete

- **Read:** Phase 0–3 journal entries; Plan 1 Phase 4 (Tasks 4.1–4.3) as supplied inline; `docs/contracts/auth-claims.md` (consumer view); `Models/FileMaster.cs` (8 required fields confirmed); current `Controllers/FileMasterController.cs`, `Program.cs`, `Services/Auth/DwsPolicies.cs`, `Services/Auth/DwsRoles.cs`.
- **Changed:**
  - `Tests/Services/Auth/ScopedCaseQueryTests.cs` — new; two tests (Validator-WMA-filter + NationalManager-sees-all). Added `CreateTestFileMaster` helper (Option B) to satisfy 8 required `FileMaster` fields cleanly.
  - `Services/Auth/IScopedCaseQuery.cs` — new; `FilterFileMasters`, `FilterProperties`, `IsInScope`. Verbatim from plan.
  - `Services/Auth/ScopedCaseQuery.cs` — new; SystemAdmin/NationalManager bypass; missing `wmaId` → empty query (fail-closed); else filter on `Property.WmaId == <claim>`. Verbatim.
  - `Program.cs:54` — uncommented `builder.Services.AddScoped<IScopedCaseQuery, ScopedCaseQuery>();`. Two `TODO(Phase 5)` markers on lines 58, 71–72 remain untouched (IdentitySeeder DI + startup invocation).
  - `Controllers/FileMasterController.cs` — class-level `[Authorize(CanRead)]`; ctor now injects `IScopedCaseQuery _scope`; `Index` rewritten to `_scope.FilterFileMasters(_context.FileMasters.AsQueryable(), User).Include(Property).OrderBy(FileNumber).ToListAsync()`; `[Authorize(CanCreateCase)]` on Create/Edit/Delete GET+POST; `[Authorize(CanTransitionWorkflow)]` on `AdvanceWorkflow`; `[Authorize(CanIssueLetter)]` on `IssueLetter` + `MarkLetterResponse` (committed separately).
- **Learned:**
  - `FileMaster` has 8 `required` fields (`RegistrationNumber`, `SurveyorGeneralCode`, `PrimaryCatchment`, `QuaternaryCatchment`, `FarmName`, `FarmNumber`, `RegistrationDivision`, `FarmPortion`) — plan's inline test literals would not compile. Chose **Option B** (helper method) per task hint; `CreateTestFileMaster(propertyId, fileNumber)` sets sensible `"N/A"`/`0` defaults in one place.
  - RED → GREEN was clean: RED showed two `CS0246: ScopedCaseQuery not found`; after implementation both tests pass on first run.
  - `FileMasterController.Index` rewrite required an explicit `.Include(fm => fm.Property)` so the pre-filtered `.Where(fm => fm.Property!.WmaId == wmaId)` projection still loads the nav for the Razor view (otherwise the list view would null-ref on `Model[i].Property.SGCode` after filtering).
  - `AdvanceWorkflow` **does** exist in this controller (line 108 pre-edit, 122 post-edit) — plan's hedge "may not exist yet" was outdated; grep confirmed one existing POST action matching the workflow-advance pattern.
  - Found an **extra, unplanned authorisation gap**: `IssueLetter` and `MarkLetterResponse` are letter-stage mutations that advance state via `TransitionToAsync` — they must be `CanIssueLetter`-gated (NWA requires RegionalManager+ to sign S35 letters). Plan only called out Create/Edit/Delete + workflow-advance; adding letter auth was a clear extension of the same role-boundary rule. Split into its own commit per explicit instruction.
  - `dotnet build`: **0 Error(s)**, 4 warnings (all pre-existing: 2× CS8618 baseline, 1× ASP0026 from Phase 2, 1× xUnit2031 from Phase 3).
  - `dotnet test`: **47 passed, 0 failed** (45 from Phase 3 + 2 new ScopedCaseQueryTests), matching the plan's expected count.
- **Status:** DONE
- **Concerns:**
  - `_scope.FilterFileMasters` returns `source.Where(_ => false)` for non-bypass roles with no `wmaId` claim — correct fail-closed default per the contract, but any `Validator` whose `OrganisationalUnit` lacks a `WmaId` will see an empty list with no UI hint. Phase 6 (user admin) should enforce WmaId non-null at user-creation time, or Phase 7 should surface a "your account is not scoped to a WMA — contact an admin" banner.
  - `IScopedCaseQuery` is currently consumed only by `FileMasterController.Index`. Other read paths (`Details` via `_fileMasterRepository.GetWithWorkflowAsync`, PDF/letter surfaces in future phases) bypass the filter. Phase 5+ agents should either route all reads through `_scope` or add defence-in-depth inside the repository.
  - Pre-existing shadow FK `OrganisationalUnitOrgUnitId` on `AspNetUsers` (flagged in Phases 1–3) still untouched — correct per plan's "out of scope" list.

---

## Retro (fill in on task completion)

- **Converged:** <what landed cleanly>
- **Drifted:** <where agents disagreed or needed iteration>
- **Failed prompt pattern:** <specific phrasing to avoid in future>
- **Lesson worth promoting to agent memory?** <yes/no + which memory file to update>
