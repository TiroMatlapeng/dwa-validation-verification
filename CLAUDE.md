# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is the **DWA V&V System** — an electronic information management system for Validation & Verification (V&V) of existing lawful water use (ELU) and compulsory licensing, as mandated by Sections 32–35 of South Africa's National Water Act (Act 38 of 1998). It manages ~500,000 property records across DWS/CMA regional offices, with a target completion date of 2026.

The governing requirements document is: *"Requirements for Verifying the Lawfulness of Existing Water Use and Extent of Existing Lawful Water Use, Edition 3, July 2024"* (DWS). A copy is in the project root.

## Technology Stack

- **ASP.NET Core 10.0** (MVC pattern, Razor Views)
- **Entity Framework Core 10.0** with SQL Server 2022
- **SQL Server** running in Docker on `localhost:1433`, database `dwa_val_ver`
- **Nullable reference types** and **implicit usings** are enabled globally

## Commands

```bash
# Build
dotnet build

# Run (development)
dotnet run
# or with hot reload:
dotnet watch run

# EF Core migrations
dotnet ef migrations add <MigrationName>
dotnet ef database update
dotnet ef migrations remove   # undo last migration

# Restore dependencies
dotnet restore
```

The database connection string is in `appsettings.json`. SQL Server must be running in Docker before starting the app.

## Architecture

### Layer Structure

```
Controllers  →  Services (planned)  →  Repositories  →  ApplicationDBContext  →  SQL Server
```

- **Controllers** (`Controllers/`): Thin HTTP boundary. Inject repository interfaces via constructor DI.
- **Services** (`Services/`): Business logic and orchestration layer — currently being built out (`PropertyService` is a stub).
- **Repositories** (`Repositories/`): Data access via EF Core. Implement `IXxx` interfaces.
- **Interfaces** (`Interfaces/`): Repository contracts injected into controllers/services.
- **Models** (`Models/`): EF Core entity classes. All keys are `Guid` configured explicitly in `OnModelCreating`.
- **DatabaseContexts/ApplicationDBContext.cs**: Single `DbContext`. All primary keys and relationships configured here, not via data annotations.

### Dependency Injection (Program.cs)

New repositories and services must be registered in `Program.cs`:
```csharp
builder.Services.AddScoped<IPropertyInterface, PropertyRepository>();
builder.Services.AddScoped<IAddress, AddressRepository>();
// Add new: builder.Services.AddScoped<IXxx, XxxRepository>();
```

### Key Domain Entities

| Entity | Purpose |
|--------|---------|
| `Property` | Central entity — a farm/land parcel with SGCode, Quaternary Drainage, Water Management Area, geo-coordinates, and a linked `Entitlement` |
| `FileMaster` | The V&V case file per property; links to Property, WARMS registration number, validation status, validation person |
| `Entitlement` | A water use entitlement (volume + type) — the outcome of the V&V process |
| `Validation` | Tracks a validation event (status, period, entitlement, property) |
| `Forestation` | SFRA (stream flow reduction activity) data: qualifying period hectares, current hectares, lawful/unlawful volumes, Pre1972, SFRA permit |
| `Irrigation` | Water taking data: volume, land area, crop area, water date, water source |
| `FieldAndCrop` | Per-field crop data: field area, crop type, plant date, rotation factor, irrigation system, water source, crop area, SAPWAT result (mm/ha/a) |
| `DamCalculation` | Dam storage capacity calculation record |
| `Storing` | Water storage record |
| `PropertyOwner` + `PropertyOwnership` | Many-to-many between owners and properties via title deed number and date |
| `ApplicationUser` | Internal DWS/CMA staff user, extends `IdentityUser`; tracks employee number |
| `Address` | Street address with Province and optional lat/lon |
| `SateliteImage` | Raw satellite image metadata used for GIS analysis — uploaded into the system separately |
| `Mapbook` | Processed GIS map product per FileMaster; references source `SateliteImage` records via `MapbookImage` join table. The V&V file artefact (items 7 and 9). |
| `MapbookImage` | Join table linking a `Mapbook` to the `SateliteImage` records used to produce it |
| `CatchmentArea` | Quaternary catchment (e.g. "A21A") with FK to `WaterManagementArea`. Part of the organisational hierarchy. |
| `GovernmentWaterControlArea` | A proclaimed GWCA with gazette reference, proclamation date, and linked `GwcaProclamationRule` records |
| `GwcaProclamationRule` | Configurable per-GWCA legal limits (e.g. MAX_HECTARES, MAX_VOLUME_PER_HA) seeded from gazette proclamations |
| `River` | River name lookup used for water source identification |

## Domain Knowledge (from DWS Requirements Document & Legal Principles Presentation)

### Legislative Framework — Sections 32–35 of the NWA

The NWA gave recognition of water uses authorised by previous legislation as Existing Lawful Water Use (ELU). The key provisions are:

- **Section 32** — Defines ELU as water use lawfully undertaken during a two-year period immediately before the NWA commencement date, authorised by or under any prior law, including stream flow reduction activities (S36(1)) and controlled activities (S37(1)), **or** declared as ELU under Section 33.
- **Section 33** — Allows the Minister to **declare** certain water uses as ELU. This has two distinct regimes:
  - **S33(2) — Kader Asmal Declaration (Scheduled Areas)**: All scheduled areas under government and irrigation board schemes as of 1 January 1999, for which prescribed rates were paid up to 30 September 1998, are ELU. **Dormant but paid-for volumes are considered ELU.** Users who had not used water were given 3 months to exercise the scheduled water use. System must track irrigation board membership and rates-paid-up-to date.
  - **S33(3) — Declaration on Individual Application**: Two sub-categories where individuals apply for their water use to be declared as ELU. System must track application date and sub-type.
- **Section 34** — Provides authority to continue with ELU under the same conditions until replacement by a licence, subject to limitations under the Act.
- **Section 35** — Outlines provisions for the responsible authority to require verification of ELU. The S35 process includes statutory notice requirements, application forms, due dates, and the right to make representations (aligned with PAJA).

### Legal Principles — Two-Tier Framework

The lawfulness assessment operates at two levels. The system must apply specific GWCA rules where applicable, falling back to general principles otherwise.

#### General Legal Principles (apply to all properties outside proclaimed areas)

**Abstraction — S21(a):**
- **Riparian Rights (Surplus Flow)**: Every riparian owner could use as much surplus water on riparian land as could be beneficially used, subject to agreements.
- **Riparian Rights (Normal Flow)**: Fair share of normal flow of a public stream, as determined by agreement or Water Court order.
- **Private Water (S5 of old Water Act)**: Water rising or falling naturally on land, for sole use of landowner. Could be controlled in a GWCA.
- **Section 9B Abstraction Limits**: From 1975 — max 110 litres/second without a permit (on land registered in deeds registry in 1975). From 1987 — same limit on subdivisions. Can be varied by S9B(1C) in a defined area.
- **Subdivisions**: Riparian land remains riparian after subdivision. Share determined by agreement or Water Court.

**Storage — S21(b):**
- **Section 9B Storage Limits**: From 1975 — max 250,000 m³ without a permit. From 1987 — same limit on subdivisions. Can be varied by S9B(1C).
- **Protected Areas (S15/S16)**: Max 114,000 m³ surplus water, max 300 l/s surplus flow per property.

**Stream Flow Reduction Activities — S21(d):**
- **Pre-1972**: No authorisation required. Genus planted pre-1972 and still existing during qualifying period = ELU.
- **Post-1984 (Forestry Act 122 of 1984)**: Permit required. Extent of permit, if established during qualifying period = ELU.
- **Homelands (pre-1994)**: Authorised by virtue of PTOs. Genus planted pre-1994 and still existing during qualifying period = ELU.

**Types of Authorisations (what constitutes proof of lawfulness):**
Riparian Right, Private Water, Permit, Servitude, Title Deed (with water right notes), Sales Agreement, Court Order, Proclamation (GWCA surface / GWCA subterranean), Farmer Agreement, General Authorisation.

#### Specific Legal Principles — Government Water Control Areas (GWCAs)

Each proclaimed GWCA has **unique rules** set by Government Notice that **override general principles** for properties within that area. These are modelled as configurable `GwcaProclamationRule` records per GWCA. Examples from the Blyde River GWCA:

| Rule | Limit | Source |
|------|-------|--------|
| Max irrigable hectares | 30 ha OR 40% of irrigable land (whichever is greater) | GN 180 of 10 July 1970 |
| Existing lawful irrigation > minimum | Max 53% of available irrigable land | GN 180 of 10 July 1970 |
| Max abstraction rate | 9,900 m³/ha | Proclamation |
| Max storage capacity per hectare | 5,000 m³/ha | GN 2805 of 14 December 1979 |
| Max storage capacity per property | 50,000 m³ | GN 2805 of 14 December 1979 |

The `LawfulnessAssessmentService` checks if a property falls within a GWCA (via `GovernmentWaterControlArea` link), loads active rules, and evaluates volumes/areas against limits before falling through to general principles.

### The V&V Process — Three Phases, 9 Control Points

**Phase 1: Inception** — Project setup, data acquisition, public participation
1. Obtain latest WARMS database & perform V&V Database Audit (new records, ownership changes)
2. Identify Unregistered Users via GIS analysis on current satellite imagery
3. V&V Database Analysis to identify Record Status (completed, in-process, Section 35 status)
4. Project Inception Report
5. Public Participation Sessions 1 (WMA-level launch)

**Phase 2: Validation** — Technical: determine if water use EXISTED during qualifying period and ESTIMATE EXTENT
6. Obtain Supportive Spatial Information (SG farm boundaries, catchment boundaries, rivers, satellite imagery 1996–1999 + current)
7. Evaluate WARMS Registration Information (volumes, crops, hectares, irrigation methods)
8. Obtain Supportive Information (Field Surveys, GWCA, Irrigation Board, Section 9B, GWS, riparian farm status)
9. Property & Source Evaluation (Title Deeds, SG Diagrams, WARMS comparison)
→ Evaluate Additional Information (Permits, S32/33 approvals, transfers, licenses, GA)
→ GIS Analysis & Field Digitising (digitise crop fields and dams for qualifying and current periods)
→ Complete Field and Crop + SAPWAT Modelling

**Phase 3: Verification** — Legal: determine if water use was LAWFUL by checking the authorising instrument
→ Calculate ELU (Lawful / Unlawful / Possible Unlawful Increase-Decrease) — applying two-tier legal framework
→ Calculate Dam Volumes
→ Calculate Stream Flow Reduction (Forestation/SFRA)
→ Update V&V Database
→ **SECTION 35 PROCESS**: issue letters, receive responses, confirm ELU
→ **SECTION 33 DECLARATIONS**: issue S33(2) or S33(3) declaration letters where applicable

**Key distinction**: Validation = "Did the use exist and how much?" (technical, GIS-based). Verification = "Was it lawful?" (legal, instrument-based).

### The Qualifying Period

The qualifying period is **1 October 1996 – 30 September 1998** for **both** ground water and surface water, per the SFRA court order direction. This is a single unified period — there is no distinction between surface and borehole qualifying dates.

Water use exercised during this period may qualify as ELU. All satellite imagery and evidence must originate from or relate to this window.

### Water Use Categories per Property (Irrigation checklist, maps to `FieldAndCrop`/`Irrigation` data)

| Row | Category | Period |
|-----|----------|--------|
| 4.1 | Proclamation Date / Previous Surveys | Historical |
| 4.2 | Possible Existing | Qualifying period: 1 Oct 1996 – 30 Sep 1998 |
| 4.3 | Possible Lawful | GWCA(S), GWCA(U), Permit, Irrigation Board, GWS, Other |
| 4.4 | Possible Existing Lawful | Sum of lawful qualifying use |
| 4.5 | Possible Unlawful | Use without authorisation |
| 4.6 | Registration (WARMS) | What is registered in WARMS |
| 4.7 | Present day use | Current period satellite analysis |
| 4.8 | Possible Unlawful Increase/Decrease | Delta between current and ELU |
| 4.9 | General Authorisation | GA-covered use |

### V&V File Content (Appendix A — what each property file must contain)

1. Checklist
2. WARMS report
3. Property Investigation Title deed report
4. Property investigation Surveyor General (SG) Diagram/s
5. Previous applicable study and legislative documentation
6. Field and Crop summary table
7. Qualifying period **Mapbook** (processed GIS map product with references to source satellite images used)
8. Qualifying period water volume calculations
9. Current period **Mapbook** (processed GIS map product with references to source satellite images used)
10. Current period water volume usage calculations
11. Water Storage calculations (map diagram + volume model results)
12. Section 35 / Section 33 letters (all correspondence)

**Note on satellite images vs Mapbooks**: Raw satellite images are uploaded into the system separately as `SateliteImage` records. The V&V file artefact is a **processed Mapbook** (`Mapbook` entity) that references the source images via `MapbookImage` join table. Workflow guards at CP5 check for `Mapbook` presence, not raw `SateliteImage`.

### Letter Types (what `LetterService` must generate)

#### Section 35 Letters (Verification Process)

| Letter | Section | Purpose |
|--------|---------|---------|
| Letter 1 | S35(1) | Notice to apply for verification |
| Letter 1A | S53(1) | Directive to apply for verification (non-responsive users) |
| Letter 2 | S35(3)(a) | Request for additional information |
| Letter 2A | S35(1) | Directive to provide additional information |
| Letter 3 | S35(4) | Confirmation of extent and lawfulness of water use (ELU certificate) |
| Letter 4A | S53(1) | Notice of intent to issue directive to stop unlawful use |
| Letter 4 & 5 | S53(1) | Directive to stop unlawful water use |

Letter 1 **must be served in person** (hand delivery by official, agent, or sheriff) per S35(2)(d). Letter delivery status and dates must be tracked per file.

#### Section 33 Letters (Declaration Process)

| Letter | Section | Purpose |
|--------|---------|---------|
| S33(2) Declaration | S33(2) | Kader Asmal Declaration — confirms ELU for irrigation board scheduled area members |
| S33(3)(a) Declaration | S33(3)(a) | Declaration of ELU on individual application — category A |
| S33(3)(b) Declaration | S33(3)(b) | Declaration of ELU on individual application — category B |

S33(2) cases follow a shorter assessment track — ELU is established by declaration, so the full SAPWAT/digitising pipeline may be skipped. The `WorkflowEngine` uses an `AssessmentTrack` on `FileMaster` to determine which control points are mandatory vs skipped for declaration-track cases.

#### Administrative Components (from presentation)

- S35(2) notice format and method of dispatch must comply with statutory requirements (personal delivery or registered mail).
- S35(3)(c) — invitation of written comments from persons with interest (aligned with PAJA).
- S35(3)(d) — affording the applicant an opportunity to make representations (aligned with PAJA).

### Lookup Values (Appendix B — seed data for reference tables)

**Validation Status** (for `FileMaster.GetValidationStatus`, distinct from process workflow stages):
`Not Commenced`, `In Process`, `Compl require client interaction`, `Completed`, `Compl Not Client Interaction`, `Compl Post Interact Processing`, `Q. Outside`, `Consolidated`

**WARMS Registration Index** (used when linking WARMS records):
`0`=WARMS Registration (Active), `1`=Unregistered Water Uses, `2`=Other Water User (Industrial), `3`=Water Scheme, `4`=Schedule 1 Use, `5`=Cancelled–No water use, `6`=Cancelled–Duplicate, `7`=Closed–New Owner, `8`=Cancelled–Property consolidated, `9`=License, `10`=Temporary Transfer permit, `33`=Section 33 applicant, `35`=Section 35 applicant

**Registration Status**: `Correctly registered`, `Under registered`, `Over registered`, `Unregistered`, `Unregistered WC`, `In Process`, `Q. outside`, `Consolidated`, `Watering Livestock`

**Additional Information Types** (for Authorisation records — `AuthorisationType`):
Permit, Section 32/33 Approval, Transfer (Temporary/Permanent), Ad-hoc Field Survey, Water Court, Other Water Act, License, General Authorisation, Other

### Dam Volume Calculation Formulas (Appendix D — maps to `DamCalculation`)

**Method 1 (Wall Length)**:
```
Slope = River Distance (R1) / Contour Difference (C1)
Depth = Fetch / Slope
Capacity (m³) = Wall Length × Fetch × Depth × Factor / 2
```

**Method 2 (Area)**:
```
Capacity (m³) = Area (ha) × Depth (m) × Factor × 1000
```

**Shape Factors**: Triangle (ravine) = 0.33, More square with bends = 0.4, More circular = 0.5

### SAPWAT

SAPWAT is the model used to estimate crop water requirements (output: mm/ha/a, stored as `SAPWATCalculationResult` in `FieldAndCrop`). It uses crop type, irrigation system, and crop area as inputs. SAPWAT estimates must be calibrated per catchment against the average annual water requirement for each crop type.

## Conventions

### EF Core Model Configuration
- All primary keys are `Guid`, configured in `ApplicationDBContext.OnModelCreating()` — do **not** use `[Key]` annotations.
- Decimal columns use `[Column(TypeName = "decimal(18, 2)")]` for volumes/areas and `decimal(9, 6)` for geo-coordinates (latitude/longitude).
- Add every new entity to `ApplicationDBContext` with a `DbSet<T>` and a `HasKey()` call in `OnModelCreating`.

### Interface Naming
- Repository interfaces live in `Interfaces/` and follow `IXxx` or `IXxxInterface` (e.g. `IPropertyInterface`, `IAddress`, `IForestation`).
- **Known issue**: `Interfaces/IEntitlement.cs` defines `public interface Entitlement` — this conflicts with `Models/Entitlement.cs`. It must be renamed to `public interface IEntitlement`.

### Async Patterns
- Prefer `async`/`await` with `SaveChangesAsync()` for all new repository methods. Some existing repos (e.g. `PropertyRepository`) use synchronous `SaveChanges()` — this is legacy.

## Planned Components (from `docs/DATA-FLOW-AND-WORKFLOW-DESIGN.md`)

| Component | Description |
|-----------|-------------|
| `WorkflowEngine` | State machine driving V&V process control point transitions |
| `CalculatorEngine` | SAPWAT, dam volume (Appendix D formulas), and SFRA stream flow reduction |
| `LetterService` | Section 35 and Section 33 PDF letter generation using QuestPDF; tracks signed/due dates |
| `LawfulnessAssessmentService` | Two-tier legal framework evaluation: checks GWCA rules first, then general principles. Takes a `FileMaster`, determines applicable legal framework, evaluates volumes/areas against limits |
| `NotificationService` | Email + in-app notifications with due date reminders |
| `SignatureService` | Electronic signature capture (SignaturePad.js) and X.509 server-side verification |
| `AuditService` | Immutable audit trail of all V&V actions |
| `IntegrationService` | **WARMS integration is one-way inbound only** — we receive a case/registration number from WARMS but do not write back to it. V&V output (ELU determinations) is pushed to **eWULAAS** instead. Both WARMS and eWULAAS are C#/ASP.NET/SQL Server systems. (Amendment per DWS meeting, March 2026) |
| Public Portal | Separate Razor Pages/Blazor area for water users to track cases and sign letters |

## Organisational Hierarchy

DWS operates a national-to-regional hierarchy that maps directly onto the case management access model:

```
National DWS (Head Office — Pretoria)
  └─ 9 Provincial DWS Offices
       └─ Regional Offices / Water Management Areas (9 WMAs, 2012 redetermination)
            └─ Catchment Areas (Quaternary Catchments, ~1,946 nationally, e.g. "A21A", "B31A")
                 └─ CMAs (Catchment Management Agencies, where established)
                      └─ Service Providers / Consultants (contracted field work)
```

A `FileMaster` case belongs to a **WMA/Regional Office** and a **Catchment Area**, and access to it is scoped by organisational unit. When a user is assigned to a province, WMA, or catchment area, they can only see and operate on cases in that scope, unless they hold a national-level role.

Key entities:
- `Province` (9 provinces)
- `WaterManagementArea` (9 WMAs per 2012 redetermination, FK from `Property`)
- `CatchmentArea` — quaternary catchment code (e.g. "A21A") with FK to `WaterManagementArea`. Replaces the old `Property.QuaternaryDrainage` and `FileMaster.QuaternaryCatchment` string fields with a proper FK relationship.
- `OrganisationalUnit` — an office (National, Provincial, Regional, CMA, **Catchment**) with province + WMA + optional catchment scope
- `ApplicationUser.OrganisationalUnitId` — which office this DWS staff member belongs to

## Internal Role / Privilege Levels

Roles map to what actions a user may perform on a FileMaster case:

| Role | Description | Key Permissions |
|------|-------------|-----------------|
| `SystemAdmin` | IT/system administrator | Manage all users, reference data, system config |
| `NationalManager` | National DWS manager | View all cases across all provinces; approve escalations |
| `RegionalManager` | Provincial/regional office head | Approve control point transitions in their WMA; countersign Section 35 letters |
| `Validator` | V&V practitioner (DWS official or delegated consultant) | Create `FileMaster` cases; capture all field data; initiate letter issuance |
| `Capturer` | Data entry clerk | Capture and update field data only; cannot transition control points or issue letters |
| `ReadOnly` | Reviewer / auditor | View case data, no modifications |

**Key authorisation rules**:
- Only `Validator`+ may create a new `FileMaster` case or transition a control point.
- Only `RegionalManager`+ may approve/sign Section 35 letters (signatory delegation per NWA).
- Letter 1 (S35(1)) must be served in person — the system must record the serving official.
- `Capturer` may update field data on a case already created by a `Validator`.
- Role scope is always filtered by `OrganisationalUnit` — no cross-office access below `NationalManager`.

## State Machine — V&V Workflow

The `WorkflowEngine` is the core of the system. It enforces that a `FileMaster` case advances through control points in order, with guards and role checks at each transition. The state is stored on `FileMaster` as a `WorkflowState` (enum → DB table).

### Phase 1: Inception — Control Point 1 (has internal sequential sub-steps)

CP1 is the only control point with mandatory internal sub-steps that must complete in order before the case can advance to CP2:

| Step | Activity | Guard / Evidence Required |
|------|----------|---------------------------|
| 1.1 | WARMS data obtained and imported | WARMS export date recorded |
| 1.2 | Satellite imagery obtained (qualifying period + current) | `SateliteImage` records present |
| 1.3 | Database audit complete | New records identified, ownership changes flagged |
| 1.4 | Unregistered users identified (GIS analysis on current imagery) | Count of unregistered users captured |
| 1.5 | Database analysis complete (record status per Appendix B assigned) | All records have `ValidationStatus` |
| 1.6 | Inception Report drafted and approved | Document upload confirmed |
| 1.7 | Public Participation Session 1 complete | Session date, venue, attendance count captured |

### Phase 2: Validation — Control Points 2–6 (Technical: Existence & Extent)

| CP | Activity | Key Guard |
|----|----------|-----------|
| CP2 | Supporting spatial information obtained (SG boundaries, catchments, rivers) | `River` + `Property` SG codes confirmed |
| CP3 | WARMS evaluation complete (volumes, crops, hectares, irrigation methods reviewed) | WARMS comparison recorded on `FileMaster` |
| CP4 | Additional information obtained (permits, S32/33 approvals, transfers, GA) | `Authorisation` records captured |
| CP5 | GIS analysis + field digitising complete; **Mapbooks** produced for qualifying + current periods | `Mapbook` records present (not raw `SateliteImage`) |
| CP6 | Field, Crop + SAPWAT complete | `FieldAndCrop` records with `SAPWATCalculationResult` present |

### Phase 3: Verification — Control Points 7–9 (Legal: Lawfulness Determination)

| CP | Activity | Key Guard |
|----|----------|-----------|
| CP7 | ELU calculated (Lawful / Unlawful / Possible Unlawful increase/decrease) — applies two-tier legal framework via `LawfulnessAssessmentService` | `Validation` record with ELU volumes set |
| CP8 | Dam volumes calculated (if applicable) | `DamCalculation` present, or explicitly marked N/A |
| CP9 | SFRA / Forestation calculated (if applicable) | `Forestation` present, or explicitly marked N/A |

### Section 35 / Section 33 Letter State Machine (post-CP9)

After CP9 the case enters the **Verification Letter** sub-process. The path depends on the `AssessmentTrack` on the `FileMaster`:

**Track A: Section 35 Verification** (standard V&V track — most cases):
```
[CP9 Complete]
    → Letter1Issued  (S35(1) — served in person per S35(2)(d))
        → Letter1Responded   (response received within statutory period)
        → Letter1ARequired   (no response — issue S53(1) directive)
            → Letter1AIssued
                → Letter1AResponded
    → AdditionalInfoRequired
        → Letter2Issued      (S35(3)(a) — request additional info)
            → Letter2Responded
            → Letter2ARequired  (S35(1) directive for non-responsive)
                → Letter2AIssued
    → ELUConfirmed
        → Letter3Issued      (S35(4) ELU certificate — lawful use confirmed)
        → UnlawfulUseFound
            → Letter4AIssued  (S53(1) — notice of intent to stop unlawful use)
                → Letter4And5Issued  (S53(1) directive to stop)
    → [Closed / Archived]
```

**Track B: Section 33(2) Declaration** (Kader Asmal — scheduled areas under irrigation boards):
```
[CP9 Complete or Shortened Track]
    → S33_2_DeclarationIssued  (confirms ELU for scheduled area)
    → [Closed / Archived]
```

**Track C: Section 33(3) Declaration** (individual application):
```
[CP9 Complete]
    → S33_3_DeclarationIssued  (declares ELU on individual application)
    → [Closed / Archived]
```

Each letter transition must record: `IssuedDate`, `IssuedByUserId`, `SignedByUserId`, `DeliveryMethod`, `PostalRegistrationNumber` (if registered post), `DueDate`, `ResponseReceivedDate`.

### WorkflowEngine Design Notes

- `FileMaster.WorkflowStateId` FK → `WorkflowState` table (seeded with all state names above).
- Every state transition is written to an immutable `AuditEntry` table (who, what, when, previous state, new state).
- The engine exposes `CanTransition(user, fileMaster, targetState)` — returns `bool` + reason string.
- Guards are defined per transition as a list of `ITransitionGuard` implementations (checked in order).
- Notifications are triggered as domain events on successful transitions.

## External User Portal (Separate Security Realm)

External stakeholders (water users, landowners, legal representatives) access a **separate portal** with its own identity regime, isolated from the internal DWS staff system.

### Identity and Security

- `ExternalUser` is a **separate entity** — does **not** inherit from `ApplicationUser` or `IdentityUser`.
- External users **self-register** (name, email, ID number, contact details) and verify via email confirmation link.
- **MFA is mandatory** (TOTP authenticator app or SMS OTP) before accessing any case data.
- External sessions have a shorter timeout than internal sessions.
- The external portal runs under a **separate authentication cookie scheme** (e.g. `ExternalScheme`) so internal and external sessions cannot overlap.

### Data Isolation (Row-Level Security)

- Each `ExternalUser` is linked to one or more `Property` records via `ExternalUserProperty` join table (established during registration, verified by a DWS official).
- All data queries in the external portal are **always scoped by `ExternalUserId`** — an external user can never retrieve records for a property not linked to their account.
- External users **cannot see other users' cases, comments, or documents**.

### External Portal Capabilities

| Feature | Description |
|---------|-------------|
| Case Status View | Read-only view of their `FileMaster` case status, current workflow state, and history |
| Notifications | Email notification when case state changes (letter issued, ELU confirmed, etc.); in-app notification bell |
| Document Upload | Upload supporting documents (title deeds, permits, field survey evidence) attached to their case |
| Comments / Responses | Submit a written response to a Section 35 letter or provide additional information |
| Protest Lodging | Lodge a formal protest against an ELU determination; a `Protest` record is created |
| Protest Documents | Upload documents supporting their protest (affidavits, legal opinions, counter-evidence) |

### Key External Portal Entities

| Entity | Purpose |
|--------|---------|
| `ExternalUser` | Self-registered water user; holds MFA credentials separate from `ApplicationUser` |
| `ExternalUserProperty` | Join table linking external users to their `Property` records (must be approved by DWS) |
| `CaseNotification` | Per-case notification record (type, message, sent date, read date, `ExternalUserId`) |
| `CaseComment` | Comment or letter response from an external user (FK to `FileMaster`, timestamp, read by DWS flag) |
| `UploadedDocument` | File attachment on a case (FK to `FileMaster`, uploader, upload date, document type, blob path) |
| `Protest` | Formal protest record (FK to `FileMaster`, `ExternalUserId`, lodged date, status, resolution notes) |
| `ProtestDocument` | Documents supporting a protest (FK to `Protest`, FK to `UploadedDocument`) |

### Implementation Notes

- File storage: use Azure Blob Storage or filesystem path — store only the path/URL in `UploadedDocument.BlobPath`.
- Virus scanning on all uploads before making them accessible.
- All external-facing controllers live in a separate MVC Area (`/Areas/ExternalPortal/`).
- DWS staff can view (but not modify) external comments and uploads from within the internal portal.
- A DWS `Validator`+ must approve the `ExternalUserProperty` link before the external user can see any case data.

### Feedback from sessions with client

- The WARMS system integration will now not be necessary as the V&V system will receive all the required data from eWULAAS
- The Workflow state machine must reflect water user inputs in cases where the DWS officials do not have all the required information. These may be facilitated through the portal and other communication means across all the control points.
- A V&V case number will be genrated on some business rule still to be furnished
- When a property is sub divided or more than one properties are consolidated, the old property is maintained with its attributes and new properrties must be created for the new property attributes with some attributes inherited from the parent one. In cases of consolidation we create one new property and the previous ones are maintained with a status. So we need to create property status - CONSOLIDATED, SUBDIVIDED on the old records. 
- They prefer we use the words Appeal or Objection instead of Protest where if the V&V Finalised.
- May we also have an HDI - historically disadvantaged individual - indicator in the User Profile. This just means either Black people and women in South Africa.
- Section 33 must be properly categorised: S33(2) is the Kader Asmal Declaration for scheduled areas under irrigation boards; S33(3) is declaration on individual application with two sub-types. Each has distinct letter types and assessment tracks.
- The qualifying period is 1 October 1996 – 30 September 1998 for BOTH ground and surface water per the SFRA court order. No distinction between surface and borehole.
- Legal framework must be structured as: General Legal Principles, then Specific Legal Principles (GWCAs). Refer to John Malungani's "Application of Legal Principles" presentation (November 2025) saved at `docs/System-Development-Presentation-Text.md`.
- The process has three phases: Inception, then Validation (technical extent), then Verification (legal lawfulness). Not two phases.
- V&V files should contain processed Mapbooks with image references, not raw satellite images. Raw images are uploaded separately.
- The organisational hierarchy must explicitly include Catchment Area (Quaternary Catchment) between WMA and property level.

