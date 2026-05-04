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

### 2026-05-04 — Stage 1 Task 12 agent (Opus 4.7) — IFileStorage + LocalDiskFileStorage
- Branch confirmed: `feat/external-portal-stage-1` in worktree `.worktrees/external-portal-stage-1`. Read journal to date and Stage 1 plan Task 12.
- Pre-state: `Services/Infrastructure/Email/` and `Tests/Services/Infrastructure/Email/` already existed from Task 11 (the `Infrastructure` parent dirs were already created); the new `Storage/` subdirectories did not exist. `Tests/dwa_ver_val.Tests.csproj` globally imports `Xunit` only, so the test file uses an explicit `using dwa_ver_val.Services.Infrastructure.Storage;` per plan.
- Step 1: created `Tests/Services/Infrastructure/Storage/LocalDiskFileStorageTests.cs` verbatim from the plan — 5 `[Fact]` tests using temp-dir round-trip pattern with `IDisposable` cleanup.
- Step 2 verification (pre-impl): `dotnet test --filter FullyQualifiedName~LocalDiskFileStorageTests` failed with `error CS0234: The type or namespace name 'Storage' does not exist in the namespace 'dwa_ver_val.Services.Infrastructure'` and `CS0246: The type or namespace name 'LocalDiskFileStorage' could not be found` — expected.
- Step 3: created three files under `Services/Infrastructure/Storage/` verbatim from plan — `StoredFileResult` (4 `required` props), `IFileStorage` (4-method async interface with XML docs about NEVER returning absolute paths), `LocalDiskFileStorage` (single-pass `CryptoStream` for SHA-256 during write, `year/month/{guid}{ext}` partitioning with forward slashes, `ResolveAbsolute` rejects `..` substring + rooted paths). Storage root `_root` is the constructor arg — Task 16 will pass `{ContentRootPath}/portal-uploads/` keeping it OUTSIDE `wwwroot/` per design spec §5.4.
- Step 4 verification (post-impl): `dotnet test --filter FullyQualifiedName~LocalDiskFileStorageTests` returns Passed: 5, Failed: 0, Total: 5 (25 ms).
- Full-suite regression: `dotnet test Tests/dwa_ver_val.Tests.csproj` returns Failed: 19, Passed: 110, Total: 129 — exactly five more passes than the Task 11 baseline (105 → 110). Matches the plan-stated 110/19 target. The 19 pre-existing parallel-startup integration failures unchanged.
- Implementation commit: `e454eb3` — `Add IFileStorage abstraction with LocalDiskFileStorage impl` (4 files: 3 production + 1 test). Journal commit follows separately.
- DI registration of `IFileStorage → LocalDiskFileStorage(rootedAt {ContentRootPath}/portal-uploads/)` deferred to Task 16 per plan. Stage 3 will consume this for portal title-deed evidence uploads (`PropertyClaimController.MyEvidenceDocument` etc.) — virus scanning to be added in a later task.
- Status: DONE.

### 2026-05-04 — Stage 1 Task 13 agent (Opus 4.7) — DwsClaimsTransformation early-return for non-Identity schemes
- Branch confirmed: `feat/external-portal-stage-1` in worktree `.worktrees/external-portal-stage-1`. Read journal to date and Stage 1 plan Task 13 (REVISED in 69eb4e3 to modify-not-create).
- Pre-state: `Tests/Services/Auth/DwsClaimsTransformationTests.cs` already existed with 2 tests using `authenticationType: "Test"` (lines 71, 110). `Services/Auth/DwsClaimsTransformation.cs` had `using Microsoft.AspNetCore.Authentication;` but NOT `Microsoft.AspNetCore.Identity` — plan note saying the directive was "already present" was incorrect; needed to add it.
- Step 1: added `using Microsoft.AspNetCore.Identity;` to test file. Replaced both `authenticationType: "Test"` with `authenticationType: IdentityConstants.ApplicationScheme`.
- Step 2: appended two new `[Fact]` tests verbatim from plan — `TransformAsync_DoesNothing_WhenSchemeIsNotIdentityApplication` and `TransformAsync_DoesNothing_ForUnauthenticatedPrincipal` — placed before the existing `private record ExpectedClaims(...)` declaration.
- Step 3 verification (pre-impl): `dotnet test --filter FullyQualifiedName~DwsClaimsTransformationTests` — 4/4 passed (NOT 3/4 as plan anticipated). The new `WhenSchemeIsNotIdentityApplication` test passes against the unmodified production code because the random `NameIdentifier` GUID doesn't match any user in the empty in-memory DB, so the existing `if (user is null) return principal;` guard short-circuits before any claims are added. Test still validates correct behaviour after the early-return is added — just doesn't fail "loud" without it. Documented and proceeded.
- Step 4: edited `Services/Auth/DwsClaimsTransformation.cs` — added `using Microsoft.AspNetCore.Identity;` at top (was missing) and inserted the early-return block immediately after the `IsAuthenticated` guard, before the `Marker` check, with the two-line comment explaining why portal/other schemes don't need DWS-staff enrichment.
- Step 5 verification (post-impl): `dotnet test --filter FullyQualifiedName~DwsClaimsTransformationTests` — 4/4 passed (776 ms). Full suite: 112 passed / 19 failed / 131 total — exactly two more passes than the Task 12 baseline (110 → 112). Matches the plan-stated 112/19 target. The 19 pre-existing parallel-startup integration failures unchanged.
- Implementation commit: `43547a7` — `DwsClaimsTransformation: early-return for non-Identity schemes` (2 files: production + test). Journal commit follows separately.
- Status: DONE_WITH_CONCERNS — the new "WhenSchemeIsNotIdentityApplication" test would still pass even WITHOUT the production change (because the user-not-found guard catches it first). To make it strictly observable, a future hardening could seed a matching user and assert no claims appear; current test verifies the *outcome* but not the *path*. The production change itself is correct and matches the plan verbatim.

### 2026-05-04 — Stage 1 Task 13 follow-up (Opus 4.7) — strengthen early-return test (load-bearing)
- Branch confirmed: `feat/external-portal-stage-1` in worktree `.worktrees/external-portal-stage-1`. Addresses the DONE_WITH_CONCERNS from Task 13 — the test now seeds a real `ApplicationUser` whose `Id` matches the principal's `NameIdentifier`, so the only way the "no displayName claim" assertion can hold is if the early-return prevents the DB lookup. Without the early-return, the lookup would find the user and add the claim.
- Edited `Tests/Services/Auth/DwsClaimsTransformationTests.cs` — replaced the body of `TransformAsync_DoesNothing_WhenSchemeIsNotIdentityApplication` verbatim from the follow-up brief: persists `ApplicationUser` with `userId`, then transforms a `ClaimsIdentity(authenticationType: "PublicPortalScheme")` carrying that same `userId`, and asserts no `displayName` / `employeeNumber` / `dws:augmented` claims appear. The other test (`TransformAsync_DoesNothing_ForUnauthenticatedPrincipal`) is untouched per brief — it tests a separate code path (the `IsAuthenticated` guard).
- Sanity check (load-bearing proof): temporarily commented out the early-return block in `Services/Auth/DwsClaimsTransformation.cs`. Filtered test run failed exactly the new test (`Assert.False()` for `displayName`: Expected False, Actual True) while the other 3 tests in the class still passed. Restored the early-return; filtered re-run returns 4/4 passing. This is the proof the previous Task 13 journal entry called out as missing.
- Full-suite regression: `dotnet test` returns Failed: 19, Passed: 112, Total: 131 — unchanged from Task 13 baseline (no test count delta; we only strengthened an existing test). The 19 pre-existing parallel-startup integration failures unchanged.
- Implementation commit: `427ffa8` — `Task 13 follow-up: strengthen early-return test to use real persisted user` (1 file: test only; production already correct from Task 13). Journal commit follows separately.
- Status: DONE — clears the Task 13 DONE_WITH_CONCERNS. The early-return is now genuinely covered by a test that fails when the production code is regressed.

### 2026-05-04 — Stage 1 Task 14 agent (Opus 4.7) — portal cookie + policy + rate-limit configurators
- Branch confirmed: `feat/external-portal-stage-1` in worktree `.worktrees/external-portal-stage-1`. Read journal to date and Stage 1 plan Task 14. Plan explicitly marks this as a no-test task — these are pure configuration classes whose wiring is integration-tested in Task 17.
- Pre-state: `Services/Portal/Auth/` directory existed from Task 10 with 2 files (`IPublicUserPropertyAccessor.cs`, `PublicUserPropertyAccessor.cs`). None of the three new configurator files existed. Pre-task baseline: 112 passed / 19 failed.
- Step 1: created `Services/Portal/Auth/PortalCookieOptions.cs` verbatim from the plan — 3 string consts (`SchemeName="PublicPortalScheme"`, `CookieName=".dwa.PortalAuth"`, `CookiePath="/ExternalPortal"`) and `Configure(CookieAuthenticationOptions)` that sets HttpOnly + SameSite.Lax + SecurePolicy.Always + 30-min sliding expiration + portal-scoped login/logout/access-denied paths. The cookie path scoping (`/ExternalPortal`) is the load-bearing piece that prevents the portal cookie from leaking into internal-portal requests.
- Step 2: created `Services/Portal/Auth/PortalPolicies.cs` verbatim from plan — 3 policy name consts (`PortalAuthenticated`, `PortalRegistrationComplete`, `PortalMfaPending`) and `Configure(AuthorizationOptions)` adding all three with `AddAuthenticationSchemes(PortalCookieOptions.SchemeName)` + `RequireAuthenticatedUser()`. Stage 1 stub: all three policies have identical "authenticated only" requirements; Stage 2 will add the `RequireClaim("portal:email-confirmed", "true")` / `RequireClaim("portal:mfa-enrolled", "true")` / `RequireClaim("portal:status", "Active")` requirements once `IPortalSignInService` stamps those claims at login.
- Step 3: created `Services/Portal/Auth/PortalRateLimitPolicies.cs` verbatim from plan — 3 partition policies: `AuthStrict` (5 / 15 min, IP-keyed) for login/MFA/password-reset; `AuthModerate` (3 / 1 hr, IP-keyed) for registration; `WriteDefault` (30 / 1 min, public-user-id-or-IP-keyed) for general writes. `RejectionStatusCode = 429`. Added the `using Microsoft.AspNetCore.Http;` directive per plan note (needed for `StatusCodes` and `HttpContext`).
- Step 4 verification: `dotnet build` returns 0 errors / 6 warnings (pre-existing, no new warnings). `dotnet test --filter FullyQualifiedName~PortalBoundaryTests` returns 2/2 passed — the NetArchTest fence stays clean (none of the new types depend on `UserManager` / `SignInManager`). Full suite `dotnet test Tests/dwa_ver_val.Tests.csproj` returns Failed: 19, Passed: 112, Total: 131 — exactly matches the pre-task baseline (no new tests added, none broken). The 19 pre-existing parallel-startup integration failures unchanged.
- Implementation commit: `39dec1e` — `Add portal cookie + policy + rate-limit configurators` (3 files, 112 insertions). Journal commit follows separately.
- Status: DONE. Task 16 (Program.cs wiring) consumes all three; Task 17 integration tests verify the wiring end-to-end via WebApplicationFactory.

### 2026-05-04 — Stage 1 Task 15 agent (Opus 4.7) — PortalExceptionHandler (IExceptionHandler)
- Branch confirmed: `feat/external-portal-stage-1` in worktree `.worktrees/external-portal-stage-1`. Read journal to date and Stage 1 plan Task 15 plus the Stage 2 security-review constraint at line 228 ("MUST NOT serialise `Exception.Message` to the HTTP response body").
- Pre-state: `Services/Portal/Auth/` had 5 files (Task 14 configurators + Task 10 accessor); `Tests/Services/Portal/Auth/` had `PublicUserPropertyAccessorTests.cs`. Pre-task baseline: 112 passed / 19 failed.
- Step 1: created `Tests/Services/Portal/Auth/PortalExceptionHandlerTests.cs` with the 3 `[Fact]` tests verbatim from plan (NotFound→404 on portal path; non-portal returns false; generic→500 on portal path).
- Step 2 verification (pre-impl): `dotnet test --filter FullyQualifiedName~PortalExceptionHandlerTests` failed with 6 × CS0246 (`'PortalExceptionHandler' could not be found`) — expected.
- Step 3: created `Services/Portal/Auth/PortalExceptionHandler.cs` verbatim from plan — `IExceptionHandler` with `PortalPathPrefix = "/ExternalPortal"` const + `StartsWithSegments(..., OrdinalIgnoreCase)` path-prefix gate. NotFoundException → 404 (LogInformation), generic → 500 (LogError with full exception). **Crucially: only sets `httpContext.Response.StatusCode` and logs — NEVER writes to the response body, per the Stage 2 security-review constraint that NotFoundException messages contain GUIDs that must not bleed.**
- Step 4 verification (post-impl): `dotnet test --filter FullyQualifiedName~PortalExceptionHandlerTests` returns Passed: 3, Failed: 0, Total: 3 (24 ms).
- `PortalBoundaryTests` re-checked: 2/2 passing — the new handler depends only on `IExceptionHandler`/`HttpContext`/`ILogger`/`NotFoundException`, no `UserManager` / `SignInManager` references, fence stays clean.
- Full-suite regression: `dotnet test Tests/dwa_ver_val.Tests.csproj` returns Failed: 19, Passed: 115, Total: 134 — exactly three more passes than the Task 14 baseline (112 → 115). Matches the plan-stated 115/19 target. The 19 pre-existing parallel-startup integration failures unchanged.
- Implementation commit: `32114d0` — `Add PortalExceptionHandler with NotFound + generic mapping` (2 files: production + test). Journal commit follows separately.
- Confirmed handler does NOT write any response body content — verified by code inspection: only `httpContext.Response.StatusCode = ...` assignments + `_logger.LogXxx(...)` calls. No `WriteAsync`, `WriteAsJsonAsync`, or `Body` access anywhere. Stage 2 security-review constraint #1 satisfied.
- Status: DONE.

### 2026-05-04 — Stage 1 Task 16 (controller-completed) — Program.cs wiring
- Implementer agent timed out mid-report after Step 6 (pipeline middleware). Implementation was actually complete: the diff to Program.cs matched the plan exactly (all 6 sub-steps applied). Controller ran the smoke test and committed.
- Curl smoke from a freshly-restarted local server (port 5099): `GET /` → 302 (existing redirect to `/Account/Login`), `GET /Account/Login` → 200 (internal login renders), `GET /ExternalPortal/Account/Login` → 404 (no portal controller yet — correct), `GET /ExternalPortal/Dashboard` → 404 (NOT a redirect to /Account/Login — confirms area route exists and portal scheme is properly scoped, not silently catching portal paths).
- Test suite: 134 / 0 / 134 (was 115 / 19 / 134 before this task — the 19 pre-existing parallel-startup integration failures resolved themselves between Tasks 13 and 16; likely the dev DB warmed up across earlier test runs and the seeder race no longer fires because `Provinces.AnyAsync()` returns true for all parallel hosts now).
- Implementation commit: `9da3f62 Wire portal cookie scheme, rate limiter, exception handler, infra DI` (85+/26-, single file).
- Status: DONE.

### 2026-05-04 — Stage 1 Task 17 agent (Opus 4.7) — PortalIntegrationTestFixture + cookie-scheme isolation tests
- Branch confirmed: `feat/external-portal-stage-1` in worktree `.worktrees/external-portal-stage-1`. Read journal to date and Stage 1 plan Task 17. Pre-task baseline: 134 passed / 0 failed.
- Pre-state: `Tests/Integration/` had 4 files (`AuthAuditTests`, `ForgotPasswordFlowTests`, `IdentityFlowTests`, `IntegrationTestBase`). Confirmed `IntegrationTestFixture` lives in `IntegrationTestBase.cs` and exposes `ConfigureWebHost(IWebHostBuilder)` as a `protected override` — the new fixture extends it cleanly via `base.ConfigureWebHost(builder)` then layers a second `services.Configure<CookieAuthenticationOptions>` keyed on `PortalCookieOptions.SchemeName`.
- Step 1: created `Tests/Integration/PortalIntegrationTestFixture.cs` verbatim from plan.
- Step 2: created `Tests/Integration/PortalCookieSchemeIsolationTests.cs` verbatim from plan (3 `[Fact]` tests).
- Step 3 first run: Test 1 (`UnauthenticatedRequest_ToInternalAdminRoute_RedirectsToInternalLogin`) FAILED with `Expected Found, Actual NotFound`. Diagnosed: `UsersController` has `[Route("Admin/[controller]/[action]")]` on the class, so the path is `/Admin/Users/Index` (matching `IdentityFlowTests.cs:33,49,61`), not `/Admin/Users` as the plan suggested. Edited the test to probe `/Admin/Users/Index`. Tests 2 + 3 passed first time.
- Step 3 second run: 3/3 passed (2 s).
- Step 4 (full suite): `dotnet test Tests/dwa_ver_val.Tests.csproj` returns Failed: 0, Passed: 137, Total: 137 — exactly matches the plan-stated 137/0 target. Test 2 (`/ExternalPortal/Dashboard`) returns 404 (NOT a redirect) — confirms the security-critical assertion that the default Identity scheme isn't wrongly answering portal paths. The Task 16 area-route + cookie-scope wiring is verified end-to-end.
- Implementation commit: `b6ab9bc` — `Add portal integration fixture + cookie scheme isolation tests` (2 files, 84 insertions). Journal commit follows separately.
- Status: DONE_WITH_CONCERNS — single deviation from plan: probed `/Admin/Users/Index` instead of `/Admin/Users` (plan-authoring miss; the latter route doesn't exist). Functionally identical assertion, just the correct URL. All 3 tests pass and the security-critical isolation check (Test 2) is verified.

### 2026-05-04 — Stage 1 implementation COMPLETE
- All 18 plan tasks (and 4 fix-up follow-ups for plan-authoring misses or review-flagged issues) executed via subagent-driven workflow.
- Final test suite: **137 passed / 0 failed / 137 total**. Full green. (Began at 79 passed / 19 failed; the 19 pre-existing parallel-startup integration failures resolved themselves between Tasks 13 and 16.)
- New files created (Stage 1 production):
  - `Models/Enums/PropertyClaimEvidenceType.cs`, `Models/Enums/PropertyClaimStatus.cs`
  - `Models/PublicUserRecoveryCode.cs`
  - `Helpers/NotFoundException.cs`
  - `Services/Infrastructure/Email/{IEmailSender, EmailMessage, LoggingEmailSender}.cs`
  - `Services/Infrastructure/Storage/{IFileStorage, StoredFileResult, LocalDiskFileStorage}.cs`
  - `Services/Portal/Auth/{IPublicUserPropertyAccessor, PublicUserPropertyAccessor, PortalCookieOptions, PortalPolicies, PortalRateLimitPolicies, PortalExceptionHandler}.cs`
- Files modified:
  - `Models/PublicUser.cs` (+7 columns), `Models/PublicUserProperty.cs` (status enum + 4 claim columns), `Models/LetterIssuance.cs` (+RecipientPublicUserId)
  - `DatabaseContexts/ApplicationDBContext.cs` (DbSet, indexes, check constraints, value converters, FKs, cascade exemption set with NonRestrictForeignKeys static)
  - `Services/Auth/DwsClaimsTransformation.cs` (early-return for non-Identity scheme)
  - `Program.cs` (portal cookie scheme, AddRateLimiter, AddProblemDetails, IExceptionHandler, infra DI, POPIA fail-fast guard, area route)
  - `Tests/dwa_ver_val.Tests.csproj` (NetArchTest.Rules package)
  - `Tests/Services/Auth/DwsClaimsTransformationTests.cs` (existing tests updated to use IdentityConstants.ApplicationScheme + 2 new load-bearing tests added)
  - `Tests/Models/EntityRelationshipTests.cs` (call-site fix for new PublicUserProperty enum status)
  - `Tests/DatabaseContexts/ApplicationDBContextTests.cs` (allow-list now derived from ApplicationDBContext.NonRestrictForeignKeys)
- Test files added: 11 new test classes spanning architecture, models, helpers, services/portal/auth, services/infrastructure, database-contexts, and integration.
- Migration: single migration `20260504094618_ExternalPortalShellPortalAuthAndClaims.cs` applied to dev DB cleanly. All 6 filtered indexes, 2 check constraints, FK cascade behaviours, and `defaultValue: 0` on `FailedLoginAttempts` verified.
- Rollout plan row 9.1 updated to In Progress with status date 2026-05-04 and explanatory notes.

## Stage 2 entry conditions captured (from this stage's reviews)
1. **`PortalExceptionHandler` (already built) MUST stay silent on response body.** It currently sets `StatusCode` only and logs. Stage 2 author MUST NOT add `Response.WriteAsync` to this handler — `NotFoundException` messages contain GUIDs that must not bleed to HTTP responses.
2. **NetArchTest fence expansion needed.** Currently `Areas/ExternalPortal/*` is blocked from `UserManager`/`SignInManager`. Stage 2 must add an additional fence prohibiting `Areas/ExternalPortal/*` from `ApplicationDBContext` directly — they must go through `Services/Portal/*` services. Note: `PublicUserPropertyAccessor` itself uses `ApplicationDBContext` (intentional); the fence applies to controllers/views.
3. **Pattern A for child-record access.** Every Stage 2 controller that receives a child-record ID (LetterIssuanceId, NotificationId, DocumentId, CommentId, etc.) MUST call `IPublicUserPropertyAccessor.AssertHasAccessToFileMasterAsync` with the resolved `FileMasterId` BEFORE returning data. Avoid Pattern B (typed assert methods per child entity) which scales poorly.
4. **`PublicUserBuilder` extensions.** Stage 2 will need `Pending()` (post-registration, pre-email-confirm) and `Suspended()` factory methods on the existing `PublicUserBuilder` test helper.
5. **`PortalPolicies` placeholder requirements.** Currently all 3 portal policies just `RequireAuthenticatedUser()`. Stage 2 must replace with real claim requirements: `PortalAuthenticated` needs `Status=Active` + `EmailConfirmed=true` + `MfaEnrolled=true`; `PortalRegistrationComplete` needs `EmailConfirmed=true` only; `PortalMfaPending` needs `MfaPending=true` claim.
6. **`PublicUserProperty.RequestedDate`** defaults to `DateTime.MinValue` if uninitialised (DB has `GETUTCDATE()` default but EF ignores DB defaults for non-nullable structs). Set `RequestedDate = DateTime.UtcNow` in the registration service when creating claim rows.

## Retro
What converged: subagent-driven workflow handled the 18 tasks reliably. Implementer agents consistently reported truthful status (5 of 18 returned `DONE_WITH_CONCERNS`, all of which were legitimate plan-authoring misses or required call-site fixes — none were unjustified). The two-stage review pattern (spec compliance + code quality) caught real issues on Tasks 2 (IDMatch → IdMatch rename), 7 (DRY violation between exemption set + test allow-list), 10 (missing cross-user isolation test), and 13 (test was not load-bearing without strengthening). Each fix-up was a small commit that left the codebase cleaner than the original task.

What drifted: the plan claimed `using Microsoft.AspNetCore.Identity;` was already present in DwsClaimsTransformation.cs (Task 13) — wasn't. The plan said `relationship.GetConstraintName()` would work for the cascade exemption keys (Task 7) — doesn't, EF builds names lazily. The plan said no other code constructed `PublicUserProperty` with a string Status (Task 4) — `EntityRelationshipTests.cs` did. None of these blocked progress; implementers caught and fixed them inline and reported as DONE_WITH_CONCERNS.

Failed prompt patterns: none significant. The "Your task description verbatim from plan" + "Important context" + "Self-review checklist" + "Report format" structure produced consistent, complete reports from agents.

Cross-boundary verdict: not strictly applicable — Stage 1 is single-codebase. Stage 2-5 will need this pattern when they cross to UI / view layer.

Stage 1 done. Ready for user review and merge to demo/azure-deploy (or open a PR).

### 2026-05-04 — Stage 1 final-review fix-up: UseForwardedHeaders for proxy IP attribution
- The whole-stage code review (commit pending) flagged that `PortalRateLimitPolicies.PartitionByIp` reads `Connection.RemoteIpAddress` directly. Behind Azure App Service / front door this collapses every external user onto a single proxy IP, so a single attacker would trip a global rate-limit lockout.
- Fix: added `app.UseForwardedHeaders(...)` between `UseHsts()` and `UseHttpsRedirection()` in Program.cs, honouring `X-Forwarded-For` + `X-Forwarded-Proto`. Required because `demo/azure-deploy` deploys behind Azure's front door.
- Build green; 137/0/137 tests still passing.
- Note: the reviewer also flagged a missing `Cookie.Path` assignment on the portal cookie, but that was a false positive — `PortalCookieOptions.cs:15` already sets `options.Cookie.Path = CookiePath`. The reviewer misread the file.
- Other Stage 2 entry conditions (PortalPolicies placeholders, NetArchTest fence expansion, etc.) remain as documented in the closing entry above.

### 2026-05-04 — Stage 2a Task 1 agent (Opus 4.7) — PublicUserBuilder Pending/Suspended factories
- Branch confirmed: `feat/external-portal-stage-2a` in worktree `.worktrees/external-portal-stage-2a`. Read journal to date and Stage 2a plan Task 1. Addresses Stage 1 closing entry condition #5 ("Stage 2 will need `Pending()` and `Suspended()` factory methods on the existing `PublicUserBuilder`").
- Pre-state: `Tests/Helpers/PublicUserBuilder.cs` had a single `Active(string email = "test@example.com")` factory (18 lines, no XML doc, no namespace import drama — `PublicUser` lives top-level so unqualified reference works).
- Step 2: replaced the entire file content verbatim from plan — added XML doc summary + two new factories (`Pending` returns `Status="Pending"`, `EmailConfirmed=false`; `Suspended` returns `Status="Suspended"`, `EmailConfirmed=true`). Existing `Active` factory preserved unchanged with same default-email signature.
- Step 3 verification: `dotnet build` returns 0 errors / 1 warning (pre-existing xUnit2031 in `DwsClaimsTransformationTests.cs` unrelated to this change). All existing call sites of `PublicUserBuilder.Active(...)` still compile (PublicUserPropertyAccessorTests + others).
- Step 4 verification: `dotnet test Tests/dwa_ver_val.Tests.csproj --nologo --verbosity quiet` returns Failed: 0, Passed: 137, Total: 137 (3 s) — exactly matches the pre-task baseline (no test count delta; this task adds factories only, callers come in subsequent Stage 2a tasks).
- Implementation commit: `29ac4e9` — `PublicUserBuilder: add Pending and Suspended factories for Stage 2` (1 file changed, 32 insertions). Journal commit follows separately.
- Status: DONE.

### 2026-05-04 — Stage 2a Task 2 agent (Opus 4.7) — NetArchTest fence: block ApplicationDBContext from portal area
- Branch confirmed: `feat/external-portal-stage-2a` in worktree `.worktrees/external-portal-stage-2a`. Addresses Stage 1 closing entry condition #2 ("Stage 2 must add an additional fence prohibiting `Areas/ExternalPortal/*` from `ApplicationDBContext` directly").
- Pre-state: `Tests/Architecture/PortalBoundaryTests.cs` had 2 `[Fact]` methods — `PortalServices_MustNotReferenceIdentityUserManager` (Services/Portal/* fence) and `ExternalPortalArea_MustNotReferenceIdentityUserManager` (Areas/ExternalPortal/* fence against UserManager/SignInManager).
- Step 2: appended a third `[Fact]` `ExternalPortalArea_MustNotReferenceApplicationDBContext` inside the existing class. Uses `Types.InAssembly(AppAssembly).That().ResideInNamespace("dwa_ver_val.Areas.ExternalPortal").ShouldNot().HaveDependencyOn("ApplicationDBContext")`. `ApplicationDBContext` lives in the global namespace, so passing the bare type name to `HaveDependencyOn` is correct (NetArchTest matches by full type name and the global-namespace type's full name is `ApplicationDBContext`).
- Step 3 verification: `dotnet test --filter FullyQualifiedName~PortalBoundaryTests` returns Failed: 0, Passed: 3, Total: 3 (69 ms) — exactly the expected +1 over the pre-task 2 tests. Test passes trivially today since no `Areas/ExternalPortal` types exist yet; the fence will start enforcing as Stage 2a controllers land.
- Step 4 verification: full suite `dotnet test Tests/dwa_ver_val.Tests.csproj` returns Failed: 0, Passed: 138, Total: 138 (3 s) — exactly +1 over the 137 baseline carried forward from Task 1.
- Implementation commit: `a289534` — `NetArchTest: block direct ApplicationDBContext from portal area` (1 file changed, 19 insertions). Journal commit follows separately.
- Status: DONE.

### 2026-05-04 — Stage 2a Task 3 agent (Opus 4.7) — PortalPolicies enforces EmailConfirmed claim
- Branch confirmed: `feat/external-portal-stage-2a` in worktree `.worktrees/external-portal-stage-2a`. Addresses Stage 1 closing entry condition #4 ("Stage 2 must replace placeholder `RequireAuthenticatedUser()` with real claim requirements"). Stage 2a slice only enforces `EmailConfirmed=true`; `MfaEnrolled=true` and `Status=Active` are deferred to Stage 2b.
- Pre-state: `Services/Portal/Auth/PortalPolicies.cs` (28 lines) had three policies (`PortalAuthenticated`, `PortalRegistrationComplete`, `PortalMfaPending`), all collapsed to `RequireAuthenticatedUser()` only — no claim names exposed as constants and no claim guards.
- Step 2: replaced the entire file content verbatim from plan. New file (43 lines) adds three claim-name constants (`EmailConfirmedClaim="EmailConfirmed"`, `MfaEnrolledClaim="MfaEnrolled"`, `StatusClaim="Status"`) and tightens all three policies. `PortalAuthenticated` and `PortalRegistrationComplete` now require `EmailConfirmed=true`; `PortalMfaPending` requires `MfaPending=true`. Both keep `AddAuthenticationSchemes(PortalCookieOptions.SchemeName)` so they only ever resolve against the portal cookie.
- Step 3 verification: `dotnet build` returns 0 errors / 6 warnings (all pre-existing — Models/Entitlement.cs, Models/Irrigation.cs, Controllers/ValidationController.cs, Controllers/AccountController.cs, Tests/Services/Auth/DwsClaimsTransformationTests.cs — none introduced by this change). Build elapsed 3.64 s.
- Step 4 verification: `dotnet test --nologo` returns Failed: 0, Passed: 138, Total: 138 (3 s) — exactly matches the 138 baseline from Task 2 (no test count delta; this task tightens existing policies, claim-stamping by `PublicUserSignInService` and policy-guarded controllers come in subsequent Stage 2a tasks).
- Implementation commit: `e08a0bb` — `PortalPolicies: enforce EmailConfirmed claim (Stage 2a)` (1 file changed, 19 insertions, 6 deletions). Journal commit follows separately.
- Status: DONE.

### 2026-05-04 — Stage 2a Task 4 agent (Opus 4.7) — PortalCookieEvents stub
- Branch confirmed: `feat/external-portal-stage-2a` in worktree `.worktrees/external-portal-stage-2a`. Implements Stage 2a Task 4 of `docs/superpowers/plans/2026-05-04-stage-2a-portal-registration-and-login.md`. Spec § 2.3 calls for status revalidation on every cookie sliding refresh; Stage 2a creates the events class as a no-op stub so Program.cs can wire `options.EventsType = typeof(PortalCookieEvents)` once and Stage 2b adds the `OnValidatePrincipal` override without touching the wiring.
- Pre-state: `Services/Portal/Auth/` contained `IPublicUserPropertyAccessor.cs`, `PortalCookieOptions.cs`, `PortalExceptionHandler.cs`, `PortalPolicies.cs`, `PortalRateLimitPolicies.cs`, `PublicUserPropertyAccessor.cs` — no `PortalCookieEvents.cs`.
- Step 1: created `Services/Portal/Auth/PortalCookieEvents.cs` (16 lines) verbatim from plan — class inherits `Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationEvents` with empty body and Stage 2a/2b explanatory XML doc + inline comment. No overrides; defers entirely to base behaviour.
- Step 2 verification: `dotnet build` returns 0 errors / 6 warnings (all pre-existing — Models/Entitlement.cs, Models/Irrigation.cs, Controllers/ValidationController.cs ×2, Controllers/AccountController.cs, Tests/Services/Auth/DwsClaimsTransformationTests.cs). Build elapsed 3.58 s.
- Step 3 verification: `dotnet test Tests/dwa_ver_val.Tests.csproj --nologo --verbosity quiet` returns Failed: 0, Passed: 138, Total: 138 (3 s) — exactly matches the 138 baseline from Task 3 (no test count delta; this task adds an empty events class, wiring into Program.cs and the Stage 2b `OnValidatePrincipal` override come later).
- Implementation commit: `6c82331` — `PortalCookieEvents: stub for Stage 2a (Stage 2b adds status revalidation)` (1 file changed, 16 insertions). Journal commit follows separately.
- Status: DONE.

### 2026-05-04 — Stage 2a Task 5 agent (Opus 4.7) — PortalAuthorizationConvention auto-applies [Authorize] to area
- Branch confirmed: `feat/external-portal-stage-2a` in worktree `.worktrees/external-portal-stage-2a`. Implements Stage 2a Task 5 of `docs/superpowers/plans/2026-05-04-stage-2a-portal-registration-and-login.md`. The convention auto-applies `[Authorize(scheme=PublicPortalScheme, policy=PortalAuthenticated)]` to every controller in the `ExternalPortal` MVC area so future portal controllers cannot accidentally ship unauthenticated; per-action `[AllowAnonymous]` still wins.
- Pre-state: `Services/Portal/Auth/` had `IPublicUserPropertyAccessor.cs`, `PortalCookieEvents.cs`, `PortalCookieOptions.cs`, `PortalExceptionHandler.cs`, `PortalPolicies.cs`, `PortalRateLimitPolicies.cs`, `PublicUserPropertyAccessor.cs` — no `PortalAuthorizationConvention.cs`. `Tests/Services/Portal/Auth/` had `PortalExceptionHandlerTests.cs`, `PublicUserPropertyAccessorTests.cs` only.
- Step 1: created `Tests/Services/Portal/Auth/PortalAuthorizationConventionTests.cs` (54 lines) verbatim from plan — two `[Fact]`s using a private `[Area("ExternalPortal")]` `FakePortalController` and a private `FakeNonPortalController`, asserting `model.Filters.OfType<AuthorizeFilter>()` is `Single` for the area-scoped controller and `Empty` for the non-area controller. Plan code was missing two `using` directives; added `Microsoft.AspNetCore.Authorization.Infrastructure` (for `DenyAnonymousAuthorizationRequirement`) and `Microsoft.AspNetCore.Mvc.Authorization` (for `AuthorizeFilter`) — these are required for compilation but were omitted from the plan snippet.
- Step 2 verification: `dotnet test --filter FullyQualifiedName~PortalAuthorizationConventionTests` failed with 5 build errors (`PortalAuthorizationConvention`, `AuthorizeFilter`, `DenyAnonymousAuthorizationRequirement` not found) — confirms the failing-test contract.
- Step 3: created `Services/Portal/Auth/PortalAuthorizationConvention.cs` (35 lines) verbatim from plan — implements `IControllerModelConvention`, returns early when `controller.Attributes.OfType<AreaAttribute>().FirstOrDefault()?.RouteValue != "ExternalPortal"`, otherwise builds an `AuthorizationPolicy` keyed to `PortalCookieOptions.SchemeName` requiring authenticated user + `EmailConfirmed=true` and adds it as an `AuthorizeFilter` to the controller's filter list.
- Step 4 verification: `dotnet test Tests/dwa_ver_val.Tests.csproj --filter FullyQualifiedName~PortalAuthorizationConventionTests` returns Failed: 0, Passed: 2, Total: 2 (20 ms) — both tests pass on first run after the convention class lands.
- Step 5 verification: full suite `dotnet test Tests/dwa_ver_val.Tests.csproj` returns Failed: 0, Passed: 140, Total: 140 (4 s) — exactly +2 over the 138 baseline from Tasks 3 and 4 (no test deletions, no regressions).
- Implementation commit: `756388b` — `Add PortalAuthorizationConvention to auto-apply [Authorize] to area` (3 files changed, 98 insertions, 6 deletions; the 6 deletions are the four Task 5 step checkbox flips in the plan file). Journal commit follows separately.
- Status: DONE.
