# Task: Full Playwright E2E — state machine coverage + complete V&V process

**Start:** 2026-06-11T11:30+02:00
**Branch:** test/e2e-full-vnv-process
**Worktree:** /Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai (main repo, no worktree)
**Plan:** n/a — direct user request: "full blown testing session by Playwright. Do a full on testing of the state machine and do a full workflow process eg do a V&V process"
**Acceptance criteria:**
- A Playwright E2E test (or test sequence) drives a COMPLETE S35-track V&V process through the real UI: create case → CP evidence at every control point (documents, mapbooks, field & crop + SAPWAT, entitlement, dam/SFRA or N/A) → pre-public review → stakeholder workshop → letter phase (Letter 1 issue → service confirm → response → ELU confirmed → PAJA checklist → Letter 3) → close. Case ends in a terminal state.
- State-machine negative coverage: every transition guard demonstrably blocks when its evidence is missing (asserted via the blocking-reasons UI or rejected POST), and role restrictions hold (Capturer cannot advance/issue; ReadOnly cannot mutate).
- S33(2) track coverage: case skips CP5–CP9 etc. to S33_2_ReadyForDeclaration after CP4; Advance is refused from that state; S33(2) letter issuance is blocked until RatesPaidConfirmed + EntitlementId are set; issuance transitions to S33_2_DeclarationIssued.
- Entire E2E suite (existing 16 + new) passes deterministically; unit suite (516) still green.
- Any genuine application bugs found get minimal fixes, each documented in this journal.

## Journal

### 2026-06-11 — dotnet-architect (e2e-implementer, two runs; both ended in API drops) + controller completion

- Read: Tests.E2E harness, Services/Workflow (WorkflowService, FlagGuards, DocumentEvidenceGuard), FileMasterController (IssueLetter, RecordCpEvidence, AdvanceWorkflow), _WorkflowPanel/_LettersPanel views, SeedDataService/IdentitySeeder, DwsPolicies.
- Changed (implementer):
  - Tests.E2E/FullVnVProcessS35Tests.cs — single full-journey test: Validator creates S35 case, CP1→CP4 evidence flags + document uploads (TitleDeedReport, SGDiagram, WARMSReport), CP5 mapbooks, CP6 field&crop+SAPWAT, CP7 entitlement, CP8/CP9 N/A toggles, CP11 file compilation, RegionalManager pre-public review + stakeholder workshop, Letter 1 (InPerson) → service confirm → responded → ELU confirmed → PAJA checklist → Letter 3 → Closed terminal; asserts blocking banner before evidence and workflow history order.
  - Tests.E2E/StateMachineGuardTests.cs — 9 negatives: CP2/CP5/CP6/CP7/CP8/CP9 guard denial messages in blocking banner; Capturer cannot advance (no button + forbidden POST); ReadOnly cannot reach Create; Letter 1 refused before letter phase (antiforgery-valid crafted POST, state unchanged).
  - Tests.E2E/S33DeclarationTrackTests.cs — S33(2) track: CP4 → S33_2_ReadyForDeclaration (CP5–CP9/CP11/review/workshop skipped, history asserted), direct Advance refused with documented engine message, issuance blocked until RatesPaidConfirmed then EntitlementId, successful issuance → S33_2_DeclarationIssued → Closed.
  - Tests.E2E/Infrastructure/VnVTestData.cs — DbContext-backed helpers (scoped property/org anchors, current-state lookup, ForceStateAsync fast-forward for negatives, rates-paid + entitlement seeding).
  - **App bug fix**: ViewModels/FileMasterDetailsViewModel.cs + Services/WorkflowService.cs — CP9_SFRACalculated was treated as "letter-ready", which hid the Advance button at CP9 and made CP11/CP_PrePublicReview/CP_StakeholderWorkshop unreachable through the UI (cases stranded at CP9). CP9 removed from IsReadyForLetters and IsLetterPhaseState; CanIssueLetterAsync still tolerates CP9 as a letter prerequisite for legacy cases.
- Changed (controller, after the implementer's second API drop pre-build):
  - Fixed 3 compile errors: Playwright Form payloads must be IFormData via Context.APIRequest.CreateFormData(), not Dictionary<string,object>.
  - Capturer_CannotAdvanceWorkflow: forbidden POST is answered 302→AccessDenied and Playwright follows it — assert landing URL instead of raw 403.
  - S33 test: TempData errors are consumed by the post-redirect render — assert the refusal text on the page IssueLetterAsync lands on (and the API-driven advance's own response body), not on a later navigation; switch to Regional("3") for the letter phase (CanIssueLetter requires RegionalManager+; the Validator sees no issue forms).
- Verification (fresh, full): `dotnet test Tests.E2E/` → Passed 27/27 (2m02s). `dotnet test Tests/` → Passed 516/516.
- Status: DONE

## Retro (on completion)

Converged: the explorer's guard/UI map was accurate enough that the implementer needed no re-exploration of the state machine, and the journey test passed on first full run — only the negatives needed iteration. Drifted: the implementer agent suffered two API drops (156k and 224k tokens) and never reached the build step; the controller finished compile fixes and stabilisation. The recurring test-authoring trap was Playwright's redirect-following interacting with ASP.NET TempData and cookie-auth (302s to AccessDenied): three separate failures came from asserting status codes or re-navigating after a redirect had already consumed the TempData. Failed prompt pattern: none specific — but very large single-agent briefs invite mid-run drops; next time split implement vs stabilise into two dispatches. Application bug found and fixed: CP9 letter-readiness stranded cases (see entry above).

### 2026-06-11T14:10+02:00 — dotnet-master review (READ-ONLY)

Reviewed PR #10 diff (commit 0898dab/merge 835a837): the 3 E2E test classes, VnVTestData, and the
CP9 letter-readiness app fix. `dotnet build Tests.E2E/` succeeded (0 errors, NU1510 warnings only).
Did NOT run the suite (controller running it concurrently on the shared E2E DB).

- App fix (WorkflowService.IsLetterPhaseState + FileMasterDetailsViewModel.IsReadyForLetters):
  removing CP9_SFRACalculated is SOUND for both S35 and S33(3). ResolveNextStateAsync only
  special-cases S33(2); S33(3) runs the full pipeline and advances CP9→CP11→PrePublicReview→
  StakeholderWorkshop by DisplayOrder, issuing its declaration from CP_StakeholderWorkshop
  (AvailableLetterActions:50-54). CanIssueLetterAsync still lists CP9 as an S35_L1/S33_3 prereq
  (WorkflowService.cs:202,210-211) for legacy cases — but _LettersPanel is gated on IsReadyForLetters
  (Details.cshtml:33), so at CP9 the panel no longer renders. Verdict: no live path needs to issue
  from CP9; the CP9 prereq is now dead for new cases (issuance UI unreachable there) but harmless. NIT.
- StateMachineGuardTests.cs:193-198 (Capturer advance POST, no AF token): AdvanceWorkflow carries
  [ValidateAntiForgeryToken] AND [Authorize]. The tokenless POST most likely fails AF (400) before the
  RBAC filter runs, so the assertion passes on the wrong reason — it proves "no token" not "Capturer
  denied". The button-absence assertion (line 188) is the real RBAC proof; the POST is weak. SHOULD-FIX
  (add token like the Letter1 negative does, to actually exercise the CanTransitionWorkflow policy).
- Playwright NetworkIdle: PageGotoOptions.Idle + WaitForLoadStateAsync(NetworkIdle) used pervasively.
  Discouraged in Playwright docs and a classic flake source under load; mitigated here because the app is
  server-rendered MVC with little background XHR and the collection serializes. Acceptable but a latent
  flake vector if the UI gains polling/websockets. NIT.
- Flakiness/shared DB: all E2E classes share [Collection(E2ECollection.Name)] and there is NO
  xunit.runner.json / DisableTestParallelization — xUnit serializes within a collection, so the shared
  dwa_val_ver_e2e DB and Kestrel port are safe. This invariant is IMPLICIT and load-bearing; if anyone
  splits these into multiple collections the suite will race. SHOULD-FIX: add an explicit
  [assembly: CollectionBehavior(DisableTestParallelization = true)] or a comment pinning the guarantee.
- Unique keys: reg/SG suffixes use DateTime.UtcNow:HHmmssfff. Safe only because of serialization (two
  classes seeding in the same ms would collide on Property.PropertyReferenceNumber/RegistrationNumber).
  Tie to the serialization note above. NIT.
- ForceStateAsync repoints CurrentWorkflowStateId WITHOUT writing a WorkflowStepRecord. Fine for the
  guard negatives (which assert state, not history), but any future history assertion after a forced
  state would see a gap. Documented in the helper. NIT (no action).
- DisposePageAsync closes only the page's context, not the page; contexts are torn down in finally on
  all paths incl. the S35 mid-journey user-switch (line 123). Browser/Playwright disposed in fixture.
  Async/await is clean — no .Result/.Wait, no fire-and-forget, all SaveChangesAsync awaited, NewDb()
  contexts in `await using`. OK.
- Magic strings (state names, letterAction values, DocumentType values) are duplicated as literals in
  tests rather than referencing the seed/enum constants. Pragmatic for E2E black-box assertions but they
  will silently rot if a state is renamed. NIT.
- Comment accuracy: FullVnVProcessS35Tests.cs:112 ("CP9 is a letter-ready state, so advance-blocking
  reasons are suppressed there") now CONTRADICTS the very fix in this PR — post-fix CP9 is NOT letter-ready
  and DOES show blocking reasons. Misleading comment. SHOULD-FIX (the test still passes because it seeds
  Authorisation then advances without asserting the banner, but the comment is wrong).

Verdict: tests are well-structured, correctly serialized, and the app fix is correct; no BLOCKERs.
Recommend tightening the Capturer AF-token gap, pinning the no-parallelization invariant explicitly,
and fixing the stale CP9 "letter-ready" comment.

### 2026-06-11T15:20+02:00 — dotnet-architect review

Read-only architectural review of PR #10 (merge 835a837; source diff d21edf8..0898dab). `dotnet build` clean (0 errors). E2E suite NOT run (controller running it concurrently).

**Workflow change (point 3) — SOUND, but exposes a latent gap**
- Verified canonical advance path: `ResolveNextStateAsync` orders purely by `DisplayOrder` (WorkflowService.cs:123-126). Seed order (SeedDataService.cs:692-701): CP9=15 → CP11=16 → CP_PrePublicReview=17 → CP_StakeholderWorkshop=18. So CP9→CP11→CP_PrePublicReview→CP_StakeholderWorkshop is exactly what the engine resolves. Removing CP9 from `IsReadyForLetters`/`IsLetterPhaseState` correctly un-strands cases at CP9. **The fix is correct.**
- Panel/prereq alignment: at CP_StakeholderWorkshop `IsReadyForLetters` is true → Advance button hidden, Letters panel shown; `CanIssueLetterAsync` allows S35_L1/S33_3 from CP_StakeholderWorkshop (WorkflowService.cs:202,210-211). No state renders the panel with every action refused, nor vice-versa. Consistent.
- BLOCKER (latent, surfaced by this change, not introduced by it) — WorkflowService.cs:151-154 + 121-139, FileMasterController.cs:223-243: `AdvanceAsync` from CP_StakeholderWorkshop resolves DisplayOrder 19 = `S35_Letter1Issued` with NO guard blocking entry into a letter `*Issued` state. The only protection is the UI hiding the button (`IsReadyForLetters`). A crafted antiforgery-valid POST to `/FileMaster/AdvanceWorkflow` (the test files demonstrate exactly this technique) walks the case into S35_Letter1Issued with NO `LetterIssuance` row — then `LetterServiceConfirmedGuard` (FlagGuards.cs:271-300) permanently blocks exit (no issuance to confirm service on) and the letters panel offers MarkLetter1Responded/IssueLetter1A against a non-existent letter. Same hole at every CP→letter and letter→letter boundary that lacks a leaving-guard. Fix: an `ITransitionGuard` denying AdvanceAsync entry into any `S35_*`/`S33_*` state (force those transitions through IssueLetter/MarkLetterResponse only), mirroring the existing S33_2_ReadyForDeclaration block at WorkflowService.cs:77-80.

**Test architecture (point 1)**
- SHOULD-FIX — FullVnVProcessS35Tests.cs:38-195: single ~160-line mega-test, ~20 sequential UI round-trips. One failure at step N aborts all downstream coverage and the failure message points at a low-level locator, not the business transition. Poor diagnosability. Recommend splitting into staged `[Fact]`s sharing a seeded fixture per phase (Validation / Verification / Letters), or an ordered class. Runtime is acceptable; diagnosability is the cost.
- NIT — VnVTestData.cs:23-25,81-88: `NewDb()` news a fresh DbContext per helper call (no pooling) and `ForceStateAsync` repoints `WorkflowInstance.CurrentWorkflowStateId` directly. The force is HONEST for the guard negatives (it deliberately skips evidence so the missing-evidence denial is what's asserted) and does NOT bypass the guard under test — guards re-run on the real Advance POST. Safe. The one soft spot: ForceStateAsync writes no WorkflowStepRecord, so history-order assertions must not be layered on force-fast-forwarded cases (current tests correctly don't).
- NIT — shared `[Collection(E2ECollection.Name)]` + per-test unique reg/SG suffix keyed on `HHmmssfff`: sustainable now (each test owns its case, serialized collection avoids the dwa_val_ver_e2e/Kestrel collision). Risk as suite grows: millisecond-suffix collisions under fast parallel authoring and ever-growing shared DB. Acceptable for current size.

**Coverage gaps (point 2), ranked by risk**
- GAP-1 (BLOCKER-adjacent): no test asserts AdvanceAsync is REFUSED from CP_StakeholderWorkshop (and from each letter state) — the exact hole above. Highest risk; add alongside the guard fix.
- GAP-2 (high): unlawful-use escalation entirely untested — MarkUnlawfulUseFound → S35_UnlawfulUseFound → Letter4A → Letter4&5 → Closed (ResponseActionMap FileMasterController.cs:493; LetterActionMap :458-459).
- GAP-3 (high): Letter 1A non-response path untested — S35_Letter1Issued → IssueLetter1A → Letter1AIssued → service-confirm → Letter1AResponded (LetterServiceConfirmedGuard covers L1A at FlagGuards.cs:279 but is never exercised).
- GAP-4 (high): S33(3)a/S33(3)b individual-application track untested — IssueS33_3a/3b → S33_3_DeclarationIssued (LetterActionMap :461-462); only S33(2) is covered.
- GAP-5 (medium): Letter 2 / 2A additional-info loop untested — IssueLetter2 → service-confirm → Letter2Responded → (IssueLetter2A) → IssueLetter3.
- GAP-6 (medium): reissuance / OneTimeLetterCodes idempotency (FileMasterController.cs:469-479,524-553) and ResponseStatus="Pending" repeatable-letter gating untested.
- GAP-7 (medium): WF-01/WF-02 optimistic-concurrency (DbUpdateConcurrencyException → WorkflowConcurrencyException, WorkflowService.cs:295-303) has no UI-level concurrency E2E.
- GAP-8 (low): CP1 sub-steps 1.1–1.7 are discrete seeded states (SeedDataService.cs:676-682) but advanced through ungated in a blind loop; no per-sub-step evidence guard exists or is asserted (may be intended — CLAUDE.md lists guards/evidence for each, so either guards are missing or the spec is aspirational; flag for product confirmation).
- Positive: PAJA-incomplete negative (Letter3 refused) IS covered (FullVnVProcessS35Tests.cs:155-161); LetterServiceConfirmedGuard negative for L1 IS covered (:144-152); CP2/5/6/7/8/9 guard negatives + Capturer/ReadOnly RBAC covered (StateMachineGuardTests.cs).

**Other**
- NIT — FileMasterController.cs:749: `MarkLetterResponse` calls `TransitionToAsync(userId: null)`, so role-checking guards see no roles on ELU-confirm/close transitions. No role guard currently targets those transitions, so harmless today, but it's a fragile default — a future role-gated letter transition would silently pass.

Verdict: Workflow CP9 fix is correct and the suite is a strong first E2E baseline, but it ships a latent BLOCKER — AdvanceAsync can enter letter states without a LetterIssuance — that the new UI gating hides rather than closes, and that no test guards against.

### 2026-06-11 — controller — second run + review remediation (branch fix/e2e-review-fixes)

- Second full E2E run on merged main: 27/27 (2m03s) — deterministic.
- Reviews: dotnet-master DONE_WITH_CONCERNS (no blockers; 3 SHOULD-FIX); dotnet-architect DONE_WITH_CONCERNS (CP9 fix confirmed SOUND incl. seed display-order CP9=15→CP11=16→Review=17→Workshop=18; 1 latent BLOCKER; 8 ranked coverage gaps). Architect agent dropped mid-report once; resumed to completion.
- BLOCKER fixed (Services/WorkflowService.cs AdvanceAsync): a crafted antiforgery-valid POST could advance CP_StakeholderWorkshop → S35_Letter1Issued with NO LetterIssuance, wedging the case (LetterServiceConfirmedGuard unsatisfiable). AdvanceAsync now refuses any resolved next state starting S35_/S33_ except the S33_2_ReadyForDeclaration holding state (the legitimate track-skip target). Letter states are reachable only via IssueLetter/MarkLetterResponse → TransitionToAsync.
- dotnet-master SHOULD-FIXes applied: (1) Capturer advance negative now sends a valid antiforgery token so the CanTransitionWorkflow policy — not the antiforgery filter — is what refuses (StateMachineGuardTests); (2) Tests.E2E/AssemblyInfo.cs pins DisableTestParallelization=true (the shared-DB/serialization invariant was implicit and load-bearing); (3) stale "CP9 is letter-ready" comment in FullVnVProcessS35Tests corrected.
- New regression tests: unit AdvanceAsync_IntoLetterState_ThrowsInvalidOperation (workshop evidence satisfied, refusal comes from the letter-entry block, state unmoved); E2E Advance_CannotEnterLetterState_WithoutIssuingLetter (GAP-1: crafted POST refused, TempData message asserted on post-redirect body).
- Open items (not in this branch): architect GAPs 2–8 — unlawful-use escalation (L4A/L4&5), Letter 1A/2/2A loops, S33(3)a/b track, reissuance idempotency, WF-01/02 concurrency E2E, CP1 sub-step guards (flagged for product confirmation: sub-steps are seeded states but ungated); NITs: NetworkIdle reliance, magic strings, MarkLetterResponse passes userId:null to TransitionToAsync.
- Verification (fresh): unit 517/517; E2E 28/28 (2m06s).
- Status: DONE
