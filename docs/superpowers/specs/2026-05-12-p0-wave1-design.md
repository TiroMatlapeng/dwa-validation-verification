# P0 Wave 1 — Design Spec

**Date:** 2026-05-12  
**Branch:** demo/azure-deploy  
**Status:** Approved by user  

## Pre-Implementation Discovery

Self-review against the live codebase revealed that three of the originally identified P0 items are already built:

| Item | Finding |
|------|---------|
| WorkflowController (originally P0 item 1) | `FileMasterController` already contains `AdvanceWorkflow`, `IssueLetter`, `MarkLetterResponse`, `LetterPreview`, `LetterPdf` — all fully implemented |
| Property subdivision/consolidation (originally P0 item 7) | `Property.PropertyStatus` and `ParentPropertyId` exist; `PropertyController` has `Subdivide` and `Consolidate` (GET/POST) with full audit logic; `Subdivide.cshtml` and `Consolidate.cshtml` views exist |
| FileMasterController CRUD completion (originally P0 item 8) | All views exist (`Index`, `Create`, `Edit`, `Details`, `Delete`, `_WorkflowPanel`, `_LettersPanel`) and are populated with the DWS brand style |

**Remaining P0 scope — 3 parallel workstreams:**

1. Field & Crop views (data capture + CRUD)
2. Forestation views (data capture + CRUD)
3. Dam Calculation views (data capture + CRUD with formula computation)

CalculatorEngine (item 5) and LawfulnessAssessmentService (item 6) remain deferred to wave 2, pending a separate design session.

---

## 1. Field & Crop Views (P0 Item 2)

### Goal
CRUD UI for `FieldAndCrop` records scoped to a `FileMaster`. Manual `SAPWATCalculationResult` entry for now; CalculatorEngine (wave 2) will automate this.

### Architecture
- New `Controllers/FieldAndCropController.cs`
- Check `IFieldAndCrop` interface for full CRUD coverage; extend `Interfaces/IFieldAndCrop.cs` and `Repositories/FieldAndCropRepository.cs` if Create/Update/Delete are missing (verify first)
- New `ViewModels/FieldAndCropViewModel.cs`
- New views under `Views/FieldAndCrop/`
- Add "Field & Crop" section to `Views/FileMaster/Details.cshtml` with a summary count and link to the Index

### Controller Actions

| Action | Method | Description |
|--------|--------|-------------|
| `Index(Guid fileMasterId)` | GET | List all FieldAndCrop rows for a FileMaster |
| `Create(Guid fileMasterId)` | GET | Blank form pre-scoped to FileMaster |
| `Create(FieldAndCropViewModel vm)` | POST | Validate and save |
| `Edit(Guid id)` | GET | Pre-filled form |
| `Edit(FieldAndCropViewModel vm)` | POST | Validate and update |
| `Delete(Guid id)` | POST | Remove row; redirect to Index for same FileMaster |

Authorization: `[Authorize(Policy = DwsPolicies.CanTransitionWorkflow)]` (Validator+).

### ViewModel Fields

| Field | Type | Input | Notes |
|-------|------|-------|-------|
| `FileMasterId` | Guid | Hidden | Scoping FK |
| `FieldArea` | decimal | Number | Hectares |
| `CropTypeId` | Guid | Dropdown | From seeded `CropType` table |
| `PlantDate` | DateOnly | Date picker | |
| `RotationFactor` | decimal | Number | 0–1 |
| `IrrigationSystemId` | Guid | Dropdown | From seeded `IrrigationSystem` table |
| `WaterSourceId` | Guid | Dropdown | From seeded `WaterSource` table |
| `CropArea` | decimal | Number | Hectares |
| `SAPWATCalculationResult` | decimal | Number | mm/ha/a — manual until CalculatorEngine |

### Views
- `Views/FieldAndCrop/Index.cshtml` — table of rows with Edit/Delete; "Add Field & Crop Record" button at top
- `Views/FieldAndCrop/Create.cshtml` — form (uses DWS card + form-row style matching existing views)
- `Views/FieldAndCrop/Edit.cshtml` — pre-filled form (same layout as Create)
- Breadcrumb back to `FileMaster/Details/{fileMasterId}` on all views

### Files
- **Create:** `Controllers/FieldAndCropController.cs`, `ViewModels/FieldAndCropViewModel.cs`, `Views/FieldAndCrop/Index.cshtml`, `Views/FieldAndCrop/Create.cshtml`, `Views/FieldAndCrop/Edit.cshtml`
- **Modify:** `Views/FileMaster/Details.cshtml` — add Field & Crop section
- **Modify if needed:** `Interfaces/IFieldAndCrop.cs`, `Repositories/FieldAndCropRepository.cs`, `Program.cs`

---

## 2. Forestation Views (P0 Item 3)

### Goal
CRUD UI for `Forestation` records scoped to a `FileMaster`. The repository (`ForestationRepository`) and interface (`IForestation`) already exist and are registered in DI.

### Architecture
- New `Controllers/ForestationController.cs`
- Check `IForestation` for Create/Update/Delete; extend if missing
- New `ViewModels/ForestationViewModel.cs`
- New views under `Views/Forestation/`
- Add "Forestation / SFRA" section to `Views/FileMaster/Details.cshtml`

### Controller Actions
Same pattern as FieldAndCropController: `Index(fileMasterId)`, `Create(GET/POST)`, `Edit(GET/POST)`, `Delete(POST)`.

Authorization: `[Authorize(Policy = DwsPolicies.CanTransitionWorkflow)]`.

### ViewModel Fields

| Field | Type | Input | Notes |
|-------|------|-------|-------|
| `FileMasterId` | Guid | Hidden | Scoping FK |
| `QualifyingPeriodHectares` | decimal | Number | Extent 1 Oct 1996 – 30 Sep 1998 |
| `CurrentHectares` | decimal | Number | Present-day extent |
| `LawfulVolume` | decimal | Number | m³/a — authorised SFRA volume |
| `UnlawfulVolume` | decimal | Number | m³/a — unauthorised volume |
| `Pre1972` | bool | Checkbox | If true: no permit required |
| `SFRAPermit` | string | Text | Permit reference (Post-1984 Forestry Act) |
| `Genus` | string | Text | Tree species genus |

### Views
- `Views/Forestation/Index.cshtml` — table + "Add Forestation Record" button
- `Views/Forestation/Create.cshtml` — form; `Pre1972` checkbox toggles `SFRAPermit` visibility (JS)
- `Views/Forestation/Edit.cshtml` — pre-filled form

### Files
- **Create:** `Controllers/ForestationController.cs`, `ViewModels/ForestationViewModel.cs`, `Views/Forestation/Index.cshtml`, `Views/Forestation/Create.cshtml`, `Views/Forestation/Edit.cshtml`
- **Modify:** `Views/FileMaster/Details.cshtml` — add Forestation section
- **Modify if needed:** `Interfaces/IForestation.cs`, `Repositories/ForestationRepository.cs`

---

## 3. Dam Calculation Views (P0 Item 4)

### Goal
CRUD UI for `DamCalculation` records implementing both Appendix D formula methods. Calculated capacity shown as a live read-only result derived from inputs (client-side JS + server-side recompute on POST).

### Architecture
- New `Controllers/DamCalculationController.cs`
- Check for existing `IDamCalculation` interface and repository; create if missing
- New `ViewModels/DamCalculationViewModel.cs`
- New views under `Views/DamCalculation/`
- Add "Dam Calculations" section to `Views/FileMaster/Details.cshtml`

### Calculation Methods (Appendix D)

**Method 1 — Wall Length:**
```
Slope            = RiverDistance / ContourDifference
Depth            = Fetch / Slope
Capacity (m³)    = WallLength × Fetch × Depth × ShapeFactor / 2
```

**Method 2 — Area:**
```
Capacity (m³)    = AreaHectares × DepthMetres × ShapeFactor × 1000
```

**Shape Factor dropdown:**
- Triangle / ravine = 0.33
- Square with bends = 0.40
- Circular = 0.50

### ViewModel Fields

| Field | Method | Type | Notes |
|-------|--------|------|-------|
| `FileMasterId` | Both | Guid (hidden) | Scoping FK |
| `CalculationMethod` | Both | Enum / radio | `Method1` or `Method2` |
| `ShapeFactor` | Both | Dropdown (decimal) | 0.33 / 0.40 / 0.50 |
| `WallLength` | Method 1 | decimal | metres |
| `RiverDistance` | Method 1 | decimal | metres (R1) |
| `ContourDifference` | Method 1 | decimal | metres (C1) |
| `Fetch` | Method 1 | decimal | metres |
| `AreaHectares` | Method 2 | decimal | ha |
| `DepthMetres` | Method 2 | decimal | m |
| `CalculatedCapacity` | Both | decimal (read-only) | m³ — computed; stored on POST |

### Client-Side Behavior
- JS toggles Method 1 / Method 2 input groups based on the radio selection.
- JS recomputes `CalculatedCapacity` on any input change and writes it to the read-only field.
- Server recomputes and stores `CalculatedCapacity` on POST (do not trust the client value).

### Views
- `Views/DamCalculation/Index.cshtml` — table with calculated capacity column + "Add Dam Calculation" button
- `Views/DamCalculation/Create.cshtml` — form with method toggle and live calculation display
- `Views/DamCalculation/Edit.cshtml` — pre-filled form

### Files
- **Create:** `Controllers/DamCalculationController.cs`, `ViewModels/DamCalculationViewModel.cs`, `Views/DamCalculation/Index.cshtml`, `Views/DamCalculation/Create.cshtml`, `Views/DamCalculation/Edit.cshtml`
- **Create if missing:** `Interfaces/IDamCalculation.cs`, `Repositories/DamCalculationRepository.cs`
- **Modify:** `Views/FileMaster/Details.cshtml` — add Dam Calculations section
- **Modify:** `Program.cs` — register `IDamCalculation`/`DamCalculationRepository`

---

## 4. Wave 2 — Deferred (separate design session required)

| Item | Why deferred |
|------|-------------|
| `CalculatorEngine` (P0 item 5) | Depends on Field & Crop, Dam Calculation, Forestation capture views being usable; complex formula logic |
| `LawfulnessAssessmentService` (P0 item 6) | Encodes two-tier GWCA/general-principles legal framework; needs dedicated design session |

---

## Cross-Cutting Concerns

- **Authorization:** `[Authorize(Policy = DwsPolicies.CanTransitionWorkflow)]` (maps to `ValidatorOrAbove`) on all new controllers, matching existing convention.
- **Routing:** Convention-based MVC routing (`{controller}/{action}/{id?}`).
- **DWS Brand Palette:** All new views use the existing CSS classes from `wwwroot/css/site.css` (`card`, `form-row`, `form-group`, `form-label`, `btn-primary`, `data-table-wrap`, etc.). No Tailwind or external color defaults.
- **Async:** All new repository methods use `async`/`await` with `SaveChangesAsync()`.
- **Migrations:** No new migrations expected — `FieldAndCrop`, `Forestation`, and `DamCalculation` models are already in the schema. Verify with `dotnet ef migrations list` before starting.
- **Tests:** Each new controller gets at least one xUnit integration test covering the happy-path GET and POST actions. Pre-existing 19 test failures must not grow.
- **Pre-implementation step:** dotnet-architect agents to review the existing codebase before implementation begins (per user request).
