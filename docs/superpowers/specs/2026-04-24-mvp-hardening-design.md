# MVP Hardening — Design Spec

**Date:** 2026-04-24
**Branch:** `demo/azure-deploy`
**Status:** Approved

## Purpose

The MVP has been deployed to Azure (`dwa-vv-demo.azurewebsites.net`) but is effectively an open read/write data explorer. This iteration turns it into a demonstrable, role-aware V&V case management system with guarded workflow transitions, PDF letter generation, an immutable audit trail, and a UI consistent with the approved wireframes.

## Scope (in)

1. Authentication and role-based authorisation (ASP.NET Identity).
2. Workflow transition guards and assessment-track branching.
3. Letter PDF generation and issuance flow (QuestPDF).
4. Immutable audit trail via `AuditService`.
5. UI re-skin to `docs/wireframes/modern-internal-wireframes.html`.
6. Human field labels via `[Display(Name = …)]` sweep.

## Scope (deferred)

HDI indicator on user profile; property subdivide/consolidate data model and flow; V&V case number generator; full X.509 digital signatures and SignaturePad.js UI; email/SMS notifications; external public-user portal (separate auth realm, MFA, document upload, objections); full `LawfulnessAssessmentService` (GWCA rules + riparian rights + Section 9B + DWS notices per jurisdiction and catchment); SAPWAT / dam volume / SFRA calculator engines; eWULAAS integration; Objection string sweep in views.

---

## 1. Authentication & Authorization

### Identity stack
- ASP.NET Core Identity on top of the existing `ApplicationUser : IdentityUser`.
- Cookie authentication for the internal staff app.
- Six seeded roles: `SystemAdmin`, `NationalManager`, `RegionalManager`, `Validator`, `Capturer`, `ReadOnly`.
- No internal staff self-registration — accounts are provisioned by a `SystemAdmin` through a "Manage Users" admin page.

### Role + scope model
- Roles carried as standard Identity role claims.
- Org-unit scope carried as custom claims `orgUnitId`, `provinceId`, `wmaId`, `catchmentId`, populated on sign-in from `ApplicationUser.OrgUnitId` via a lookup against `OrganisationalUnit`.
- An `IScopedCaseQuery` service wraps `FileMaster` / `Property` queries with an org-unit filter. `NationalManager` and `SystemAdmin` bypass the filter; everyone else is restricted. Repositories stay pure — scope is applied at the service boundary.

### Authorization
- `[Authorize]` at layout level; unauthenticated users redirect to Login.
- Named policies (registered in `Program.cs`):
  - `CanAdminister` — SystemAdmin only.
  - `CanCreateCase` — Validator and above.
  - `CanTransitionWorkflow` — Validator and above, scope-checked.
  - `CanIssueLetter` — RegionalManager and above, scope-checked.
  - `CanCapture` — Capturer and above, scope-checked.
- Controllers reference policies, not role strings, so claim source can change without rewriting authorisation.

### Entra ID readiness
- Authentication pipeline in `Program.cs` structured so `AddIdentity(...)` can be swapped for `AddAuthentication(...).AddMicrosoftIdentityWebApp(...)` in one place.
- A single `IClaimsTransformation` implementation maps signed-in user → role + scope claims, regardless of whether claims originate from Identity or Entra.

### Demo seed users
One user per role, scoped to the WMA of the existing three seeded sample cases, with known passwords sourced from `appsettings.Development.json` only. Role and seed-user provisioning runs on app startup and is idempotent (skipped if the Identity role table is populated).

### UI
- Re-skinned Login, Logout, Access Denied, Manage Users views.
- Manage Users page supports: list staff, create staff (email, name, employee number, role, org unit), reset password, deactivate.

---

## 2. UI Re-skin to Wireframes

### Source of truth
`docs/wireframes/modern-internal-wireframes.html` — taken as the approved design at the start of implementation.

### Shell
- Rewrite `Views/Shared/_Layout.cshtml` to match the wireframe: DWS-branded top bar, left sidebar, main content area with a page-header slot (title, breadcrumb, primary-action button), flash-message region.
- Extract sidebar into `_Sidebar.cshtml` partial. Nav items driven by the signed-in user's role and scope (hide admin links from non-admins, hide Create buttons from `ReadOnly`).
- `_LoginLayout.cshtml` for login / access-denied — same brand shell, no sidebar.

### Shared CSS module
Single `wwwroot/css/dws.css`, loaded after Bootstrap's reset. Contents:
- DWS brand tokens (colours, type scale, spacing, radii) as CSS custom properties. No Tailwind defaults, no inline styles, no Bootstrap colour utilities.
- Component classes: `.dws-card`, `.dws-panel`, `.dws-form`, `.dws-form-row`, `.dws-table`, `.dws-btn` (with `-primary` / `-secondary` / `-ghost` / `-danger`), `.dws-status-pill` (with state modifiers), `.dws-breadcrumb`, `.dws-page-header`.
- Workflow-specific: `.dws-workflow-stepper` (active / done / pending), `.dws-letter-card`.

### Views ported to the new shell
Home/Index, Property list/Details/Create/Edit/Delete, FileMaster list/Details/Create/Edit/Delete (including `_WorkflowPanel` and `_LettersPanel`), Owner admin views, all new Auth views.

### Out of this round
- No tag helpers or view components for form fields.
- No styleguide page.
- No client-side framework — server-rendered Razor with vanilla JS for the letter-issue modal and workflow-advance confirmation.

### Consistency guard
After the port, sweep the repo for inline `style="..."` blocks and Bootstrap colour utilities and remove them. All colour must route through DWS tokens.

---

## 3. Workflow Guards, Track Branching, Audit

### Transition guards
- `ITransitionGuard` interface; `GuardResult { bool Allowed, string? Reason }`.
- `WorkflowService.AdvanceAsync` / `TransitionToAsync` evaluate the full guard list for the target state before applying any change. First failure blocks; reason surfaces to the UI (disabled Advance button with tooltip, or inline error on forced transitions).

### Guard catalogue

| Guard | Check |
|---|---|
| `Cp1SubStepsComplete` | CP1 cannot leave until sub-steps 1.1–1.7 are ticked on `Cp1Progress` |
| `Cp2SpatialInfoCaptured` | `FileMaster.SpatialInfoConfirmedAt` is set |
| `Cp3WarmsReviewed` | `FileMaster.WarmsReviewedAt` is set |
| `Cp4AdditionalInfoCaptured` | `FileMaster.AdditionalInfoReviewedAt` is set |
| `Cp5MapbookPresent` | `Mapbook` records exist for both qualifying and current periods |
| `Cp6FieldCropSapwatComplete` | Every `FieldAndCrop` on the case has `SAPWATCalculationResult` set |
| `Cp7EluCalculated` | At least one `Validation` record with lawful + unlawful volumes |
| `Cp8DamCalculatedOrNA` | `DamCalculation` exists or `FileMaster.DamMarkedNA` is true |
| `Cp9SfraCalculatedOrNA` | `Forestation` exists or `FileMaster.SfraMarkedNA` is true |
| `RoleCanTransition` | User holds `CanTransitionWorkflow` policy for this case's org unit |

### CP1 sub-step tracking
New `Cp1Progress` entity, 1:1 with `FileMaster`, seven booleans + completion timestamps matching CLAUDE.md CP1 sub-steps (WARMS imported, satellite imagery obtained, database audit complete, unregistered users identified, database analysis complete, inception report approved, PPS1 complete).

### Assessment-track branching
`FileMaster.AssessmentTrack` (`S35`, `S33_2`, `S33_3`) drives workflow routing:

- `S35` → standard CP1 → CP9 → S35 letter state machine.
- `S33_2` (Kader Asmal) → CP1 sub-steps → lightweight scheduled-area membership + rates-paid validation → `S33_2_DeclarationIssued` → `Closed`. CP5, CP6, CP7, CP8, CP9 are skipped.
- `S33_3` → standard CP1 → CP9 → `S33_3_DeclarationIssued` → `Closed`.

Implemented via a `TrackSkipPredicate` consulted by `WorkflowService.GetNextState(fileMaster)` before falling back to `DisplayOrder`. Track logic lives in this one place.

### Audit service
- `IAuditService.LogAsync(AuditEvent)` writes an immutable row to `AuditLog`.
- Called explicitly from `WorkflowService`, `LetterService`, user-admin controller. No global filter.
- `AuditLog` fields: `OccurredAt`, `UserId`, `UserDisplayName`, `EntityType`, `EntityId`, `Action`, `FromValue`, `ToValue`, `Reason`.
- Exposed as an Audit tab on FileMaster Details.

---

## 4. Letter Generation (PDF)

### Library
QuestPDF. Pure-managed, works on Azure App Service without native dependencies. `QuestPDF.Settings.License = LicenseType.Community` set in `Program.cs` for the demo build. Commercial-licence eligibility for DWS as a government department must be confirmed before a production rollout — if Community does not apply, a Professional licence is purchased and the setting changed to `LicenseType.Professional` with the key held in App Service configuration.

### Service model
- `ILetterService`
  - `Task<LetterPreview> RenderPreviewAsync(Guid fileMasterId, LetterTypeCode code)`
  - `Task<LetterIssuance> IssueAsync(Guid fileMasterId, LetterTypeCode code, IssueLetterRequest req)`
  - `Task RecordResponseAsync(Guid letterIssuanceId, RecordResponseRequest req)`
- `IPdfRenderer` — thin wrapper over QuestPDF so `LetterService` is unit-testable without the library in tests.
- `ILetterTemplateRegistry` — resolves `LetterTypeCode` → `ILetterTemplate` implementation.

### Templates
One class per letter type under `Services/Letters/Templates/`:

- S35: `S35Letter1Template`, `S35Letter1ATemplate`, `S35Letter2Template`, `S35Letter2ATemplate`, `S35Letter3Template`, `S35Letter4ATemplate`, `S35Letter4And5Template`.
- S33: `S33_2DeclarationTemplate`, `S33_3aDeclarationTemplate`, `S33_3bDeclarationTemplate`.

Shared header and footer live in `DwsLetterLayout`. Each template builds its QuestPDF document tree from a `LetterContext` (property, owner, case, WMA, signatory, issue date, due date, reference number).

### Template content
Legal wording lifted from the DWS Requirements Document (Edition 3, July 2024) and the Application of Legal Principles presentation. Placeholder tokens filled from `LetterContext`. Content undergoes a one-pass legal-wording review from a Validator user before the iteration is considered complete.

### Storage
- `IBlobStore { WriteAsync, ReadAsync, GetSasUrl }` abstracts storage.
- Production: Azure Blob Storage container `dwa-letters`.
- Dev: local filesystem at `wwwroot/_uploads/letters/` (gitignored).
- `LetterIssuance` stores only `BlobPath` + metadata — never raw PDF bytes.

### Section 35(2)(d) personal-service enforcement
The issuance form's delivery-method dropdown requires `ServedByOfficialId` for `PersonalService`, and `PostalRegistrationNumber` for `RegisteredMail`. Letter 1 (S35(1)) has `PersonalService` as the default and blocks issuance without a server assigned.

### Signing (minimal for this round)
Typed-name + password re-auth ("Sign as Jane Doe — confirm password"). Captures on `LetterIssuance`:
- `SignedByUserId`
- `SignedAt`
- `SignatureHash` = SHA-256 of the PDF bytes at sign time.

Full X.509 signatures and SignaturePad.js are deferred.

### UI flow on `_LettersPanel`
1. "Issue Letter" → modal: pick letter type (filtered by current workflow state + track), confirm recipient, preview PDF in inline iframe, set delivery method + due date, click "Issue & Sign" → password re-auth → letter appears as an issued card.
2. Issued cards show type, issued date, due date, delivery method, signed-by, PDF download, "Record Response" action, "Mark Served" action (Letter 1 only).
3. Recording a response calls `WorkflowService` (which respects guards) to advance state, and writes an audit row.

### Notification stub
`INotificationService.NotifyLetterIssued(letterIssuance)` writes a `Notification` row and logs to console. Email/SMS wiring is deferred.

---

## 5. Field Labels

- Sweep all 49 entity classes in `Models/`; apply `[Display(Name = "...")]` to every public property that appears on a view.
- Convention: human English Title Case; abbreviations kept uppercase (e.g. `SG Code`, `WARMS Registration Number`, `SAPWAT Calculation Result`, `Quaternary Drainage`).
- Views switched to `@Html.DisplayNameFor(m => m.X)` / `LabelFor` / `DisplayFor` / `EditorFor` (currently a mix of hard-coded labels and raw property names).
- Enum members labelled with `[Display(Name = "...")]`; rendered through `EnumHelper.GetDisplayName()` or a tag helper.
- Screens that transform data (joins, computed fields, flattening) continue to use view models under `ViewModels/` — `FileMasterDetailsViewModel` is the existing pattern.

---

## 6. Data-model changes

All covered by a single migration named `MvpHardening`:

- New entity: `Cp1Progress` (Id, FileMasterId FK, seven booleans + completion timestamps).
- `FileMaster` adds: `DamMarkedNA`, `SfraMarkedNA`, `SpatialInfoConfirmedAt`, `WarmsReviewedAt`, `AdditionalInfoReviewedAt`.
- `LetterIssuance` adds: `BlobPath`, `SignedByUserId`, `SignedAt`, `SignatureHash`, `ServedByOfficialId`, `PostalRegistrationNumber`, `DueDate`, `DeliveryMethod`.
- `AuditLog` adds (only if missing): `UserDisplayName`, `FromValue`, `ToValue`, `Reason`.
- Rename `Interfaces/IEntitlement.cs` declaration from `public interface Entitlement` to `public interface IEntitlement` and update any references (resolves the CLAUDE.md-flagged naming collision).
- Seed Identity roles and demo users on startup (idempotent).

Migration applied in CI on deploy via the existing `Database.MigrateAsync()` call in `Program.cs`.

---

## 7. Testing

- **Unit tests** (xUnit, added to existing `Tests/` project):
  - Each guard — happy path + failure path.
  - `ITransitionGuard` composition: guards run in order, first failure short-circuits.
  - `IClaimsTransformation` — user with role + org unit produces expected claim set.
  - Each letter template — render to PDF bytes, assert specific text content (not byte equality).
  - `AuditService` — row written with expected fields.
  - `IScopedCaseQuery` — filter behaviour per role.
- **Integration tests:**
  - Login flow for each of the six roles.
  - Policies against seeded cases — authorised + denied paths.
  - Workflow advance with guards satisfied; workflow advance blocked when a guard fails.
  - Letter issuance end-to-end — PDF produced, blob stored, `LetterIssuance` row written, audit row written.
- Existing 33 tests must stay green.

---

## 8. Deployment

- `appsettings.Production.json` stays secret-free. New settings injected via Azure App Service Configuration: `Identity:LockoutOptions`, `Storage:BlobContainer`, seeded-user initial passwords.
- GitHub Actions workflow (`.github/workflows/deploy-azure.yml`) already runs EF migrations on startup — no workflow change needed.
- `deploy/provision-azure.sh` adds an Azure Storage Account for `dwa-letters`.
- Identity role and seed-user provisioning runs on startup after migrations; idempotent.

---

## 9. Success criteria

- A SystemAdmin can sign in, create other staff users with roles and org units, and those users can sign in.
- A Validator in Limpopo WMA sees only Limpopo cases in the FileMaster list; a NationalManager sees all cases.
- A Validator cannot advance CP5 on a case without at least one `Mapbook` for each period; the UI states the reason.
- A RegionalManager can issue a Section 35 Letter 1 with personal-service details, sign it with password re-auth, and the PDF downloads. The signed letter appears on the case's `_LettersPanel` and an audit row is visible on the Audit tab.
- An S33(2) case skips CP5–CP9 and lands at `S33_2_DeclarationIssued`.
- Every screen uses the wireframe shell and `dws.css` tokens — no Bootstrap colour utilities, no inline styles.
- Field labels on every screen read as English ("SG Code" not "SGCode", "WARMS Registration Number" not "WarmsRegistrationNumber").
- All unit + integration tests pass; existing 33 tests still pass.
