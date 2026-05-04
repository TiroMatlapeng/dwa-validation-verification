# Task: External Portal Shell (Tasks 9.1 + 9.2 + 9.4)

**Start:** 2026-05-03 evening
**Branch:** demo/azure-deploy
**Worktree:** /Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai (main repo, not a worktree)
**Plan:** not yet written — currently in brainstorm phase
**Spec:** to be written at docs/superpowers/specs/2026-05-03-external-portal-shell-design.md

**Acceptance criteria (in scope for this slice):**
- New MVC Area `/Areas/ExternalPortal/` with separate cookie scheme `PublicPortalScheme`
- `PublicUser` self-registration with email confirmation + forced TOTP MFA on first login
- Property linking flow: ID-match suggestion + manual title-deed-upload claim → DWS approval queue (Validator+) → approved → user sees case data
- "My Cases" dashboard scoped strictly by approved `PublicUserProperty` rows
- Case detail page with timeline of workflow transitions + letter issuances; PDF download for letters where the user is the addressee
- In-app notifications + email send (logging-only sender for now) on workflow state changes
- All DB queries scoped through one `IPublicUserPropertyAccessor` to prevent cross-user data leakage

**Out of scope (deferred to later tasks):**
- SMS OTP (Task 2.6 needs Key Vault first)
- Real email provider (Task 2.6)
- Azure Blob upload (Task 2.4)
- Virus scanning
- Comment thread submission (Task 9.3)
- Objection lodging (Task 5.4 — also reduced in scope per client: lodge + docs + DWS report only, no internal adjudication)
- Two-way letter response upload (Task 9.3)

## Journal

### 2026-05-03 evening — controller (Claude) — task journal created
- Read: CLAUDE.md, Models/PublicUser.cs, Models/PublicUserProperty.cs, Models/Notification.cs, Models/Document.cs, Program.cs (auth wiring), DBContext (FK wiring for PublicUser-related entities), Project rollout plan.xlsx
- Confirmed: PublicUser/PublicUserProperty entities exist; FKs already wired for Document, CaseComment, Objection, Notification, DigitalSignature, SignatureRequest; portal-wireframes.html exists; no IEmailSender wired anywhere
- Decisions made with user (sections 1 + 2 of design approved):
  - Scope C: shell + 9.1 + 9.2 + 9.4 (full read-only portal)
  - Identity stack: hybrid C — standalone PublicUser + ASP.NET Identity primitives (PasswordHasher, DataProtection, Otp.NET) but NOT full Identity scaffolding
  - MFA: TOTP only for MVP (Q3D)
  - Email: IEmailSender interface + LoggingEmailSender for now (Q4A)
  - Property linking: combined flow — auto-suggest by ID match (never auto-grant) + manual title-deed-upload — both routed through one DWS approval queue
  - Timeline: workflow transitions + letter issuances (with PDF download for addressee letters); comments/objections deferred
  - Objection scope clarified: lodge + docs + DWS report only — no internal adjudication workflow
- Status: brainstorm-in-progress

### 2026-05-03 evening — controller — dispatching three parallel critique agents
- Goal: critique sections 1 + 2 of the proposed design before locking in sections 3-6
- Dispatched: security-architect (auth boundary + crypto + POPIA), dotnet-architect (.NET 10 + Areas + cookie schemes + service shape), sqlserver-ef-architect (PublicUser schema, indexing, migration shape)
- These critiques are independent: each looks at a different layer
- All three receive identical briefing packet + their specific "your slice" focus


### 2026-05-03 evening — dotnet-architect (Opus 4.7) — framework critique
- Read: CLAUDE.md, Program.cs (auth wiring lines 19-80), Services/Auth/DwsPolicies.cs, Services/IdentitySeeder.cs, Services/{Auth,Workflow,Letters,Audit} layout
- Confirmed existing wiring: AddIdentity<ApplicationUser, IdentityRole<Guid>> + ConfigureApplicationCookie + OIDC "Microsoft" external scheme using SignInScheme=IdentityConstants.ExternalScheme
- Status: critique delivered to controller (parent agent)

### 2026-05-03 evening — security-architect agent — security critique complete
- Read: Models/PublicUser.cs, Models/PublicUserProperty.cs, Models/Notification.cs, Models/Document.cs, Program.cs, DatabaseContexts/ApplicationDBContext.cs, Services/Auth/DwsPolicies.cs, Services/Auth/DwsClaimsTransformation.cs, journal.md
- Confirmed: PublicPortalScheme NOT yet wired in Program.cs; DwsClaimsTransformation runs scheme-blind; PublicUserRecoveryCode NOT in ApplicationDBContext; no rate limiting anywhere in pipeline
- Key risks: DwsClaimsTransformation DB-queries IdentityUser table on every PublicUser request (latency + noise); PublicUser.Status not enforced in policy; IdentityNumber unencrypted at rest; IsHDI requires explicit POPIA consent capture; no account lockout for standalone PublicUser flow; BlobPath must never be served directly; no audit trail for portal auth events
- Status: DONE_WITH_CONCERNS

### 2026-05-03 evening — sqlserver-ef-architect (Sonnet 4.6) — DB/EF critique
- Read: Models/PublicUser.cs, PublicUserProperty.cs, PropertyOwner.cs, PropertyOwnership.cs, LetterIssuance.cs, FileMaster.cs, Notification.cs, Document.cs, Property.cs, DatabaseContexts/ApplicationDBContext.cs (full), Migrations/* (latest 4 files)
- Confirmed: PublicUserProperty missing RequestedDate/EvidenceType/EvidenceDocumentId/RejectionReason — none yet in model or migrations. PublicUser missing all 5 new auth columns (MfaSecret, MfaEnrolledDate, LastLoginDate, FailedLoginAttempts, LockoutUntil). PublicUserRecoveryCode entity absent from DBContext entirely. Auto-cascade override loop at DBContext line 637 WILL silently override the new EvidenceDocumentId FK cascade unless explicitly ordered before the loop. PropertyOwner.IdentityDocumentNumber has no index — auto-match query will scan. LetterIssuance has no index on FileMasterId+IssuedDate — timeline query will scan. No status enum/value-converter in use anywhere in existing schema; strings throughout.
- Key risks: single migration on a table with large Property join risk is low (PublicUserProperty is new/small); however the EvidenceDocumentId FK needs explicit placement before the cascade-override foreach loop. Timeline UNION query is a SQL-side operation not a LINQ UNION — will need FromSqlRaw or two separate EF queries + in-memory merge. Letter auth check traverses FileMaster.PropertyId via EF navigation = implicit join that may not be indexed. Missing MaxLength on new string columns will default to nvarchar(max) blocking index creation.
- Status: DONE_WITH_CONCERNS

### 2026-05-03 evening — controller — critique synthesis + design sections 3-7 written
- Folded all BLOCKING and HIGH severity findings from the three critiques into Sections 2-7 of the spec.
- Two user decisions confirmed during synthesis:
  1. Add `LetterIssuance.RecipientPublicUserId` column (rather than 3-hop join) — simpler auth check, indexed.
  2. Defer `IdentityNumber` encryption to Task 10.3 (Key Vault dependency); add fail-fast Production startup guard rail keyed on `Portal:AllowPlaintextIdentityNumber` config flag.
- User approved Sections 3 (data model deltas) and 4 (user flows) verbatim as presented.
- Sections 5 (hardening), 6 (testing), 7 (staged delivery) written directly into the spec without per-section approval — user requested I proceed autonomously after approving §4.
- Spec written to `docs/superpowers/specs/2026-05-03-external-portal-shell-design.md` (~480 lines).
- Self-review: fixed two minor issues — meta-reference to "the brainstorm" in §4 (rephrased), added `MyEvidenceDocument` to `PropertyClaimController` action list in §2.1 (referenced in §5.4 but missing from the controller list).
- Status: spec ready for user review (next session).

## Retro

What converged: all three critique agents independently flagged the same top-3 BLOCKING items — `PublicPortalScheme` cookie not yet wired, `DwsClaimsTransformation` running scheme-blind, and the cookie-path/file-storage scoping risks. Convergence is the strongest signal; these became the first items written into Section 2.5 + 5.4 of the spec.

What drifted: the EF agent and the security agent disagreed slightly on whether `LetterIssuance.RecipientPublicUserId` should be added or derived via a 3-hop join — EF preferred the column for indexing; security was neutral. Resolved by user decision (add the column).

Failed prompt pattern: none significant. The "Your slice — Design under review" embedded directly in each prompt worked well — agents didn't need to ask follow-up questions about scope, all returned within their token budget.

Cross-boundary verdict (Rule 7): not strictly applicable since this was a design critique, not implementation. But the three independent critiques served as a proxy for the two-sided verdict pattern, and the convergence on top items was the signal that the design's foundations were sound (modulo the listed corrections).

Next-session pickup: user to read `docs/superpowers/specs/2026-05-03-external-portal-shell-design.md` end-to-end. If approved, invoke `superpowers:writing-plans` to produce a per-stage implementation plan (Stages 1-5 are pre-decomposed in §7).

### 2026-05-04 — Stage 1 Task 1 agent (Opus 4.7) — NetArchTest boundary fence skeleton
- Branch confirmed: `feat/external-portal-stage-1` in worktree `.worktrees/external-portal-stage-1`. Read full journal to date and Stage 1 plan task 1.
- Created `Tests/Architecture/PortalBoundaryTests.cs` with two `[Fact]` tests asserting that `dwa_ver_val.Services.Portal.*` and `dwa_ver_val.Areas.ExternalPortal.*` types must not depend on `Microsoft.AspNetCore.Identity.UserManager``1` or `SignInManager``1`. Both tests pass trivially today (those namespaces don't exist yet) and will start enforcing once Stage 2+ adds portal code.
- Modified `Tests/dwa_ver_val.Tests.csproj` — added `<PackageReference Include="NetArchTest.Rules" Version="1.3.2" />` inside the existing `<ItemGroup>` that already holds the other PackageReference entries.
- Step 2 verification (pre-package): `dotnet test --filter PortalBoundaryTests` failed with `error CS0246: The type or namespace name 'NetArchTest' could not be found` — expected.
- Step 4 verification (post-package): `dotnet test --filter PortalBoundaryTests` returns Passed: 2, Failed: 0, Total: 2 (44 ms). Package version 1.3.2 restored cleanly from NuGet.
- Full-suite regression: `dotnet test Tests/dwa_ver_val.Tests.csproj` returns Failed: 19, Passed: 81, Total: 100 — matches the documented baseline (the 19 pre-existing parallel-startup integration failures are unchanged). No new failures introduced.
- Single commit: `Add NetArchTest boundary fence for portal namespaces` (touched only the two files listed in the plan).
- Status: DONE.

### 2026-05-04 — Stage 1 Task 2 agent (Opus 4.7) — portal claim enums
- Branch confirmed: `feat/external-portal-stage-1` in worktree `.worktrees/external-portal-stage-1`. Read journal to date and Stage 1 plan Task 2.
- Verified pre-state: `Models/Enums/` directory did not exist; `Tests/Models/Enums/` did not exist; `Tests/dwa_ver_val.Tests.csproj` only globally imports `Xunit` (no `dwa_ver_val.Models` `<Using>`), so the test file needs an explicit `using dwa_ver_val.Models.Enums;`.
- Step 1: created `Tests/Models/Enums/PortalEnumsTests.cs` with two `[Fact]` tests (`PropertyClaimEvidenceType_HasIDMatchAndTitleDeedUpload`, `PropertyClaimStatus_HasPendingApprovedRejected`). Added `using dwa_ver_val.Models.Enums;` per plan note.
- Step 2 verification (pre-enums): `dotnet test --filter FullyQualifiedName~PortalEnumsTests` failed with `error CS0234: The type or namespace name 'Enums' does not exist in the namespace 'dwa_ver_val.Models'` — expected.
- Step 3: created `Models/Enums/PropertyClaimEvidenceType.cs` (IDMatch=0, TitleDeedUpload=1) and `Models/Enums/PropertyClaimStatus.cs` (Pending=0, Approved=1, Rejected=2). Both in namespace `dwa_ver_val.Models.Enums`.
- Step 4 verification (post-enums): `dotnet test --filter FullyQualifiedName~PortalEnumsTests` returns Passed: 2, Failed: 0, Total: 2 (11 ms).
- Full-suite regression: `dotnet test Tests/dwa_ver_val.Tests.csproj` returns Failed: 19, Passed: 83, Total: 102 — exactly two more passes than the Task 1 baseline (81 → 83). The 19 pre-existing parallel-startup integration failures unchanged.
- Implementation commit: `Add PropertyClaimEvidenceType and PropertyClaimStatus enums` (e9fd585) — touched only the three files listed in the plan.
- Status: DONE.

### 2026-05-04 — Stage 1 Task 3 agent (Opus 4.7) — PublicUser auth columns
- Branch confirmed: `feat/external-portal-stage-1` in worktree `.worktrees/external-portal-stage-1`. Read journal to date and Stage 1 plan Task 3.
- Verified pre-state: `Tests/Helpers/` directory did not exist; `PublicUser` lives in the global namespace (no `namespace` declaration in `Models/PublicUser.cs`), so the test file references `PublicUser` unqualified — confirmed working. Test csproj globally imports `Xunit` only, so the test file needs explicit `using dwa_ver_val.Tests.Helpers;`. PublicUser already has 5 `required` props (EmailAddress, PasswordHash, FirstName, LastName, Status) — builder sets all five.
- Step 1: created `Tests/Helpers/PublicUserBuilder.cs` (`PublicUserBuilder.Active()` factory) and `Tests/Models/PublicUserModelTests.cs` with two `[Fact]` tests verifying default + setter behaviour for the 7 new columns.
- Step 2 verification (pre-columns): `dotnet test --filter FullyQualifiedName~PublicUserModelTests` failed with 17 × CS1061 (`'PublicUser' does not contain a definition for 'MfaSecret'` etc.) — expected.
- Step 3: edited `Models/PublicUser.cs` — added 7 properties (`MfaSecret string?`, `MfaEnrolledDate DateTime?`, `LastLoginDate DateTime?`, `FailedLoginAttempts int`, `LockoutUntil DateTime?`, `LastUsedOtpTimestamp long?`, `HdiConsentGivenDate DateTime?`) immediately after `RegistrationDate` and before `PublicUserProperties`, exactly per plan.
- Step 4 verification (post-columns): `dotnet test --filter FullyQualifiedName~PublicUserModelTests` returns Passed: 2, Failed: 0, Total: 2 (15 ms).
- Full-suite regression: `dotnet test Tests/dwa_ver_val.Tests.csproj` returns Failed: 19, Passed: 85, Total: 104 — exactly two more passes than the Task 2 baseline (83 → 85). The 19 pre-existing parallel-startup integration failures unchanged.
- Implementation commit: `Add MFA, lockout, and HDI consent columns to PublicUser` (351c075) — touched only the three files listed in the plan.
- Status: DONE.

### 2026-05-04 — Stage 1 Task 4 agent (Opus 4.7) — PublicUserProperty claim columns + Status enum
- Branch confirmed: `feat/external-portal-stage-1` in worktree `.worktrees/external-portal-stage-1`. Read journal to date and Stage 1 plan Task 4.
- Pre-state grep `PublicUserProperty` --include='*.cs': 1 active call site found that constructs the entity — `Tests/Models/EntityRelationshipTests.cs:380` (uses `Status = "Approved"`, no `EvidenceType`/`RequestedDate`). Migration snapshot mentions are unaffected by source changes. Confirmed `PropertyClaimEvidenceType.IdMatch` casing per d20b35d (test code in plan already updated).
- Step 1: created `Tests/Models/PublicUserPropertyModelTests.cs` with the two `[Fact]` tests verbatim from the plan (defaults + EvidenceDocumentId/RejectionReason setters).
- Step 2 verification (pre-changes): `dotnet test --filter FullyQualifiedName~PublicUserPropertyModelTests` failed with build errors CS0029/CS0117/CS1503/CS1061 — `Status` is string, missing `EvidenceType`/`EvidenceDocumentId`/`RequestedDate`/`RejectionReason` — expected.
- Step 3: rewrote `Models/PublicUserProperty.cs` per plan VERBATIM — `Status` now `required PropertyClaimStatus`, added `required PropertyClaimEvidenceType EvidenceType`, `Guid? EvidenceDocumentId`, `Document? EvidenceDocument`, `DateTime RequestedDate`, `string? RejectionReason`. File kept top-level (no namespace) to match existing convention, with `using dwa_ver_val.Models.Enums;` directive at top.
- Out-of-plan call-site fix: `Tests/Models/EntityRelationshipTests.cs:380-396` — converted `Status = "Approved"` → `Status = dwa_ver_val.Models.Enums.PropertyClaimStatus.Approved`, added required `EvidenceType = PropertyClaimEvidenceType.IdMatch` + `RequestedDate = DateTime.UtcNow`, and updated the assertion to compare enum values. Folded into the same implementation commit since the file was on the listed grep call-site list per the plan's "fix the call sites" guidance.
- Step 4 verification (post-changes): `dotnet test --filter FullyQualifiedName~PublicUserPropertyModelTests` returns Passed: 2, Failed: 0, Total: 2 (13 ms). `dotnet build` returns 0 Warning(s)/0 Error(s).
- Full-suite regression: `dotnet test Tests/dwa_ver_val.Tests.csproj` returns Failed: 19, Passed: 87, Total: 106 — exactly two more passes than the Task 3 baseline (85 → 87). The 19 pre-existing parallel-startup integration failures unchanged.
- Implementation commit: `Convert PublicUserProperty.Status to enum + add claim evidence columns` (5878f11) — three files: model + new test + the EntityRelationshipTests call-site fix.
- Status: DONE.

### 2026-05-04 — Stage 1 Task 5 agent (Opus 4.7) — LetterIssuance.RecipientPublicUserId
- Branch confirmed: `feat/external-portal-stage-1` in worktree `.worktrees/external-portal-stage-1`. Read journal to date and Stage 1 plan Task 5.
- Pre-state: `LetterIssuance` already has `PortalAcknowledgedByPublicUserId` at line 38 (the user who marked acknowledged) — distinct from the new `RecipientPublicUserId` (the user the letter is ADDRESSED to). Model is top-level (no namespace), matching existing convention.
- Step 1: created `Tests/Models/LetterIssuanceModelTests.cs` with the single `[Fact]` `RecipientPublicUserId_DefaultsToNull_AndIsSettable` verbatim from the plan.
- Step 2 verification (pre-column): `dotnet test --filter FullyQualifiedName~LetterIssuanceModelTests` failed with 3 × CS1061 (`'LetterIssuance' does not contain a definition for 'RecipientPublicUserId'`) — expected.
- Step 3: edited `Models/LetterIssuance.cs` — added `Guid? RecipientPublicUserId` + `PublicUser? RecipientPublicUser` immediately after `PortalAcknowledgedByPublicUserId` (line 39-40), keeping the portal-public-user fields grouped per plan note.
- Step 4 verification (post-column): `dotnet test --filter FullyQualifiedName~LetterIssuanceModelTests` returns Passed: 1, Failed: 0, Total: 1 (7 ms).
- Full-suite regression: `dotnet test Tests/dwa_ver_val.Tests.csproj` returns Failed: 19, Passed: 88, Total: 107 — exactly one more pass than the Task 4 baseline (87 → 88). The 19 pre-existing parallel-startup integration failures unchanged.
- Implementation commit: `Add RecipientPublicUserId addressee column to LetterIssuance` (68d4085) — two files: model + new test. No DBContext config changes (deferred to Task 7 per plan).
- Status: DONE.

### 2026-05-03 — Stage 1 Task 7 agent (Sonnet 4.6) — EF schema config, indexes, cascade exemption

- Branch confirmed: `feat/external-portal-stage-1` in worktree `.worktrees/external-portal-stage-1`. Read journal to date, plan Task 7, and full `ApplicationDBContext.cs`.
- Baseline: 90 passed / 19 failed (109 total). Cascade loop confirmed at line 634 (not 637 as plan stated — shifted by earlier edits).
- Step 1: created `Tests/DatabaseContexts/PortalSchemaConfigurationTests.cs` with 4 `[Fact]` tests per plan.
- Step 2 verification (pre-implementation): build failed with `CS1061` on `PublicUserRecoveryCodes` — expected.
- Step 3a: added `DbSet<PublicUserRecoveryCode> PublicUserRecoveryCodes` at line 80 (after `PublicUserProperties`).
- Step 3b: inserted all new `OnModelCreating` config immediately before the cascade loop — `PublicUserRecoveryCode` key/columns/Cascade FK/filtered index; `PublicUser` column types/MaxLength/defaults/unique index/filtered index/check constraint; `PublicUserProperty` enum converters/column types/MaxLength/EvidenceDocumentId SetNull FK/three indexes/check constraint; `LetterIssuance` RecipientPublicUser SetNull FK + timeline composite index; `Notification` unread filtered index; `PropertyOwner` IdentityDocumentNumber filtered index.
- Step 3c deviation: the plan's constraint-name exemption strategy (`GetConstraintName()`) doesn't work because EF Core hasn't materialised constraint names at loop-execution time — they're generated lazily. Replaced with a `HashSet<(Type Dependent, Type Principal, string FkProperty)>` key that is available at loop time. This is mechanically equivalent but more robust.
- Step 4 first run: 2/4 passed; `RecoveryCode_FK_IsCascade` and `EvidenceDocument_FK_IsSetNull` both failed with `Restrict` — proved the original plan's constraint-name approach would never have worked. Applied the type-triple exemption fix.
- Step 4 second run: 4/4 passed. Full suite: 94 passed / 20 failed — one new failure: `All_Delete_Behaviors_Are_Restrict` in `ApplicationDBContextTests.cs`. That test was added in a prior task and asserts every FK is Restrict. Updated it to `All_Delete_Behaviors_Are_Restrict_Except_Whitelisted` with the same allow-list logic.
- Final full suite: 94 passed / 19 failed (113 total). Matches plan target.
- Files changed: `DatabaseContexts/ApplicationDBContext.cs`, `Tests/DatabaseContexts/PortalSchemaConfigurationTests.cs`, `Tests/DatabaseContexts/ApplicationDBContextTests.cs` (guard test update — one deviation from the plan's two-file list, justified by the pre-existing test collision).
- Implementation commit: `db4f5da` — `Configure portal schema: indexes, check constraints, cascade exemption`.
- Status: DONE_WITH_CONCERNS (cascade exemption strategy deviated from plan; guard test required update; both are improvements, not regressions).

### 2026-05-04 — Stage 1 Task 6 agent (Opus 4.7) — PublicUserRecoveryCode entity
- Branch confirmed: `feat/external-portal-stage-1` in worktree `.worktrees/external-portal-stage-1`. Read journal to date and Stage 1 plan Task 6.
- Pre-state: neither `Models/PublicUserRecoveryCode.cs` nor `Tests/Models/PublicUserRecoveryCodeModelTests.cs` existed. `PublicUser` confirmed top-level (no namespace) — matched for new entity.
- Step 1: created `Tests/Models/PublicUserRecoveryCodeModelTests.cs` with the two `[Fact]` tests verbatim from the plan (`NewRecoveryCode_HasExpectedDefaults`, `Redemption_FlipsUsedAndUsedDate`).
- Step 2 verification (pre-entity): `dotnet test --filter FullyQualifiedName~PublicUserRecoveryCodeModelTests` failed with 2 × CS0246 (`type or namespace name 'PublicUserRecoveryCode' could not be found`) — expected.
- Step 3: created `Models/PublicUserRecoveryCode.cs` per plan VERBATIM — top-level (no namespace), 8 properties: `Id Guid`, `PublicUserId Guid`, `PublicUser PublicUser?`, `CodeHash required string`, `Used bool`, `UsedDate DateTime?`, `CreatedDate DateTime`, `ExpiresDate DateTime?`. Cascade-delete config explicitly deferred to Task 7.
- Step 4 verification (post-entity): `dotnet test --filter FullyQualifiedName~PublicUserRecoveryCodeModelTests` returns Passed: 2, Failed: 0, Total: 2 (13 ms).
- Full-suite regression: `dotnet test Tests/dwa_ver_val.Tests.csproj` returns Failed: 19, Passed: 90, Total: 109 — exactly two more passes than the Task 5 baseline (88 → 90). The 19 pre-existing parallel-startup integration failures unchanged.
- Implementation commit: `Add PublicUserRecoveryCode entity` (b4e3997) — two files: model + new test.
- Status: DONE.

### 2026-05-04 — Stage 1 Task 8 agent (Sonnet 4.6) — EF migration generate + apply

- Branch confirmed: `feat/external-portal-stage-1` in worktree `.worktrees/external-portal-stage-1`. Read full journal.
- Pre-state: Docker SQL Server container healthy on port 1433. Last migration `20260428122725_PropertyLineageSuccessor`. `dotnet ef database update` correctly rejected with `PendingModelChangesWarning` — Task 7 config committed but not yet migrated.
- Step 2: `dotnet ef migrations add ExternalPortalShellPortalAuthAndClaims` succeeded. Timestamp: `20260504094618`. EF warned "may result in loss of data" — expected; the `Status` column on `PublicUserProperties` is narrowed from `nvarchar(max)` to `nvarchar(20)` (enum value converter). Table is currently empty in dev DB, so no actual data loss.
- Step 3 — all 11 inspection items PASSED AS GENERATED, no manual edits required:
  - `FailedLoginAttempts defaultValue: 0` — present (line 48)
  - All `DateTime` columns use `datetime2(0)` — confirmed for 7 columns across `PublicUsers` and `PublicUserRecoveryCodes`
  - `MaxLength` on all strings — `MfaSecret` 256, `EvidenceType` 20, `RejectionReason` 1000, `CodeHash` 128 — all correct
  - Filtered unique index `[Status] <> 'Rejected'` — present
  - Pending-claims filtered index `[Status] = 'Pending'` — present
  - Unread-notifications filtered index `[IsRead] = 0 AND [PublicUserId] IS NOT NULL` — present
  - `IdentityDocumentNumber` filtered index `[IdentityDocumentNumber] IS NOT NULL` — present
  - Two `AddCheckConstraint` calls (`CK_PublicUsers_HdiConsent`, `CK_PublicUserProperties_EvidenceDocumentId`) — both present
  - `PublicUserRecoveryCodes` FK is `ReferentialAction.Cascade` — correct
  - `EvidenceDocumentId` FK is `ReferentialAction.SetNull` — correct
  - `RecipientPublicUserId` FK is `ReferentialAction.SetNull` — correct
  - `Down()` drops everything cleanly — confirmed
- Step 4: `dotnet ef database update` applied cleanly. All DDL executed without errors — `ALTER TABLE`, `CREATE TABLE PublicUserRecoveryCodes`, all `CREATE INDEX ... WHERE [...]` filtered indexes, both `ALTER TABLE ... ADD CONSTRAINT ... CHECK`, and both `ALTER TABLE ... ADD CONSTRAINT ... FOREIGN KEY ... ON DELETE SET NULL`. Migration row inserted into `__EFMigrationsHistory`.
- Step 5: full suite `dotnet test Tests/dwa_ver_val.Tests.csproj` — Failed: 19, Passed: 95, Total: 114. Exact pre-task baseline maintained.
- Step 6: implementation commit `2ceabbb` — three files: new `.cs`, new `.Designer.cs`, modified `ApplicationDBContextModelSnapshot.cs`.
- Status: DONE. No manual migration edits required — the Task 7 EF config was complete and correct, and EF scaffolded the migration cleanly.

### 2026-05-04 — Stage 1 Task 7 follow-up agent (Opus 4.7) — code review fix-ups
- Branch confirmed: `feat/external-portal-stage-1` in worktree `.worktrees/external-portal-stage-1`. Read journal to date and the two Important nits raised by review agent `afa457f919a90520f`.
- Fix 1 (DRY): added `public static readonly IReadOnlyCollection<(Type Dependent, Type Principal, string FkProperty, DeleteBehavior Behavior)> NonRestrictForeignKeys` on `ApplicationDBContext` (after class declaration, before constructor). The OnModelCreating cascade-override loop now derives its `cascadeFkExemptions` HashSet from this list via `Select(...).ToHashSet()`. The `All_Delete_Behaviors_Are_Restrict_Except_Whitelisted` test in `ApplicationDBContextTests` now derives its `allowList` Dictionary from the same source via `ToDictionary(...)`, replacing the LINQ `Where`/`FirstOrDefault` matching with a cleaner `TryGetValue`. Adding a 4th non-Restrict FK now requires a single edit.
- Fix 2 (test gap): added a 5th `[Fact]` to `PortalSchemaConfigurationTests.cs`: `RecipientPublicUserId_FK_To_PublicUser_IsSetNull_NotRestrict` — same pattern as `EvidenceDocument_FK_To_Document_IsSetNull_NotRestrict`, asserts `LetterIssuance.RecipientPublicUserId → PublicUser` is `DeleteBehavior.SetNull`.
- Verification: `dotnet build` green (0 errors, 6 pre-existing warnings unchanged). `dotnet test --filter "FullyQualifiedName~PortalSchemaConfigurationTests"` returns 5/5 passed (was 4/4). `dotnet test --filter "All_Delete_Behaviors_Are_Restrict_Except_Whitelisted"` returns 1/1 passed. Full suite: 95 passed / 19 failed / 114 total — exactly one more pass than the Task 7 baseline (94 → 95). The 19 pre-existing parallel-startup integration failures unchanged.
- Implementation commit: `79cbb0f` — three files (DBContext + two test files). Journal commit follows separately.
- Status: DONE.

### 2026-05-04 — Stage 1 Task 9 agent (Opus 4.7) — NotFoundException helper
- Branch confirmed: `feat/external-portal-stage-1` in worktree `.worktrees/external-portal-stage-1`. Read journal to date and Stage 1 plan Task 9.
- Pre-state: `Helpers/` directory at project root did not exist; `Tests/Helpers/` exists (PublicUserBuilder.cs etc.). Test file needed both `using Xunit;` and `using dwa_ver_val.Helpers;` — plan only listed Xunit, added the second per plan note.
- Step 1: created `Tests/Helpers/NotFoundExceptionTests.cs` with two `[Fact]` tests (`Constructor_SetsMessage`, `IsAnException`) verbatim from plan + the missing `using dwa_ver_val.Helpers;`.
- Step 2 verification (pre-type): `dotnet test --filter FullyQualifiedName~NotFoundExceptionTests` failed with `error CS0234: The type or namespace name 'Helpers' does not exist in the namespace 'dwa_ver_val'` — expected.
- Step 3: created `Helpers/NotFoundException.cs` per plan VERBATIM — namespace `dwa_ver_val.Helpers`, single `(string message)` constructor, XML doc comment about HTTP 404 mapping by `PortalExceptionHandler`. Created the new `Helpers/` directory at project root.
- Step 4 verification (post-type): `dotnet test --filter FullyQualifiedName~NotFoundExceptionTests` returns Passed: 2, Failed: 0, Total: 2 (12 ms).
- Full-suite regression: `dotnet test Tests/dwa_ver_val.Tests.csproj` returns Failed: 19, Passed: 97, Total: 116 — exactly two more passes than the Task 7 follow-up baseline (95 → 97). Matches the plan-stated 97/19 target. The 19 pre-existing parallel-startup integration failures unchanged.
- Implementation commit: `40597a8` — `Add NotFoundException helper for 404-mapped portal errors` (two files: production type + test). Journal commit follows separately.
- Status: DONE. No consumers yet (Tasks 10 + 15 will reference this type).

### 2026-05-04 — Stage 1 Task 10 agent (Opus 4.7) — IPublicUserPropertyAccessor row-level scoping spine
- Branch confirmed: `feat/external-portal-stage-1` in worktree `.worktrees/external-portal-stage-1`. Read journal to date, Stage 1 plan Task 10, plus existing helpers (`TestDbContextFactory`, `PublicUserBuilder`, `NotFoundException`, both portal enums) and the structural details of `Property` (top-level, `PropertyId` PK) and `FileMaster` (top-level, `FileMasterId` PK).
- Pre-state: `Services/Portal/` and `Tests/Services/Portal/` did not exist. `PropertyClaimEvidenceType.IdMatch` casing confirmed (matches plan test code).
- Step 1: created `Tests/Services/Portal/Auth/PublicUserPropertyAccessorTests.cs` with the 5 `[Fact]` tests from the plan.
- Step 2 verification (pre-implementation): `dotnet test --filter FullyQualifiedName~PublicUserPropertyAccessorTests` failed with `error CS0234: 'Portal' does not exist in 'dwa_ver_val.Services'` — expected.
- Steps 3 + 4: created `Services/Portal/Auth/IPublicUserPropertyAccessor.cs` (interface) and `Services/Portal/Auth/PublicUserPropertyAccessor.cs` (impl) verbatim from plan. Implementation depends only on `ApplicationDBContext` + `NotFoundException` + `PropertyClaimStatus.Approved` — no `UserManager`/`SignInManager`, so the Task 1 NetArchTest fence stays clean.
- Step 4 second build: failed with `CS9035` for 7 `required` members on `FileMaster` (`RegistrationNumber`, `SurveyorGeneralCode`, `PrimaryCatchment`, `QuaternaryCatchment`, `FarmName`, `FarmNumber`, `RegistrationDivision`, `FarmPortion`) that the plan's test code didn't account for. Added a private `NewFileMaster(Guid propertyId)` helper at the top of the test class with sensible placeholder values, and replaced the three `new FileMaster { FileMasterId = ..., PropertyId = ... }` block initializers with calls to it. This is the documented "plan-authoring discipline" gap (memory: feedback_plan_authoring) — call sites needed to satisfy `required` props.
- Step 5 verification (post-implementation): `dotnet test --filter FullyQualifiedName~PublicUserPropertyAccessorTests` returns Passed: 5, Failed: 0, Total: 5 (843 ms).
- Full-suite regression: `dotnet test Tests/dwa_ver_val.Tests.csproj` returns Failed: 19, Passed: 102, Total: 121 — exactly five more passes than the Task 9 baseline (97 → 102). Matches the plan-stated 102/19 target. The 19 pre-existing parallel-startup integration failures unchanged.
- `PortalBoundaryTests` re-checked: 2/2 passing (NetArchTest fence still clean; new `Services/Portal/Auth/*` types take no Identity dependencies).
- Implementation commit: `8868ef9` — `Add IPublicUserPropertyAccessor row-level scoping spine` (3 files: interface + impl + test). Journal commit follows separately.
- Status: DONE.

### 2026-05-04 — security review fix-up (Task 10) — added cross-user isolation test + Stage 2 constraints
- Added `AssertHasAccessToFileMasterAsync_ThrowsForOtherUsersApprovedLink` test (cross-user isolation: user B's Approved link does NOT grant user A access). 6 tests now pass in `PublicUserPropertyAccessorTests`. Full suite 103/19.
- **Stage 2 constraint #1 (load-bearing on Task 15):** `PortalExceptionHandler` MUST return a generic `ProblemDetails` with NO `Exception.Message` forwarded — `NotFoundException` messages contain `FileMasterId` and `PublicUserId` GUIDs that must not bleed to HTTP response bodies. Internal logging is fine.
- **Stage 2 constraint #2 (NetArchTest expansion):** The current boundary fence (`Tests/Architecture/PortalBoundaryTests.cs`) blocks `Areas/ExternalPortal/*` from `UserManager<ApplicationUser>` but NOT from direct `ApplicationDBContext`. Stage 2 must add a fence prohibiting `Areas/ExternalPortal/*` types from depending on `ApplicationDBContext` directly — they must go through `Services/Portal/*` services. Note: the `Services/Portal/Auth/PublicUserPropertyAccessor` itself depends on `ApplicationDBContext` (intentionally — it's the wrapper); the fence applies to controllers/views, not the wrapper.
- **Stage 2 constraint #3 (recommended pattern):** Every Stage 2 controller that receives a child-record ID (LetterIssuanceId, NotificationId, DocumentId, CommentId, etc.) must call `AssertHasAccessToFileMasterAsync` with the resolved `FileMasterId` BEFORE returning data — Pattern A from the security review. Avoid Pattern B (typed assert methods per child entity) which scales poorly.

### 2026-05-04 — Stage 1 Task 11 agent (Opus 4.7) — IEmailSender + LoggingEmailSender
- Branch confirmed: `feat/external-portal-stage-1` in worktree `.worktrees/external-portal-stage-1`. Read journal to date and Stage 1 plan Task 11.
- Pre-state: neither `Services/Infrastructure/` nor `Tests/Services/Infrastructure/` existed. Confirmed `Services/` already had `Audit`, `Auth`, `Letters`, `Portal`, `Workflow` siblings; `Tests/Services/` mirrored that layout. New cross-cutting `Infrastructure/Email/` directory created at both locations.
- Step 1: created `Tests/Services/Infrastructure/Email/LoggingEmailSenderTests.cs` with two `[Fact]` tests verbatim from the plan (Information-log verification with Moq's `It.IsAnyType` matcher; empty-To returns false).
- Step 2 verification (pre-impl): `dotnet test --filter FullyQualifiedName~LoggingEmailSenderTests` failed with `error CS0234: The type or namespace name 'Infrastructure' does not exist in the namespace 'dwa_ver_val.Services'` — expected.
- Step 3: created three files under `Services/Infrastructure/Email/` verbatim from plan — `EmailMessage` (4 props, 3 `required`), `IEmailSender` (returns `Task<bool>`, must-not-throw contract documented in XML), `LoggingEmailSender` (LogWarning + return false on empty `To`; LogInformation + return true otherwise). The XML doc on `LoggingEmailSender` references "Program.cs startup warning and design spec §5.6" — startup warning to be wired in Task 16 (DI registration), spec section already exists.
- Step 4 verification (post-impl): `dotnet test --filter FullyQualifiedName~LoggingEmailSenderTests` returns Passed: 2, Failed: 0, Total: 2 (62 ms).
- Full-suite regression: `dotnet test Tests/dwa_ver_val.Tests.csproj` returns Failed: 19, Passed: 105, Total: 124 — exactly two more passes than the Task 10 follow-up baseline (103 → 105). Matches the plan-stated 105/19 target. The 19 pre-existing parallel-startup integration failures unchanged.
- Implementation commit: `f94bfd6` — `Add IEmailSender abstraction with LoggingEmailSender dev impl` (4 files: 3 production + 1 test). Journal commit follows separately.
- DI registration of `IEmailSender → LoggingEmailSender` deferred to Task 16 (Program.cs) per plan. Stage 2 will consume this for registration confirmation, password reset, MFA enrolment, claim approval/rejection emails. Internal portal will reuse the same abstraction.
- Status: DONE.
