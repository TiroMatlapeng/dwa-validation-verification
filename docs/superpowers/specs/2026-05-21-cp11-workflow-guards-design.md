# CP11 File Compilation + PAJA Guard + Letter Service Confirmation Guards — Design

## Goal

Add three missing workflow-engine enforcement points that currently have no guard:

1. **CP11 `FileCompiled` state + guard** — a new control point after CP9 SFRA that requires the V&V file to contain all nine Appendix A evidence items before the case can proceed to public review.
2. **CP19 PAJA Checklist guard** — block issuance of Letter 3 (ELU certificate) until the PAJA four-field checklist is marked complete.
3. **Letter service confirmation guards** — require proof of delivery (`ServiceConfirmedDate`) on each Section 35 letter before the case can advance past the `*Issued` state.

All three are pure workflow-engine changes. No new models, no UI beyond what already exists.

---

## Architecture

The implementation follows the existing `ITransitionGuard` pattern in `Services/Workflow/Guards/FlagGuards.cs`:

- Each guard is a class implementing `ITransitionGuard`.
- Guards receive a `GuardContext` (FileMaster, current state name, target state name, optional user).
- Guards return `GuardResult.Ok` or `GuardResult.Deny(reason)`.
- Guards that need DB access take `ApplicationDBContext` in their constructor (injected by DI).
- Guards are registered in `Program.cs` as `AddScoped<ITransitionGuard, XxxGuard>()`.
- `WorkflowService.CanAdvanceAsync` and `AdvanceAsync` call every registered guard; all must return `Ok` for a transition to proceed.

**S33(2) skip**: `WorkflowService.CpsSkippedOnS33_2` must include `"CP11"` so declaration-track cases skip the new control point and jump directly to `S33_2_ReadyForDeclaration`.

**Tech stack**: ASP.NET Core 10, EF Core 10, SQL Server, xUnit, in-memory DB via `TestDbContextFactory.Create()`.

---

## Section 1 — CP11 File Compilation State and Guard

### New Workflow State

Insert `CP11_FileCompiled` at **DisplayOrder 16** in `SeedDataService.SeedWorkflowStatesAsync()`:

| State Name | Phase | DisplayOrder | IsTerminal |
|---|---|---|---|
| `CP9_SFRACalculated` | Verification | 15 | false |
| **`CP11_FileCompiled`** | **Verification** | **16** | **false** |
| `CP_PrePublicReview` | Verification | 17 | false |
| `CP_StakeholderWorkshop` | Verification | 18 | false |
| `S35_Letter1Issued` | Verification | 19 | false |
| … all subsequent states shift by +1 … | | | |

The seed is idempotent — existing rows get their `DisplayOrder` corrected on re-run; no data loss.

### Guard: `Cp11FileCompilationGuard`

**File**: `Services/Workflow/Guards/FlagGuards.cs` (appended to existing file)

**Fires when**: `currentState.StartsWith("CP11")` AND `targetState` does not start with `"CP11"`.

**Checks** (all must pass; each produces a distinct denial message):

| # | Check | Denial message |
|---|---|---|
| 1 | `FileMaster.WarmsReviewedAt.HasValue` | "WARMS review must be recorded before file can be compiled." |
| 2 | `FileMaster.Property.SGCode` is not null or whitespace | "Property SG code must be confirmed before file can be compiled." |
| 3 | At least one `Authorisation` record exists for this `FileMasterId` | "At least one authorisation record must be captured before file can be compiled." |
| 4 | At least one `FieldAndCrop` record exists with `SAPWATCalculationResult > 0` | "At least one field with a SAPWAT result must be captured before file can be compiled." |
| 5 | A `Mapbook` with `MapType == "Qualifying"` exists for this `FileMasterId` | "Qualifying period mapbook must be present before file can be compiled." |
| 6 | `FileMaster.EntitlementId.HasValue` | "Entitlement must be linked before file can be compiled." |
| 7 | A `Mapbook` with `MapType == "Current"` exists for this `FileMasterId` | "Current period mapbook must be present before file can be compiled." |
| 8 | `DamCalculation` exists OR `FileMaster.DamMarkedNA == true` | "Dam volume calculation must be recorded or marked N/A before file can be compiled." |
| 9 | `Forestation` exists OR `FileMaster.SfraMarkedNA == true` | "SFRA/Forestation record must be recorded or marked N/A before file can be compiled." |

> **Note on check 9**: `FileMaster.SfraMarkedNA` must be confirmed as an existing bool field before implementation. If the field is named differently, use the actual name — do NOT use null-forgiving or add a new field.

### S33(2) skip

In `WorkflowService.cs`, add `"CP11"` to `CpsSkippedOnS33_2`:

```csharp
private static readonly string[] CpsSkippedOnS33_2 = {
    "CP5", "CP6", "CP7", "CP8", "CP9", "CP11",
    "CP_PrePublicReview", "CP_StakeholderWorkshop"
};
```

---

## Section 2 — CP19 PAJA Checklist Guard

### Guard: `Cp19PajaChecklistGuard`

**File**: `Services/Workflow/Guards/FlagGuards.cs` (appended)

**Fires when**: target state name is `"S35_Letter3Issued"` (regardless of current state — this guard protects entry into the ELU certificate state).

**Check**: A `PAJAChecklist` linked to `FileMasterId` must exist AND `IsComplete == true`.

`PAJAChecklist.IsComplete` is a computed property: all four text fields (`FactualBasis`, `LegalBasis`, `UserInputConsideration`, `FinalReasoning`) must be non-empty AND `CompletedAt.HasValue`.

**Denial messages**:
- If no `PAJAChecklist` row exists: `"PAJA checklist must be completed before Letter 3 (ELU certificate) can be issued."`
- If row exists but `!IsComplete`: `"PAJA checklist is incomplete — all four sections must be filled and the checklist must be marked complete before Letter 3 can be issued."`

---

## Section 3 — Letter Service Confirmation Guards

### Guard: `LetterServiceConfirmedGuard`

**File**: `Services/Workflow/Guards/FlagGuards.cs` (appended)

**Fires when**: current state is one of the four `*Issued` states AND target state does not start with the same letter prefix. Specifically:

| Current state | Letter type code to check | Denial message |
|---|---|---|
| `S35_Letter1Issued` | `S35_L1` | "Letter 1 service must be confirmed (proof of delivery recorded) before advancing." |
| `S35_Letter1AIssued` | `S35_L1A` | "Letter 1A service must be confirmed before advancing." |
| `S35_Letter2Issued` | `S35_L2` | "Letter 2 service must be confirmed before advancing." |
| `S35_Letter2AIssued` | `S35_L2A` | "Letter 2A service must be confirmed before advancing." |

**Check**: Query `LetterIssuances` where `FileMasterId == ctx.FileMaster.FileMasterId` AND `LetterType.LetterName == <code>`. The most recent such record must have `ServiceConfirmedDate.HasValue`.

The join path: `LetterIssuance.LetterType` is a navigation property; `LetterType.LetterName` holds the canonical code (e.g. `"S35_L1"`). Include `LetterType` in the query.

---

## Testing

All tests use `TestDbContextFactory.Create()` (in-memory EF Core) — no mocks of `ApplicationDBContext`.

Test file: `Tests/Services/Workflow/Guards/FlagGuardsTests.cs` (existing file — append new test classes).

### `Cp11FileCompilationGuardTests`

Nine `Deny` tests (one per missing item) and one `Ok` test (all nine items present). The `Ok` test is the reference setup — copy it as a base then remove the relevant item for each `Deny` test.

Pattern:
```csharp
var db = TestDbContextFactory.Create();
// seed FileMaster, Property, related records
var guard = new Cp11FileCompilationGuard(db);
var ctx = new GuardContext(fm, currentState: "CP11_FileCompiled", targetState: "CP_PrePublicReview", ...);
var result = await guard.EvaluateAsync(ctx, CancellationToken.None);
Assert.True/False(result.Allowed);
```

### `Cp19PajaChecklistGuardTests`

Three tests:
1. `Deny` — no `PAJAChecklist` row exists.
2. `Deny` — row exists but `IsComplete == false` (one field is null).
3. `Ok` — row exists, all four fields non-empty, `CompletedAt` set.

### `LetterServiceConfirmedGuardTests`

Eight tests (two per letter: `Deny` with no `ServiceConfirmedDate`, `Ok` with date set).

---

## Files Changed

| Action | File |
|---|---|
| Modify | `Services/SeedDataService.cs` — insert `CP11_FileCompiled` at DO 16; shift subsequent DOs |
| Modify | `Services/WorkflowService.cs` — add `"CP11"` to `CpsSkippedOnS33_2` |
| Modify | `Services/Workflow/Guards/FlagGuards.cs` — append three new guard classes |
| Modify | `Program.cs` — register three new guards as `AddScoped<ITransitionGuard, XxxGuard>()` |
| Modify | `Tests/Services/Workflow/Guards/FlagGuardsTests.cs` — append new test classes |

No new files. No model changes. No migrations.

---

## Constraints

- `AdvanceAsync` is blocked from `S33_2_ReadyForDeclaration` (existing behaviour, must not regress).
- Seed is idempotent — `CP11_FileCompiled` insertion must not break tests that rely on `SeedWorkflowStatesAsync`.
- Existing guard tests must remain green.
- Do not use null-forgiving operators (`!`) on navigation properties that may be null — load with `Include` or check for null.
