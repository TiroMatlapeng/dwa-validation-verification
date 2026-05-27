# BUG-019 — Appendix D Dam Calculator Inputs on Create

**Date:** 2026-05-27
**Status:** Approved

## Problem

The `DamCalculation` Create form does not expose the Appendix D input fields (method selector, wall length, fetch, river distance, contour difference, dam area, dam depth, shape factor). Users are currently instructed to calculate externally and type the result into `DamCapacity` manually. The Edit form already has the full Appendix D section and the Calculate Capacity button works end-to-end — the gap is only on Create.

## Scope

Three targeted changes. No model changes, no ViewModel changes, no migrations, no new tests.

| File | Change |
|------|--------|
| `Views/DamCalculation/Create.cshtml` | Add Appendix D section (method selector, conditional M1/M2 inputs, shape factor, JS toggle) |
| `Controllers/DamCalculationController.cs` — `Create` POST | Map all 8 calculator input fields from VM to entity; redirect to Edit instead of Index |
| `Controllers/DamCalculationController.cs` — `Create` GET | No change |

## View — Create.cshtml

Add a new form section after the "Capacity & Status" row, before the submit buttons:

```
<div class="form-section-title">Appendix D Volume Calculation</div>

Calculation Method select (id="calcMethod", name="CalculationMethod")
  options: "", "Method1 — Wall Length", "Method2 — Area"
  onchange="toggleMethod()"

Method 1 fields (id="method1-fields", display:none until Method1 selected):
  - Wall Length (m)         name="WallLength"
  - Fetch (m)               name="Fetch"
  - River Distance R1 (m)   name="RiverDistance"
  - Contour Difference C1   name="ContourDifference"

Method 2 fields (id="method2-fields", display:none until Method2 selected):
  - Dam Area (ha)   name="DamArea"
  - Dam Depth (m)   name="DamDepth"

Shape Factor select (id="shape-factor-group", display:none until any method selected):
  - 0.33 — Ravine (triangle)
  - 0.40 — Square with bends
  - 0.50 — Circular

@section Scripts: toggleMethod() JS — same implementation as Edit.cshtml
```

Replace the existing hint text on `DamCapacity` ("Use Appendix D Method 1 or 2 to calculate before entering") with: *"Enter inputs above and save — then click Calculate Capacity on the next screen."*

## Controller — Create POST

**Map calculator fields from VM to entity** (currently missing):

```csharp
CalculationMethod    = vm.CalculationMethod,
WallLength           = vm.WallLength,
Fetch                = vm.Fetch,
RiverDistance        = vm.RiverDistance,
ContourDifference    = vm.ContourDifference,
DamArea              = vm.DamArea,
DamDepth             = vm.DamDepth,
ShapeFactor          = vm.ShapeFactor,
```

**Change redirect** from Index to Edit:

```csharp
// Before:
return RedirectToAction(nameof(Index), new { propertyId = vm.PropertyId });

// After:
return RedirectToAction(nameof(Edit), new { id = entity.DamCalculationId });
```

This lands the user directly on the Edit page where they can click "Calculate Capacity" without a separate navigation step.

## User Flow (after fix)

1. User clicks **+ Add Dam Calculation**
2. Fills in dam details, dates, river
3. Selects Calculation Method → inputs appear
4. Enters inputs, selects shape factor
5. Leaves DamCapacity at 0 (or enters a known value)
6. Clicks **Save Record** → saved, redirected to **Edit**
7. Clicks **Calculate Capacity** → capacity computed and displayed
8. Clicks **Save Changes** → final record persisted

## What is NOT changing

- `DamVolumeCalculator.cs` — pure calculator, already correct
- `CalculatorService.ComputeDamVolumeAsync` — already correct
- `Edit.cshtml` — already has the full Appendix D section
- `DamCalculationViewModel.cs` — all fields already present
- `DamCalculation.cs` model — all fields already present
- Tests — existing suite covers the calculator; no new tests needed for view/controller wiring
