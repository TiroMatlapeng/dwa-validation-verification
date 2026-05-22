# Task: Display Labels — humanise form labels across internal portal
**Start:** 2026-05-22T17:00+02:00
**Branch:** main
**Worktree:** /Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai

**Context:**
PascalCase property names were rendering as labels in forms (e.g. "StreetAddress", "SuburbName").
The fix is adding `[Display(Name = "...")]` attributes to models/ViewModels.
Rule: split PascalCase into words; abbreviations (SG, ELU, WARMS, SFRA, SAPWAT, WMA, GWCA, HDI) stay uppercase; FK ID fields show referenced entity; booleans drop "Is" prefix.

**Scope finding (pre-flight scan):**
- `FileMaster.cs` — already has full Display attrs ✓
- `Property.cs` — already has full Display attrs ✓
- `PropertyOwner.cs` — done in BUG-017 sprint ✓
- `DamCalculationViewModel.cs`, `FieldAndCropViewModel.cs`, `ForestationViewModel.cs` — already done ✓
- `SubdivideViewModel.cs`, `ConsolidateViewModel.cs` — already done ✓
- `CreateUserViewModel.cs`, `EditUserViewModel.cs` — done in MISSING-001 sprint ✓
- `PAJAChecklistForm.cs` — view uses hardcoded `<div class="form-label">` not `<label asp-for>` — NO CHANGE NEEDED
- `UserListItemViewModel.cs` — table display only, no asp-for labels — NO CHANGE NEEDED
- `OngoingValidationRow` (in ValidationController.cs) — view renders column headers manually — NO CHANGE NEEDED

**ACTUAL GAPS (views with empty asp-for labels relying on property name):**
- `Address.cs`: StreetAddress, SuburbName, CityName render raw in Property/Register + Property/Edit (empty `<label asp-for="Address.StreetAddress">`)
- `DamCalculation.cs`: forms use ViewModel (done), model annotations secondary
- `FieldAndCrop.cs`: same — secondary
- `Forestation.cs`: same — secondary

## Journal

### 2026-05-22T17:00+02:00 — Controller — pre-flight + direct implementation
- Read all target models/ViewModels to verify existing Display coverage.
- Confirmed ViewModels for DamCalculation/FieldAndCrop/Forestation fully covered.
- Confirmed Address.cs has ZERO Display attributes — this is the primary visual gap.
- Implementing directly (no agent needed — pure attribute additions to 4 model files).
- Status: IMPLEMENTING
