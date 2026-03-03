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
| `SateliteImage` | Satellite image metadata used for GIS analysis |
| `River` | River name lookup used for water source identification |

## Domain Knowledge (from DWS Requirements Document)

### The V&V Process — Two Phases, 9 Control Points

**Phase 1: Project Inception**
1. Obtain latest WARMS database & perform V&V Database Audit (new records, ownership changes)
2. Identify Unregistered Users via GIS analysis on current satellite imagery
3. V&V Database Analysis to identify Record Status (completed, in-process, Section 35 status)
4. Project Inception Report
5. Public Participation Sessions 1 (WMA-level launch)

**Phase 2: Study Implementation**
6. Evaluate WARMS Registration Information (volumes, crops, hectares, irrigation methods)
7. Obtain Supportive Spatial Information (SG farm boundaries, catchment boundaries, rivers, satellite imagery 1996–1999 + current)
8. Obtain Supportive Information (Field Surveys, GWCA, Irrigation Board, Section 9B, GWS, riparian farm status)
9. Property & Source Evaluation (Title Deeds, SG Diagrams, WARMS comparison)
→ Evaluate Additional Information (Permits, S32/33 approvals, transfers, licenses, GA)
→ GIS Analysis & Field Digitising (digitise crop fields and dams for qualifying and current periods)
→ Complete Field and Crop + SAPWAT Modelling
→ Calculate ELU (Lawful / Unlawful / Possible Unlawful Increase-Decrease)
→ Update V&V Database
→ Calculate Dam Volumes
→ Calculate Stream Flow Reduction (Forestation/SFRA)
→ **VALIDATION PROCESS** (Section 35): issue letters, receive responses, confirm ELU

### The Qualifying Period

The "qualifying period" is the 2 years before the NWA commencement date of **1 October 1998**:
- **Surface water**: 1 October 1997 – 1 October 1999
- **Borehole**: 1 October 1996 – 1 October 1998

Water use exercised during this period may qualify as ELU. All satellite imagery and evidence must originate from or relate to this window.

### Water Use Categories per Property (Irrigation checklist, maps to `FieldAndCrop`/`Irrigation` data)

| Row | Category | Period |
|-----|----------|--------|
| 4.1 | Proclamation Date / Previous Surveys | Historical |
| 4.2 | Possible Existing | Qualifying period (surface: 1 Oct 97–99, borehole: 1 Oct 96–98) |
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
7. Qualify GIS Map
8. Qualify period water volume calculations
9. Current GIS Map
10. Current period water volume usage calculations
11. Water Storage calculations (map diagram + volume model results)
12. Section 35 letters (all correspondence)

### Section 35 Letter Types (what `LetterService` must generate)

| Letter | Section | Purpose |
|--------|---------|---------|
| Letter 1 | S35(1) | Notice to apply for verification |
| Letter 1A | S53(1) | Directive to apply for verification (non-responsive users) |
| Letter 2 | S35(3)(a) | Request for additional information |
| Letter 2A | S35(1) | Directive to provide additional information |
| Letter 3 | S35(4) | Confirmation of extent and lawfulness of water use (ELU certificate) |
| Letter 4A | S53(1) | Notice of intent to issue directive to stop unlawful use |
| Letter 4 & 5 | S53(1) | Directive to stop unlawful water use |

Letter 1 **must be served in person** (hand delivery by official, agent, or sheriff). Letter delivery status and dates must be tracked per file.

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
| `LetterService` | Section 35 PDF letter generation using QuestPDF; tracks signed/due dates |
| `NotificationService` | Email + in-app notifications with due date reminders |
| `SignatureService` | Electronic signature capture (SignaturePad.js) and X.509 server-side verification |
| `AuditService` | Immutable audit trail of all V&V actions |
| `IntegrationService` | WARMS and eWULAAS data sync (both on C#/ASP.NET/SQL Server) |
| Public Portal | Separate Razor Pages/Blazor area for water users to track cases and sign letters |
