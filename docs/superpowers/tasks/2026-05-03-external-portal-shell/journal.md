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
