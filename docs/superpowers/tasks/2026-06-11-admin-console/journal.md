# Task: Admin console — org units, GWCA proclamation rules, reference data

**Start:** 2026-06-11T14:15+02:00
**Branch:** feat/admin-console
**Worktree:** /Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai (main repo, no worktree)
**Plan:** n/a — scoped from project memory: "user-admin done, but OrganisationalUnit mgmt + reference-data CRUD + GWCA proclamation-rule mgmt are seed-only — candidate next feature"
**Acceptance criteria:**
- SystemAdmin (CanAdminister policy) can manage OrganisationalUnits (list/create/edit, scope fields: type, province, WMA, optional catchment) through the UI.
- SystemAdmin can manage GovernmentWaterControlAreas and their per-GWCA GwcaProclamationRule records (the configurable legal limits seeded from gazette proclamations).
- SystemAdmin can manage core reference data used by capture forms: Rivers, CatchmentAreas, IrrigationBoards, Crops, WaterSources, IrrigationSystems.
- Deletes never orphan in-use data: rows referenced by FKs are protected (block with a clear message, or soft-deactivate where the model supports it).
- All new controllers follow the existing Admin pattern: Controllers/Admin/, [Authorize(Policy = DwsPolicies.CanAdminister)], [Route("Admin/[controller]/[action]")]; sidebar gets an Administration section visible to SystemAdmin only.
- Non-admin roles get 403/AccessDenied on every new route (tested).
- Unit/controller tests in Tests/ for the new surface; full unit suite stays green; build clean.

## Journal

### 2026-06-11 — dotnet-architect (admin-console-builder, two runs; both ended in API drops) + controller completion

- Implementer (before drop): Controllers/Admin/{OrganisationalUnitsController, GwcasController, ReferenceDataController}.cs — full CRUD, [Authorize(CanAdminister)] + Admin/[controller]/[action] routes, FK-usage delete protection on every entity (users/cases/child-units for org units; properties/assessment-results for GWCAs; dam-calcs, properties/cases/offices, cases/letters, field-crops/water-rates, irrigations for reference data), GwcaProclamationRule managed inline with IsActive soft-toggle. ViewModels/Admin/*.cs. Views: OrganisationalUnits complete (incl. _Form), Gwcas complete (Index/Create/Edit/EditRule), ReferenceData Index + River pair.
- Controller (after second drop, pre-views): 10 remaining ReferenceData views (Catchment/IrrigationBoard/Crop/WaterSource/IrrigationSystem Create+Edit pairs, dws-* classes); _Layout sidebar — System section renamed Administration, added Organisational Units / GWCAs & Rules / Reference Data links (SystemAdmin only); Tests/Controllers/AdminConsoleControllerTests.cs (9 tests: policy-attribute reflection on all 3 controllers, river create+dedupe, FK-delete refusals for river/catchment/org-unit, unreferenced delete success, rule IsActive toggle — fixed required members ApplicationUser.{FirstName,LastName,EmployeeNumber}, Guid Id, rule PK = RuleId); Tests.E2E/AdminConsoleTests.cs (7 tests: 3 surfaces reachable as Admin, denied as ReadOnly, river create→list→delete round trip; gotchas: JS confirm() needs page.Dialog accept, selector must be form.dws-form to avoid the layout form).
- Verification (fresh): unit 526/526; E2E 35/35 (2m17s). Build 0 errors.
- Rollout plan updated (rows 4.4, 4.5, 5.1, 11.1) per standing practice.
- Status: DONE

## Retro (on completion)

Converged: the implementer's controller/viewmodel layer was complete, idiomatic, and needed zero correction — FK-guard choices were exactly right. Drifted: two more mid-run API drops (133k and 192k tokens) — the third agent today to die mid-flight; the controller finished views/nav/tests. Lesson reinforced from the E2E task retro: split big builds into implement-then-finish dispatches, or budget for controller completion. Test-authoring gotchas worth remembering: ApplicationUser uses IdentityUser<Guid> with required FirstName/LastName/EmployeeNumber; GwcaProclamationRule PK is RuleId; Playwright dismisses JS confirm() dialogs by default (delete buttons need a Dialog handler); prefer scoped form selectors (form.dws-form) over bare form when a layout-level form exists.
