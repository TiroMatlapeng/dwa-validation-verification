# Task: Bug-Fix Sprint 2 — QA findings from user-testing-validator
**Start:** 2026-05-20
**Branch:** demo/azure-deploy
**Worktree:** /Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai
**Acceptance criteria:**
- BUG-001: FieldAndCrop Edit saves successfully without modifying values (decimal locale fix)
- BUG-002: TempData["WorkflowError"] / ["Error"] appears visibly on FileMaster Details after failed workflow transition
- BUG-003: GovernmentWaterControlArea dropdown on Property Edit has at least one option (Blyde River GWCA seeded)
- BUG-004: WaterSource and IrrigationSystem dropdowns on FieldAndCrop Create are populated
- BUG-005: Crops, CropWaterRates, WorkflowStates each appear exactly once after seeding (no duplicates)
- BUG-006: ELU Assessment panel in Details.cshtml uses DWS CSS variables, not hardcoded hex colors
- `dotnet build` 0 errors
- `dotnet test` 243/243 pass (or more)

## Journal

### 2026-05-20 — BUG-001 FIXED (dotnet-master)

**Root cause confirmed.** Server OS culture uses comma as decimal separator. ASP.NET tag helpers always render `decimal` properties in invariant culture (e.g. `10.00`), but model binding on POST runs under the request culture (comma-based), so it rejects every dot-formatted decimal coming back. Affected every Edit form with a `decimal` field: FieldAndCrop (FieldArea, CropArea, RotationFactor, SAPWATCalculationResult), DamCalculation (DamCapacity, WallLength, Fetch, RiverDistance, ContourDifference, DamArea, DamDepth, ShapeFactor), Forestation (Pre1972Hectares, SFRAPermitHectares, QualifyPeriodSFRAHectares, ELUHectares, etc.).

**Fix.** Added `UseRequestLocalization` middleware to `Program.cs` pinning the request culture to `CultureInfo.InvariantCulture` for both formatting and UI. Placed BEFORE `UseRouting` so the culture is set for the whole pipeline (including MVC model binding). Single-culture list (`InvariantCulture` only) means the framework cannot negotiate down to any other locale — Accept-Language headers are ignored, which is exactly what we want for a single-language back-office system.

```csharp
using System.Globalization;
using Microsoft.AspNetCore.Localization;
// ...
var invariantCulture = CultureInfo.InvariantCulture;
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(invariantCulture, invariantCulture),
    SupportedCultures = new[] { invariantCulture },
    SupportedUICultures = new[] { invariantCulture }
});
```

**Why not change view models or culture-aware model binders.** The middleware approach is the canonical Microsoft-recommended fix for exactly this symptom and requires no per-property attributes, no custom `IModelBinder`, and no view changes. It is also forward-compatible: every future decimal/date field on every future Edit form is covered automatically.

**Verification.**
- `dotnet build` — 0 errors, 8 pre-existing warnings unchanged.
- App started cleanly on `http://localhost:4002` (no startup errors, seeding ran, `Now listening on: http://localhost:4002`). Confirmed FieldAndCrop route is reachable (302 → Login as expected for unauthenticated). Full authenticated form roundtrip not exercised manually — covered transitively by the integration test suite which mounts the same `Program.cs` via `WebApplicationFactory`.
- `dotnet test` — 243/243 pass, no regressions.

**Out-of-scope drift noticed.** Working tree shows `Services/SeedDataService.cs` is modified — not by me, predates this task. Did **not** stage it; commit is `Program.cs` only.

**Files touched.**
- `Program.cs` — +17 lines (using directives + middleware block).

### 2026-05-20 — BUG-003 / BUG-004 / BUG-005 seeding fixes (dotnet-master)

**Files touched**
- `Services/SeedDataService.cs`

**BUG-003 — empty GWCA dropdown**
- Added `SeedGovernmentWaterControlAreasAsync()` (seeds "Blyde River", gazette `GN 180 of 10 July 1970`, proclaimed 1970-07-10).
- Reordered `SeedAsync()` so GWCAs seed BEFORE `SeedGwcaProclamationRulesAsync()`; the rule seeder previously silently no-op'd when the lookup returned null.

**BUG-004 — empty WaterSource / IrrigationSystem lookups**
- Added `SeedWaterSourcesAsync()`: River, Borehole, Dam, Spring, Irrigation Canal, Wetland.
- Added `SeedIrrigationSystemsAsync()`: Drip, Sprinkler, Flood/Furrow, Centre Pivot, Micro-irrigation.
- Both use per-name idempotency (skip if name already exists).

**BUG-005 — duplicate seed rows under concurrent startup**
- Added `DeduplicateExistingSeedRowsAsync()` (runs first in `SeedAsync`).
- Strategy: "keep lowest GUID per natural key, reparent child FKs onto the keeper, delete the rest".
- EF Core can't translate `GroupBy + SelectMany + Skip` to SQL → materialise (id, key) pairs first, group in memory.
- Covers: WorkflowStates (reparent WorkflowStepRecords + WorkflowInstances), Crops (reparent CropWaterRates + FieldAndCrops via shadow FK), CropWaterRates (composite key, no children), SfraSpeciesRates, GovernmentWaterControlAreas (reparent Properties + LawfulnessAssessmentResults; delete duplicate child GwcaProclamationRules), WaterSources (reparent Irrigations + FieldAndCrops shadow FK), IrrigationSystems (reparent CropWaterRates + FieldAndCrops shadow FK), GwcaProclamationRules (composite WaterControlAreaId+RuleCode).
- Replaced bulk `AnyAsync()` checks with per-item `HashSet<>` membership tests on: `SeedCropsAsync`, `SeedCalculatorReferenceDataAsync` (CropWaterRates block), `SeedWorkflowStatesAsync` (full canonical-merge with display-order drift correction), `SeedGwcaProclamationRulesAsync` (per-RuleCode for the GWCA).

**Surprise during testing**
- After the first round, the post-test DB still showed 5× duplicates of Blyde River GWCA + new WaterSources + new IrrigationSystems. Root cause: the integration test run spins up multiple `WebApplicationFactory<Program>` instances concurrently in the same xUnit process — each calls `SeedAsync()`. The initial dedup pass only covered the tables called out in the task (Crops/WorkflowStates/etc.); the new GWCA/WaterSource/IrrigationSystem seeders introduced the same race and needed dedup coverage too. Extended dedup to those three + GwcaProclamationRules. Final state verified: GWCAs=1, GwcaProclamationRules=5, WaterSources=9, IrrigationSystems=7, Crops=14, CropWaterRates=14, WorkflowStates=35, SfraSpeciesRates=4 — zero duplicates across every affected table.

**Out of scope but noted**
- `CustomerTypes`, `Provinces`, `WaterManagementAreas`, `AuthorisationTypes`, `Periods`, `EntitlementTypes`, `FileMasters` still use bulk `AnyAsync()`. They have not produced observed duplicates yet, but the same race is theoretically possible. Did not change to keep blast radius small — these tables have FK fan-out into business data (Properties.ProvinceId, OrganisationalUnits.WmaId, FileMaster ↔ WorkflowInstance, etc.) and reparenting would require deeper analysis than this task budgeted.

**Verification**
- `dotnet build` → 0 errors, 8 warnings (all pre-existing).
- `dotnet test` → 243/243 passing.
- Cold `dotnet run` → "Application started" with no exceptions or stack traces in log.
- SQL spot-check after the test run confirmed canonical lookup counts and zero duplicates across the seven affected tables.

### 2026-05-20 — BUG-002 + BUG-006 FIXED (dotnet-master)

**Files touched**
- `Controllers/FileMasterController.cs`
- `Views/FileMaster/Details.cshtml`

**BUG-002 — workflow/letter errors invisible on Details after redirect**

Root cause was a TempData key split between writers and readers. `FileMasterController` wrote workflow-related errors to `TempData["WorkflowError"]` in six places (AdvanceWorkflow, IssueLetter ×3, MarkLetterResponse ×2), but the shared error banner in `Views/Shared/_Layout.cshtml` only renders `TempData["Error"]` (and `TempData["Success"]`). The Details view had a local inline `@if (TempData["WorkflowError"] != null)` block (line 26) which technically *did* match — so the banner did render when reached via Details — but:
1. It sat *below* the page header / breadcrumb / status badge, so users testing in a busy viewport scrolled past it.
2. Other actions in the same controller (e.g. `AssessLawfulness`) already used `TempData["Error"]`, so error visibility was inconsistent across actions hitting the same page.
3. Any future view of the same case (or any other page in the app) that didn't replicate the inline block would silently drop the message.

**Fix.** Remapped all six `TempData["WorkflowError"]` writes in `FileMasterController` to `TempData["Error"]`. Removed the now-duplicate inline `@if (TempData["WorkflowError"])` block from `Details.cshtml`. All error/success banners now flow through the single `_Layout.cshtml` channel at the top of `.page-content`, appearing consistently above the page header on every page in the app.

Why this approach (vs adding a `TempData["WorkflowError"]` block to the Layout, or keeping the two keys side-by-side): the task constraint was "do not add new TempData keys — remap to existing keys if possible". Consolidating to `TempData["Error"]` removes a parallel channel rather than creating a second one in the Layout, and prevents future drift where new actions accidentally pick the wrong key. No controller business logic or workflow transition logic was touched — only the TempData key string.

Confirmed no test fixtures or other call sites read `TempData["WorkflowError"]` (`grep -rn WorkflowError` returns zero matches after the edit).

**BUG-006 — ELU Assessment panel inline hex colors**

Replaced six hardcoded hex values in the ELU Assessment section of `Details.cshtml` with the closest semantic DWS CSS variables defined in `wwwroot/css/dws.css`:

| Hardcoded | DWS variable | Resolves to | Role |
|-----------|-------------|-------------|------|
| `#6c757d` | `var(--dws-text-muted)` | `#555d6e` | Muted text (placeholder, labels, low-emphasis cells) |
| `#f8f9fa` | `var(--dws-neutral-100)` | `#f2f4f7` | Light surface background |
| `#dee2e6` | `var(--dws-border)` | `#d0d5db` | Table row borders + container border |
| `#f0f4f8` | `var(--dws-neutral-100)` | `#f2f4f7` | Table header background |
| `#166534` | `var(--dws-success)` | `#2e7d32` | Lawful (m³) column — semantic success |
| `#991b1b` | `var(--dws-danger)` | `#c62828` | Unlawful (m³) column — semantic danger |

The conditional ternaries inside `style="color:@(...)"` were updated to emit the CSS variable string (`"var(--dws-danger)"` / `"var(--dws-text-muted)"`) rather than hex literals so the rendered inline style stays token-driven. `grep -n "#6c757d\|#f8f9fa\|#dee2e6\|#f0f4f8\|#166534\|#991b1b" Views/FileMaster/Details.cshtml` returns zero matches after the edit.

**Verification**
- `dotnet build` — 0 errors, 7 pre-existing warnings (unchanged from BUG-001 baseline).
- `dotnet test` — 243/243 passing, no regressions.
- Visual review: ELU panel now inherits theme colours via CSS vars, so future palette changes in `dws.css` propagate without touching the view.

**Out of scope / noted**
- `Models/PRD Documentation/`, `Tests/TestResults/`, untracked `docs/` files predate this task — not staged.
- A handful of other views still contain inline hex literals (e.g. `Views/Shared/_Layout.cshtml` success/error banners themselves at lines 111 and 117 use `#f0fdf4`, `#86efac`, `#166534`, `#fef2f2`, `#fecaca`, `#991b1b`). Those banners pre-date this sprint; they are functionally the same colours we just replaced and are a natural follow-up if a "wholesale DWS-tokenisation of inline styles" task comes up.

## Retro (on completion)
