# Client Demo — V&V Workflow + Section 35 Letter Flow

**Date:** 2026-04-17
**Target:** Working demo for DWS client, same day, 3–4 hour build window
**Audience:** DWS client meeting later today

## Goal

Show the client a **live, data-backed walk-through** of a V&V case advancing from inception to ELU confirmation, including the Section 35 letter flow. Every click writes to the real SQL Server database.

Not an MVP. Not feature-complete. A *demonstrable slice* that tells the story end to end.

## The Story the Client Sees

1. Open the V&V Cases list — three seeded sample cases are there.
2. Open a fresh case — its workflow panel shows "Phase 1: Inception · CP1: WARMS & Imagery Obtained", with the phase tracker highlighting the current control point.
3. Click **Advance to next CP** repeatedly — case progresses through CP1 → CP2 → … → CP9. Each transition is recorded with timestamp, user, and prior state.
4. At CP9, a **Letters** panel appears on the Details page.
5. Click **Issue Letter 1 (S35(1))** — form asks for recipient, delivery method, issued date. Submitting creates a `LetterIssuance` row and transitions state to `Letter1Issued`.
6. Click **Mark Letter 1 Responded** — state moves to `Letter1Responded`.
7. Issue Letter 2 (request for additional info), mark responded.
8. Issue Letter 3 (S35(4) ELU certificate) — state becomes `ELUConfirmed`, case closes.
9. The transition history list on the page shows the full audit trail.

## Scope

### In scope

- Seed workflow states (9 CPs + Section 35 letter states + terminal `ELUConfirmed` / `Closed`)
- Seed letter types (Letter 1, 1A, 2, 2A, 3, 4A, 4&5) per `LetterIssuance` model
- Seed reference data sufficient for sample cases: 1 Province, 1 WMA, 1 CatchmentArea, 1 OrganisationalUnit, 2 Properties, 3 FileMasters
- Auto-create a `WorkflowInstance` at CP1 when a `FileMaster` is created (or backfill for seeded cases)
- **Workflow panel** on `FileMaster/Details` — phase tracker, current state badge, advance button, transition history
- **Letters panel** on `FileMaster/Details`, visible when the case has reached CP9 — issue-letter form and response-marking buttons
- Every state transition writes a `WorkflowStepRecord`
- Every letter action writes/updates a `LetterIssuance`

### Out of scope (explicitly cut)

- Role-based authorisation guards (demo runs as a single implicit user)
- CP1 internal sub-steps (CP1 is treated as a single advanceable state)
- PDF letter generation (no QuestPDF work today)
- Digital signatures (no SignatureRequest / DigitalSignature flow)
- S53(1) directive letters (Letter 1A, 2A, 4A, 4&5) — buttons exist but are not required for the happy-path demo
- S33(2) and S33(3) declaration tracks — only S35 Verification track is implemented
- Public portal / external user flows
- SAPWAT, dam volume, SFRA calculations
- WARMS / eWULAAS integration
- Validation guards (e.g. "cannot advance past CP5 without Mapbooks") — demo allows free advance

## Architecture

### Where the new code lives

```
Services/
  WorkflowService.cs           -- NEW. Encapsulates state transitions, step-record writes.
  SeedDataService.cs           -- EXTEND. Add workflow states, letter types, sample cases.

Controllers/
  FileMasterController.cs      -- EXTEND. Add AdvanceWorkflow, IssueLetter, MarkLetterResponse actions.

Views/FileMaster/
  Details.cshtml               -- EXTEND. Add workflow panel and letters panel partials.
  _WorkflowPanel.cshtml        -- NEW partial.
  _LettersPanel.cshtml         -- NEW partial.
  _IssueLetterModal.cshtml     -- NEW partial (form inside a modal or inline panel).

wwwroot/css/
  site.css                     -- EXTEND. Styles for phase tracker, CP pill, timeline.
```

No new entity classes are introduced. All models needed already exist: `WorkflowState`, `WorkflowInstance`, `WorkflowStepRecord`, `LetterIssuance`, `LetterType`.

### WorkflowService API

```csharp
public interface IWorkflowService
{
    Task<WorkflowInstance> StartWorkflowAsync(Guid fileMasterId);
    Task<WorkflowInstance> AdvanceAsync(Guid fileMasterId, Guid? userId, string? notes);
    Task<IReadOnlyList<WorkflowStepRecord>> GetHistoryAsync(Guid workflowInstanceId);
    Task<WorkflowState?> GetNextStateAsync(Guid currentStateId);
}
```

`AdvanceAsync` does three things atomically:
1. Mark the current `WorkflowStepRecord` as `Completed` with completion time
2. Update `WorkflowInstance.CurrentWorkflowStateId` to the next state (by `DisplayOrder`)
3. Insert a new `WorkflowStepRecord` in `InProgress` status for the new state

If the instance is already at a terminal state, throw — caller must handle gracefully in UI.

### Letter issuance flow

The Letters panel is driven entirely by what letters already exist on the case and what the current workflow state is:

| Current State         | UI offers                              |
|-----------------------|----------------------------------------|
| `CP9_Complete`        | Issue Letter 1                         |
| `Letter1Issued`       | Mark Letter 1 Responded                |
| `Letter1Responded`    | Issue Letter 2, or Issue Letter 3      |
| `Letter2Issued`       | Mark Letter 2 Responded                |
| `Letter2Responded`    | Issue Letter 3                         |
| `Letter3Issued`       | (Terminal — ELU confirmed, case closed)|

Issuing a letter = (a) write `LetterIssuance` record, (b) advance workflow state to the corresponding `LetterXIssued` state. Marking a response = advance state only (update `LetterIssuance.ResponseDate`, `ResponseStatus = "Agreed"`).

## Data Flow

```
User clicks "Advance"
  → FileMasterController.AdvanceWorkflow(id)
  → WorkflowService.AdvanceAsync(id, ...)
  → EF Core updates WorkflowInstance + inserts WorkflowStepRecord
  → SaveChangesAsync
  → Redirect to Details/{id}
  → View renders updated phase tracker + history
```

Letters follow the same shape — controller action → service call → EF write → redirect.

## Workflow States to Seed

Order by `DisplayOrder`:

| # | StateName              | Phase        | Terminal |
|---|------------------------|--------------|----------|
| 1 | CP1_InceptionSetup     | Inception    | no       |
| 2 | CP2_SpatialInfo        | Validation   | no       |
| 3 | CP3_WARMSEvaluation    | Validation   | no       |
| 4 | CP4_AdditionalInfo     | Validation   | no       |
| 5 | CP5_GISDigitising      | Validation   | no       |
| 6 | CP6_FieldCropSAPWAT    | Validation   | no       |
| 7 | CP7_ELUCalculation     | Verification | no       |
| 8 | CP8_DamVolumes         | Verification | no       |
| 9 | CP9_SFRAForestation    | Verification | no       |
| 10 | CP9_Complete          | Verification | no       |
| 11 | Letter1Issued         | Verification | no       |
| 12 | Letter1Responded      | Verification | no       |
| 13 | Letter2Issued         | Verification | no       |
| 14 | Letter2Responded      | Verification | no       |
| 15 | Letter3Issued         | Verification | no       |
| 16 | ELUConfirmed          | Verification | yes      |

## Letter Types to Seed

Minimum set for the happy-path demo:

| LetterName | NWASection | Description                              |
|------------|------------|------------------------------------------|
| Letter 1   | S35(1)     | Notice to apply for verification         |
| Letter 2   | S35(3)(a)  | Request for additional information       |
| Letter 3   | S35(4)     | Confirmation of extent and lawfulness    |

## Sample Data

- 1 Province: "Mpumalanga"
- 1 WMA: "Inkomati-Usuthu"
- 1 CatchmentArea: "X21A"
- 1 OrganisationalUnit: "Mpumalanga Regional Office"
- 2 Properties:
  - `Farm "Doornhoek 123"` SG code `T0HT00000000012300000`
  - `Farm "Leeuwfontein 456"` SG code `T0HT00000000045600000`
- 3 FileMasters, one against each property plus one extra — one fresh at CP1, one mid-workflow (CP5), one already at CP9_Complete ready to demo the letter flow

## UI Notes

The phase tracker is a horizontal band on the Details page styled with existing CSS variables (DWS brand palette, no Tailwind defaults per the feedback memory). Three phase segments, each containing its CPs as pills. The current CP is filled with `--dws-blue`, completed CPs use a muted fill, upcoming CPs are outlined.

The transition history is a simple vertical list below the tracker showing `StateName · CompletedDate · Notes`.

The Letters panel uses the same table styling as the existing FileMaster Index for consistency.

## Error Handling

- If a user tries to advance beyond a terminal state, show an inline message on the Details page ("Case is closed — ELU confirmed on <date>"). No hard error.
- If seed data already exists on startup, the seeder is a no-op for that entity type (existing pattern in `SeedDataService`).
- Form validation on the issue-letter form uses standard MVC `ModelState`.

## Testing

Tight window — no new test writing today. Smoke test by clicking through the demo happy path in the browser before the client meeting. Existing 33 unit tests must still pass (`dotnet test`).

## Risks

- **Migration needed?** Let me verify during implementation — if `WorkflowState` and `WorkflowInstance` DbSets already have migrations applied, no schema change needed. If not, a migration must be added and applied (~5 min).
- **Seed idempotency** — existing `SeedDataService` may not handle re-runs gracefully. Confirm by running twice during local testing.
- **Time** — if we slip past 3.5 hours, drop Letter 2 and go straight from Letter 1 → Letter 3. Story still works.

## Success Criteria

Click-through demo runs end to end on the local dev server with no errors. Client sees:
- A case advance through all 9 CPs via real state transitions
- A Section 35 letter issued and responded to
- An ELU-confirmed terminal state
- The full transition history persisted

Next session: S33(2) + S33(3) declaration tracks, Letter 1A/2A directive flows, role guards, PDF generation.
