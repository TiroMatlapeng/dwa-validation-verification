# Document Uploads as Gated Workflow Evidence — Design

**Date:** 2026-06-02
**Status:** Approved (design) — pending written-spec review
**Author:** V&V engineering (with Claude)

## 1. Problem & Intent

The DWS V&V system must compile a per-property case file containing the Appendix A
evidence items. Today the workflow guards verify *data-derived* artefacts
(Mapbooks, `DamCalculation`, `Entitlement`, `FieldAndCrop`/SAPWAT, `Authorisation`)
but never verify that *uploaded supporting documents* — Title Deed report, SG
Diagram, WARMS report, previous studies — are actually attached to the case.

There is also no **internal staff** document-upload capability. A `Document` model
and an **external-portal** `DocumentController` (water users) already exist, but
DWS officials cannot attach documents to a case from the internal application, and
documents play no part in the workflow state machine.

### Intent (confirmed with stakeholder)

1. Documents are **first-class workflow evidence that gates transitions**.
2. Document upload is available **from the start of the V&V process** (case creation
   onward). Most documents are **optional** and missing ones can be **solicited from
   the water user via the external portal**.
3. Where a specific document is genuinely required by a given control point, that
   document is **mandatory at that control point** (blocks leaving it).
4. Documents must be **properly annotated**. The binary will **ultimately live in
   eWULAAS**, but the eWULAAS `IntegrationService` is not built yet, so we store
   locally now behind the existing storage abstraction and make each record
   eWULAAS-ready for a later push.

## 2. Scope

**In scope**

- `Document` model annotation fields + EF config + migration.
- A controlled document-type vocabulary aligned to Appendix A.
- Internal `DocumentController` (list / upload / download / delete), org-scoped + RBAC.
- A documents panel on the FileMaster detail view.
- A single generic `DocumentEvidenceGuard` (Approach A) + extension of the existing
  `Cp11FileCompilationGuard`, both reading one shared requirements map.
- Tests for the guard, the requirements map, and the controller.

**Out of scope (explicitly deferred)**

- eWULAAS document push (waits for `IntegrationService`). We add the fields and sync
  status now; the actual transport is a later task.
- Antivirus scanning transport (the `VirusScanStatus` field exists; no scanner is
  wired — `Infected` is honoured by guards, see §5).
- Azure Blob backing implementation (swap behind `IFileStorage` later; no code here).
- Admin-configurable requirements table (Approach B) — see §10 future work.
- `Mark N/A` / waiver for mandatory documents — see §10 future work.

## 3. Decisions (locked)

| # | Decision |
|---|----------|
| D1 | Documents gate workflow transitions (not view-only). |
| D2 | Upload available from case start; most docs optional; selected docs mandatory per CP. |
| D3 | **Approach A** — one generic guard with a hardcoded shared requirements map (mirrors `LetterServiceConfirmedGuard._map`). Not a config table. |
| D4 | Storage: local/Blob now via `IFileStorage`; annotate fully; add `ExternalDocumentRef` + `SyncStatus` for a later eWULAAS push. |
| D5 | CP→document mapping: CP2 = Title Deed Report + SG Diagram; CP3 = WARMS Report; CP11 re-checks all. `PreviousStudy` and everything else optional. |

## 4. Model Changes

### 4.1 `Document` (existing model — add fields)

```csharp
// New annotation / eWULAAS-readiness fields on Models/Document.cs
public Guid? WorkflowStateId { get; set; }      // control point this doc satisfies/belongs to
public WorkflowState? WorkflowState { get; set; }
public string? ExternalDocumentRef { get; set; } // eWULAAS document id (null until synced)
public string SyncStatus { get; set; } = "NotSynced"; // NotSynced | Pending | Synced | Failed
```

`DocumentType` remains a `string` column but its values are now constrained to the
controlled vocabulary (§4.2) at the application layer (upload validation). Existing
freeform values are left as-is (no data migration of historical rows).

### 4.2 `DocumentTypes` controlled vocabulary (new static class)

No new DB table. A static class exposes the canonical codes, display names, and an
`IsAppendixA` flag, consumed by both the internal and external uploaders and by the
requirements map.

```csharp
// Services/Documents/DocumentTypes.cs (illustrative)
public static class DocumentTypes
{
    public const string WarmsReport     = "WARMSReport";      // Appendix A item 2
    public const string TitleDeedReport = "TitleDeedReport";  // Appendix A item 3
    public const string SgDiagram       = "SGDiagram";        // Appendix A item 4
    public const string PreviousStudy   = "PreviousStudy";    // Appendix A item 5 (optional)
    public const string TitleDeed       = "TitleDeed";
    public const string Permit          = "Permit";
    public const string FieldSurvey     = "FieldSurvey";
    public const string Correspondence  = "Correspondence";
    public const string Other           = "Other";

    // code -> (display name, isAppendixA). Drives dropdowns and the docs panel grouping.
    public static readonly IReadOnlyDictionary<string, (string Display, bool IsAppendixA)> All = ...;
}
```

### 4.3 EF config + migration

- Configure `Document.WorkflowStateId` as an optional FK to `WorkflowState` with
  `DeleteBehavior.SetNull` (deleting a state must not cascade-delete documents).
- `SyncStatus` non-null, max length 16; `ExternalDocumentRef` nullable, max length 200.
- One migration: `AddDocumentWorkflowAnnotationAndSync`.

## 5. The Gate — `DocumentEvidenceGuard` (Approach A)

A single `ITransitionGuard` in `Services/Workflow/Guards/`. It reads a shared,
hardcoded requirements map and denies a transition when a **mandatory** document
type for the state *being left* has no satisfying `Document` on the case.

### 5.1 Shared requirements map

```csharp
// Services/Workflow/Guards/DocumentRequirements.cs
// state name -> required document types (mandatory) for leaving that state.
public static class DocumentRequirements
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<DocReq>> Map =
        new Dictionary<string, IReadOnlyList<DocReq>>(StringComparer.OrdinalIgnoreCase)
    {
        ["CP2_SpatialInfo"]     = [ new(DocumentTypes.TitleDeedReport, "Title Deed report"),
                                    new(DocumentTypes.SgDiagram,       "SG Diagram") ],
        ["CP3_WARMSEvaluation"] = [ new(DocumentTypes.WarmsReport,     "WARMS report") ],
    };

    // The full Appendix-A document set the CP11 compilation guard must re-check.
    public static readonly IReadOnlyList<DocReq> FileCompilationDocuments =
        [ new(DocumentTypes.WarmsReport, "WARMS report"),
          new(DocumentTypes.TitleDeedReport, "Title Deed report"),
          new(DocumentTypes.SgDiagram, "SG Diagram") ];
}
public record DocReq(string DocumentType, string DisplayName);
```

### 5.2 Guard logic

```
DocumentEvidenceGuard.CheckAsync(ctx):
  if ctx is not leaving a state in DocumentRequirements.Map -> Ok      // same IsLeaving helper
  for each required DocReq for the current state:
      exists = any Document where FileMasterId == ctx.FileMaster.FileMasterId
               && DocumentType == req.DocumentType
               && VirusScanStatus != "Infected"
      if not exists -> Deny($"{req.DisplayName} must be uploaded before leaving this control point.")
  -> Ok
```

- A document with `VirusScanStatus == "Infected"` never satisfies a requirement.
  `Pending`, `Clean`, and `null` all satisfy it (no AV scanner is wired yet).
- Reuses the existing `Cp2SpatialInfoGuard.IsLeaving(ctx, prefix)` style check
  (matched on exact state name via the map key rather than prefix).

### 5.3 CP11 re-check

`Cp11FileCompilationGuard` is extended to iterate
`DocumentRequirements.FileCompilationDocuments` and `Deny` for any missing item,
using the same non-`Infected` existence check. No logic is duplicated — both guards
consume `DocumentRequirements`.

### 5.4 DI registration

Register `DocumentEvidenceGuard` alongside the other guards in `Program.cs`
(`builder.Services.AddScoped<ITransitionGuard, DocumentEvidenceGuard>()`). Blocking
reasons surface automatically through the existing `GetBlockingReasonsAsync`.

## 6. Internal `DocumentController` (main MVC area)

New `Controllers/DocumentController.cs`, mirroring the external-portal controller but
using internal identity, org-scoping, RBAC, audit, and notifications.

| Action | Method | RBAC | Notes |
|--------|--------|------|-------|
| `List(fileMasterId)` | GET | ReadOnly+ | Documents for the case, grouped by Appendix A type. |
| `Upload(fileMasterId)` | GET | Capturer+ | Upload form with type dropdown + optional CP tag. |
| `Upload(model)` | POST | Capturer+ | Validates type ∈ vocabulary; stores via `IFileStorage`; audit + notify. |
| `Download(documentId)` | GET | ReadOnly+ | Streams from `IFileStorage`. |
| `Delete(documentId)` | POST | Validator+ | Soft scope check; audit. |

- **Scoping:** every action confirms the case is in the user's org scope via
  `IScopedCaseQuery.IsInScope` (or `FilterFileMasters`), returning `Forbid()` otherwise.
- **Validation:** allowed extensions `.pdf .jpg .jpeg .png .tiff`; size limit 25 MB
  (SG diagrams are large). `DocumentType` must be in `DocumentTypes.All`.
- **On upload:** set `UploadedByUserId`, `UploadDate`, `DocumentHash` (from
  `StoredFileResult.Sha256Hex`), `VirusScanStatus = "Pending"`, `SyncStatus =
  "NotSynced"`, optional `WorkflowStateId`. Write an `AuditLog` via `IAuditService`
  and notify via `INotificationService`.
- **On delete:** hard delete — call `IFileStorage.DeleteAsync(relativePath)` (idempotent)
  then remove the `Document` row, in that order; audit the action with the document
  metadata (id, type, file name) captured before removal. A delete that would remove
  the last satisfying document for a *mandatory* type on a case that has **not yet
  passed** that control point is allowed but logged (the guard will re-block the next
  transition); no special prevention for now.

## 7. UI — Documents panel on FileMaster detail

A new partial `Views/FileMaster/_DocumentsPanel.cshtml`, rendered on `Details.cshtml`
beside `_WorkflowPanel`/`_LettersPanel`.

- Documents grouped by Appendix A item; each row shows file name, type, upload date,
  uploader, and download/delete (delete for Validator+).
- A **requirements checklist** region shows each mandatory item as **present** or
  **missing**, and **mandatory** vs **optional**, using the same blocking-reason
  styling the workflow panel already uses — so a Validator sees exactly which missing
  document is blocking the next transition.
- Inline upload form (type dropdown from `DocumentTypes`, file picker).
- DWS brand palette only (no generic/Tailwind defaults), consistent with existing panels.

## 8. Data Flow

```
Validator/Capturer -> Internal DocumentController.Upload (POST)
  -> IScopedCaseQuery.IsInScope (Forbid if out of scope)
  -> validate extension/size/type
  -> IFileStorage.SaveAsync -> StoredFileResult (relative path + SHA-256)
  -> persist Document (annotated, SyncStatus=NotSynced)
  -> IAuditService.Write + INotificationService notify
WorkflowService.AdvanceAsync / TransitionToAsync
  -> runs all ITransitionGuard incl. DocumentEvidenceGuard
  -> Deny w/ reason if a mandatory doc for the state-being-left is missing/Infected
  -> GetBlockingReasonsAsync surfaces reasons to the docs panel + workflow panel
[future] IntegrationService -> push NotSynced docs to eWULAAS -> set ExternalDocumentRef, SyncStatus=Synced
```

## 9. Testing

- **`DocumentEvidenceGuard`**: for each mapped state — all mandatory present → Ok;
  one missing → Deny (correct message); present-but-`Infected` → Deny; not leaving
  the state → Ok; leaving an unmapped state → Ok.
- **`Cp11FileCompilationGuard`**: existing tests still pass; new cases for each
  missing Appendix-A document.
- **`DocumentRequirements`**: every referenced `DocumentType` exists in
  `DocumentTypes.All`; every mapped state name exists in the seeded workflow states.
- **`DocumentController`**: upload happy path; rejected extension/size/type;
  out-of-scope → Forbid; RBAC (Capturer can upload, ReadOnly cannot; only Validator+
  can delete); audit + notify invoked.

## 10. Future Work (noted, not built now)

- eWULAAS document push via `IntegrationService` (consume `SyncStatus`/`ExternalDocumentRef`).
- Real antivirus scanning that sets `VirusScanStatus`.
- Azure Blob `IFileStorage` implementation.
- Admin-configurable requirements (Approach B) if the client wants tunable rules.
- `Mark N/A` / documented waiver for a mandatory document where a property
  legitimately lacks it (e.g. no SG diagram), mirroring `DamMarkedNA`/`SfraMarkedNA`.

## 11. Risks & Edge Cases

- **S33(2) declaration track:** CP2/CP3 precede the CP5–CP9 skip, so Title Deed / SG
  Diagram / WARMS report remain required for declaration cases. CP11 is not on the
  S33(2) path, so its document re-check does not fire there.
- **Deleting a satisfying document after the CP was passed:** allowed; the case has
  already advanced and does not normally re-enter the earlier state.
- **Historical freeform `DocumentType` values:** left intact; only new uploads are
  constrained to the vocabulary. The guard matches on the canonical codes.
- **State-name coupling:** the requirements map keys on seeded `WorkflowState.StateName`
  values; a unit test asserts each key exists to catch rename drift.
