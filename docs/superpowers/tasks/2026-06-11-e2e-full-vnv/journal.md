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
