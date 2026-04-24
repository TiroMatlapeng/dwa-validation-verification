# Workflow Guards + Audit Implementation Plan (Plan 3 of 4)

**Goal:** Harden the workflow with transition guards, branch S33(2) around CP5–CP9, and add an immutable audit trail visible on each case. Inline execution; no subagent dispatch.

**Architecture:**
- New `IAuditService` / `AuditService` writes `AuditLog` rows (already-existing entity reused; no schema change to `AuditLog`).
- `ITransitionGuard` + a registered collection evaluated by `WorkflowService.AdvanceAsync`/`TransitionToAsync` before any state change; first failure blocks and surfaces its `Reason`.
- Track-skip logic for S33_2 short-circuits the `GetNextState` sequence, jumping past CP5–CP9 to `S33_2_DeclarationIssued`.
- `FileMaster` gains 5 flag/timestamp columns consumed by simple flag-based guards: `SpatialInfoConfirmedAt`, `WarmsReviewedAt`, `AdditionalInfoReviewedAt`, `DamMarkedNA`, `SfraMarkedNA`.
- New `AuditEvent` cross-boundary contract at `docs/contracts/audit-event.md` + fixture at `contracts/fixtures/audit/audit-event.json`.

**Guards shipped in this plan (six):**
| Guard | Check |
|---|---|
| `Cp2SpatialInfoGuard` | `FileMaster.SpatialInfoConfirmedAt` is set before leaving CP2 |
| `Cp3WarmsReviewedGuard` | `FileMaster.WarmsReviewedAt` set before leaving CP3 |
| `Cp4AdditionalInfoGuard` | `FileMaster.AdditionalInfoReviewedAt` set before leaving CP4 |
| `Cp5MapbookPresentGuard` | `FileMaster.Mapbooks` not empty before leaving CP5 |
| `Cp8DamOrNAGuard` | `DamCalculation` exists OR `DamMarkedNA` true before leaving CP8 |
| `Cp9SfraOrNAGuard` | `Forestation` exists OR `SfraMarkedNA` true before leaving CP9 |

**Deferred to later iteration** (not blocking for MVP):
- `Cp1Progress` entity with 7 sub-step booleans (CP1 sub-steps).
- `Cp6FieldCropSapwatCompleteGuard` — depends on a well-defined `FieldAndCrop.SAPWATCalculationResult` population path not yet present.
- `Cp7EluCalculatedGuard` — depends on `Validation` record with lawful/unlawful volume fields populated; that's Plan 3.5/Plan 4 territory.
- Role-based `RoleCanTransitionGuard` — controller-level `[Authorize(Policy = DwsPolicies.CanTransitionWorkflow)]` + `IScopedCaseQuery.IsInScope` already cover this at the HTTP boundary. Adding a service-level check is double-coverage not essential for MVP.

**Migration:** single `WorkflowGuardsAndAudit` migration adds 5 columns to `FileMaster`. `AuditLog` unchanged.

**Execution sequence:**
1. Write `AuditEvent` contract + fixture + CHANGELOG entry.
2. Add 5 flag columns to `FileMaster`; generate `WorkflowGuardsAndAudit` migration; apply.
3. Implement `IAuditService` / `AuditService`; fixture-driven unit test.
4. Implement `ITransitionGuard` + six concrete guards; unit tests for each (pass-fail per guard).
5. Wire guards + audit into `WorkflowService.MoveToStateAsync`; unit tests for the composed pipeline.
6. Add `AssessmentTrack`-aware next-state resolver; unit test S33(2) skip path.
7. Add Audit tab to FileMaster Details view (simple reverse-chronological list).
8. Live smoke + merge back into `demo/azure-deploy`.
