# Session: Bug-Fix Sprint 2 — QA-driven fixes, demo readiness achieved
**Date:** 2026-05-20
**Branch:** demo/azure-deploy
**Orchestrator:** Claude Sonnet 4.6

## What happened this session

Ran the user-testing-validator against the full Wave 2a/2b feature set. Found 6 bugs (1 P0, 3 P1, 1 P2, 1 P3). Fixed all 6 via parallel subagent dispatch. Second QA pass found 1 still-failing (BUG-001 — wrong fix approach) and 2 new low-priority issues. Re-fixed and re-ran final QA — all 8 issues verified, system signed off as **READY FOR DEMO**.

### Commits landed

| SHA | Description |
|-----|-------------|
| `8fd6083` | fix(locale): UseRequestLocalization with InvariantCulture (superseded — see below) |
| `710fe98` | fix(seed): GWCA + WaterSource + IrrigationSystem seed data, dedup idempotency |
| `de62387` | fix(ui): TempData error display on Details + ELU Assessment panel DWS CSS tokens |
| `1fa3687` | docs: bug-fix sprint 2 task journal |
| `23b1237` | fix(ui): hardcoded hex in panel partials + InvariantCulture for success messages |
| `8d253f9` | fix(locale): InvariantDecimalModelBinderProvider (correct fix for BUG-001) |

### Test count: 243/243 (unchanged — all seeding/UI fixes, no new test surface)

### Key decisions made

- **`UseRequestLocalization` does NOT fix decimal model binding on en_ZA hosts** — `DecimalModelBinder` resolves culture from `CultureInfo.CurrentCulture` (process), not from request-localization middleware. The correct fix is a custom `InvariantDecimalModelBinderProvider` registered at `ModelBinderProviders.Insert(0, ...)` in `AddControllersWithViews(options => ...)`.
- **`InvariantDecimalModelBinder`** normalises comma→dot before parsing, so both "10,5" and "10.5" are accepted from any locale.
- **Seeding idempotency**: bulk `if (!await AnyAsync())` is not safe under concurrent startup (multiple `WebApplicationFactory` instances in xUnit, multiple AKS replicas). Fixed with `DeduplicateExistingSeedRowsAsync()` (reparent child FKs onto keeper row, delete duplicates) + per-item `HashSet<>` membership checks. Pattern applied to: WorkflowStates, Crops, CropWaterRates, SfraSpeciesRates, GovernmentWaterControlAreas, WaterSources, IrrigationSystems, GwcaProclamationRules.
- **GWCA seeding order**: `SeedGovernmentWaterControlAreasAsync()` must run BEFORE `SeedGwcaProclamationRulesAsync()` — the rules seeder silently no-ops if GWCA lookup returns null.
- **TempData key consolidation**: `TempData["WorkflowError"]` remapped to `TempData["Error"]` throughout FileMasterController — the shared Layout banner only reads `["Error"]` and `["Success"]`.
- **3 hex values left intentionally** in `_WorkflowPanel.cshtml` and `_LettersPanel.cshtml` where no DWS CSS token equivalent exists (`#fcd34d` warning border, `#fef2f2`/`#fecaca` expired-deadline state).

### Residual items (non-blocking, post-demo)

- Read-only result divs in `Views/FieldAndCrop/Edit.cshtml` and `Views/Forestation/Edit.cshtml` call `.ToString("N2")` without `InvariantCulture` — display shows comma decimal on en_ZA host. Data stored correctly. ~1 hour fix.
- Same views have 3 hardcoded hex colors (`#f0fdf4`, `#bbf7d0`, `#166534`) in SAPWAT/SFRA result panels. ~30 minutes.
- 7 other seeders (`CustomerTypes`, `Provinces`, `WaterManagementAreas`, `AuthorisationTypes`, `Periods`, `EntitlementTypes`, `FileMasters`) still use bulk `AnyAsync()` — theoretically race-prone but no observed duplicates yet.

## State at session end

- Build: 0 errors
- Tests: 243/243 passing
- QA verdict: **READY FOR DEMO**
- App running: localhost:4000 (fresh restart after final fix)
- All commits pushed to `origin/demo/azure-deploy`
- Branch: `demo/azure-deploy`
