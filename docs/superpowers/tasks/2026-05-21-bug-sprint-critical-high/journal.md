# Task: Critical & High Bug Sprint (UAT Report 2026-05-21)
**Start:** 2026-05-21T20:00+02:00
**Branch:** main
**Worktree:** N/A — working directly on main
**Live URL:** http://20.87.59.203 (AKS LoadBalancer)

## Bugs in scope (in fix order)

### Critical
| Bug | Description |
|-----|-------------|
| BUG-008 | Liveness probe timeout=1s too aggressive → intermittent pod restarts under load. **Deployment-agents scope only** — Helm fix, no code change. Pod currently stable (0 restarts). |
| BUG-013 | IssueLetter stores issuing user's display name as Recipient instead of the submitted Recipient field. NWA S35(2)(d) compliance risk. |
| BUG-014 | IssueLetter can run on an incomplete case → orphaned LetterIssuance with no matching workflow state. |
| BUG-018 | River lookup dropdown is empty — no River records seeded → DamCalculation forms cannot be saved → CP8 permanently blocked. |

### High
| Bug | Description |
|-----|-------------|
| BUG-012 | GetBlockingReasonsAsync (or the view rendering) re-evaluates ALL guards regardless of current CP → stale/irrelevant error banners shown (e.g. CP5 error on a CP9 case). |
| BUG-015 | S33(2)/(3) workflow state pills appear in the phase tracker on S35-track cases. |
| BUG-023 | External portal post-login routes return 404: /ExternalPortal/Mfa/SetupTotp, /ExternalPortal/Dashboard, /ExternalPortal/Cases, /ExternalPortal/Documents, /ExternalPortal/Objections. Controllers exist in codebase — root cause is routing, auth, or view absence. |

## Acceptance criteria
- [ ] Letter Recipient field in IssueLetter form is persisted to LetterIssuance correctly (not replaced by issuing user)
- [ ] IssueLetter is blocked (with clear error) when prerequisite CPs are not satisfied
- [ ] River seed data present; DamCalculation Create form shows rivers in dropdown
- [ ] GetBlockingReasonsAsync only surfaces guards relevant to the current workflow state
- [ ] Phase tracker on S35-track FileMaster shows no S33(2)/(3) state pills
- [ ] External portal MFA setup, Dashboard, Cases, Documents, Objections all load after authentication
- [ ] Liveness probe timeout raised (Helm) — pod survives under load without restart
- [ ] `dotnet test` passes (368+) after code changes
- [ ] Validator agent confirms all above on live http://20.87.59.203

## Journal

### 2026-05-21T20:00+02:00 — Controller — Task setup
- Read: UAT report from validator agent (2026-05-21 session)
- Read: Pod describe — BUG-008 pod currently 0 restarts, started 19:29:10. Previous pod completed cleanly (exit code 0). Liveness timeout=1s, period=30s, failure=5.
- Read: Codebase inventory — ExternalPortal controllers DO exist (DashboardController, CaseController, MfaController, ObjectionController, ResponseController, DocumentController). BUG-023 is routing/auth/view issue, not missing controllers.
- Decided: BUG-008 → deployment agents only (Helm liveness probe timeout 1s→5s). All other bugs → dotnet-master.
- Status: Dispatching dotnet-master (Phase 1).

### 2026-05-21T21:00+02:00 — dotnet-master — Code fixes
Baseline before changes: `dotnet test` = 368 passed / 0 failed. After all fixes + new tests: **378 passed / 0 failed**.

**BUG-018 — Rivers not seeded (CP8 blocked).**
- File: `Services/SeedDataService.cs` — added `SeedRiversAsync()` (42 SA rivers), called it from `SeedAsync()` after `SeedIrrigationSystemsAsync()`. Idempotent via case-insensitive name HashSet, mirroring the existing WaterSource/IrrigationSystem seeders.
- Root cause: `River` table simply never had a seed block; `DamCalculation/Create` requires a `RiverId`, so the dropdown was always empty.
- Tests added: `GuardTests.cs` — `SeedAsync_SeedsRivers_ForDamCalculationDropdown`, `SeedAsync_RiverSeed_IsIdempotent`.

**BUG-015 — S33(2)/(3) pills shown on S35-track cases.**
- File: `Views/FileMaster/_WorkflowPanel.cshtml` — phase tracker now filters Verification-phase states through `IsTrackRelevant(stateName)` keyed on `FileMaster.AssessmentTrack`. Pre-fork states (CP*, review, workshop, Closed) always show; S35_*/S33_2_*/S33_3_* show only for the matching track.
- Root cause: tracker rendered ALL states in each phase; all letter/declaration states live in the "Verification" phase, so every track saw every fork's pills.

**BUG-012 — Stale advance-guard banner in letter phase.**
- Files: `Services/WorkflowService.cs` (`GetBlockingReasonsAsync` + new `IsLetterPhaseState`), `Views/FileMaster/_WorkflowPanel.cshtml` (banner gated on `!IsReadyForLetters`).
- Root cause: `BlockingReasons` is recomputed on every Details GET for the *Advance* path. On a CP9 / letter-sub-state case the operative action is "issue a letter", not "advance", so the advance-guard denial (e.g. the CP9 SFRA / Mapbook-style messages) was rendered as a stale, irrelevant banner. Now `GetBlockingReasonsAsync` returns empty when the current state is `CP9_SFRACalculated`, `CP_StakeholderWorkshop`, or any `S35_`/`S33_` state, and the view only renders the banner when Advance is actually offered. Guards already self-scope via `IsLeaving`, so no already-passed-CP guard was ever firing — the issue was phase-appropriateness, not guard scoping.

**BUG-013 — Recipient field ignored; issuing user shown instead.**
- Files: `Models/LetterIssuance.cs` (new nullable `RecipientName` column), `Services/Letters/LetterService.cs` (persist `req.RecipientName`), `Views/FileMaster/_LettersPanel.cshtml` (Recipient column now shows `RecipientName ?? ServingOfficialName`), migration `20260521185228_AddRecipientNameToLetterIssuance`.
- Root cause: `LetterIssuance` had NO recipient column. The panel's "Recipient" column displayed `ServingOfficialName`, which `LetterService` sets to the *issuing/signing user's* display name for in-person service. The form `recipient` value only ever reached the PDF body, never a persisted column. Legacy rows fall back to `ServingOfficialName` in the view.
- **Migration note for deployment agents:** a new EF migration is included; `db.Database.MigrateAsync()` on startup will add the `RecipientName` column (nullable nvarchar(max)) to `LetterIssuances`. No data backfill needed.

**BUG-014 — Letter issued on incomplete case → orphaned LetterIssuance.**
- Files: `Interfaces/IWorkflowService.cs` (new `CanIssueLetterAsync` + `LetterIssuanceCheck` record), `Services/WorkflowService.cs` (impl + `LetterPrerequisiteStates` map), `Controllers/FileMasterController.cs` (call it FIRST in `IssueLetter`, before any DB write or the idempotency read).
- Root cause: `IssueLetter` created the `LetterIssuance` row (via `_letters.IssueAsync`) and only THEN attempted `TransitionToAsync`. If the transition was rejected the row was already committed. New pre-issuance check maps each letter code to its valid prerequisite current state(s) (mirrors `AvailableLetterActions`) and fails fast with no DB write.
- Tests added: `WorkflowServiceTests.cs` — allow/deny/unknown-code theories. Existing `FileMasterControllerLetterTests` updated: added `OkWorkflowMock()` helper and `CanIssueLetterAsync → Ok` setup so the 14 letter-controller tests still exercise their intended downstream behaviour.

**BUG-023 — External portal post-login routes 404 (really: auth bounce).**
- Files: `Services/Portal/Auth/PortalCookieOptions.cs` (`SecurePolicy` Always → `SameAsRequest`), `Areas/ExternalPortal/Controllers/MfaController.cs` (device-trust cookie `Secure = Request.IsHttps`).
- Root cause: the portal auth cookie had `SecurePolicy = Always`. On AKS the pod terminates TLS at the ingress and speaks plain HTTP, so the browser refused to return the Secure cookie over HTTP — every post-login / post-MFA request appeared unauthenticated and the cookie middleware bounced it to the portal login (presenting as "all post-login routes 404"). This is the SAME issue already fixed for the internal `Identity.Application` cookie (see Program.cs comment). All views and controllers already existed — no missing views. The MFA flow logic (SelectMethod → EnrolTotp/SMS → Verify → Dashboard) was correct; only cookie persistence was broken.
- **Note for validator:** verify on live by registering → confirming email → SelectMethod → EnrolTotp → Dashboard. Behind HTTPS the cookie is still marked Secure. Also note `Tests/Integration/IntegrationTestBase.cs` only relaxed the *Identity.Application* cookie to SameAsRequest for tests — the portal cookie now derives SameAsRequest from its own config, so portal integration tests over plain-HTTP test client will carry the cookie correctly.

**Verification:** `dotnet build` (main) = 0 errors. `dotnet test` = **378 passed, 0 failed, 0 skipped**.

### 2026-05-21T22:00+02:00 — devops-platform-strategist — Helm probe fix + cookie validation

**BUG-008 — Helm probe timeoutSeconds raised (liveness 1s→5s, readiness 1s→3s).**

Files changed (Helm only — no C# touched):
- `deploy/helm/dwa-vv/values.yaml` — added `timeoutSeconds: 5` under `probes.liveness`, `timeoutSeconds: 3` under `probes.readiness`.
- `deploy/helm/dwa-vv/templates/deployment.yaml` — added `timeoutSeconds: {{ .Values.probes.liveness.timeoutSeconds }}` to `livenessProbe` block and `timeoutSeconds: {{ .Values.probes.readiness.timeoutSeconds }}` to `readinessProbe` block (lines 107–121).
- `deploy/helm/dwa-vv/values.dev.yaml` — no change needed; `values.dev.yaml` does not override `probes`, so it inherits the new defaults from `values.yaml`.

Root cause: Kubernetes defaults `timeoutSeconds` to 1s when the field is absent. Under even modest load the root-path health check on an ASP.NET Core pod (which triggers middleware + DB Data Protection key lookup on first hit) can exceed 1s, causing the probe to record a failure. With `failureThreshold: 5` and `period: 30s` this means ~150s of consecutive slow responses trips a restart — consistent with the "intermittent restarts under load" symptom. Raising to 5s (liveness) / 3s (readiness) gives the app breathing room while remaining well below the `periodSeconds` to avoid probe overlap.

Next deployment step: `helm upgrade dwa-vv deploy/helm/dwa-vv -f deploy/helm/dwa-vv/values.dev.yaml --set image.tag=<sha>`. Rolling update will pick up the new probe config on the replacement pod.

---

**BUG-023 — Cookie SecurePolicy validation: SameAsRequest is correct for this AKS setup.**

Finding: SOUND. The `SameAsRequest` change is the correct policy.

Evidence chain:
1. `Program.cs` lines 261–265: `UseForwardedHeaders` is configured with `XForwardedFor | XForwardedProto`. This middleware runs BEFORE `UseAuthentication` (line 296) in the pipeline.
2. nginx-ingress terminates TLS and sets `X-Forwarded-Proto: https` on requests forwarded to the pod over plain HTTP. ASP.NET Core's `ForwardedHeaders` middleware rewrites `Request.Scheme` to `https` and sets `Request.IsHttps = true` based on that header.
3. `CookieSecurePolicy.SameAsRequest` evaluates `context.Request.IsHttps` at cookie-write time. Because `ForwardedHeaders` middleware has already run, `Request.IsHttps` is `true` for all production requests that originated over HTTPS at the ingress. The portal auth cookie will be marked `Secure` in production — correctly.
4. On the dev cluster (LoadBalancer, no ingress, no TLS), `X-Forwarded-Proto` is not present. `Request.IsHttps = false`. `SameAsRequest` produces a non-Secure cookie. This is acceptable for a dev/demo environment on an internal cluster — same behaviour as the existing `Identity.Application` cookie (see `Program.cs` line 69 comment which explicitly documents this reasoning).
5. This mirrors the internal cookie fix already in place and documented in the AKS deployment memory.

No code change required. BUG-023 fix committed by dotnet-master is confirmed correct for the current and future (HTTPS-fronted) AKS topology.
