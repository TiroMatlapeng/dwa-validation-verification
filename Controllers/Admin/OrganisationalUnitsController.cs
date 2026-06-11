using dwa_ver_val.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Controllers.Admin;

[Authorize(Policy = DwsPolicies.CanAdminister)]
[Route("Admin/[controller]/[action]")]
public class OrganisationalUnitsController : Controller
{
    private readonly ApplicationDBContext _db;

    /// <summary>The five organisational-unit types per the DWS hierarchy.</summary>
    public static readonly string[] UnitTypes =
        { "National", "Provincial", "Regional", "CMA", "Catchment" };

    public OrganisationalUnitsController(ApplicationDBContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var items = await _db.OrganisationalUnits
            .OrderBy(ou => ou.Name)
            .Select(ou => new OrgUnitListItemViewModel
            {
                Id = ou.OrgUnitId,
                Name = ou.Name,
                Type = ou.Type,
                ProvinceName = ou.Province != null ? ou.Province.ProvinceName : null,
                WmaName = ou.WaterManagementArea != null ? ou.WaterManagementArea.WmaName : null,
                CatchmentName = ou.CatchmentArea != null ? ou.CatchmentArea.CatchmentName : null,
                ParentName = ou.ParentOrgUnit != null ? ou.ParentOrgUnit.Name : null,
                UserCount = ou.Users.Count
            })
            .ToListAsync(ct);

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var model = new OrgUnitFormViewModel();
        await PopulateDropdowns(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OrgUnitFormViewModel model, CancellationToken ct)
    {
        ValidateScopeCoherence(model);
        if (!ModelState.IsValid)
        {
            await PopulateDropdowns(model, ct);
            return View(model);
        }

        var unit = new OrganisationalUnit
        {
            OrgUnitId = Guid.NewGuid(),
            Name = model.Name.Trim(),
            Type = model.Type,
            ProvinceId = model.ProvinceId,
            WmaId = model.WmaId,
            CatchmentAreaId = model.CatchmentAreaId,
            ParentOrgUnitId = model.ParentOrgUnitId
        };
        _db.OrganisationalUnits.Add(unit);
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = $"Organisational unit \"{unit.Name}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var unit = await _db.OrganisationalUnits.FirstOrDefaultAsync(ou => ou.OrgUnitId == id, ct);
        if (unit is null) return NotFound();

        var model = new OrgUnitFormViewModel
        {
            Id = unit.OrgUnitId,
            Name = unit.Name,
            Type = unit.Type,
            ProvinceId = unit.ProvinceId,
            WmaId = unit.WmaId,
            CatchmentAreaId = unit.CatchmentAreaId,
            ParentOrgUnitId = unit.ParentOrgUnitId
        };
        await PopulateDropdowns(model, ct);
        return View(model);
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, OrgUnitFormViewModel model, CancellationToken ct)
    {
        if (id != model.Id) return BadRequest();

        ValidateScopeCoherence(model);
        if (model.ParentOrgUnitId == id)
            ModelState.AddModelError(nameof(model.ParentOrgUnitId), "A unit cannot be its own parent.");

        if (!ModelState.IsValid)
        {
            await PopulateDropdowns(model, ct);
            return View(model);
        }

        var unit = await _db.OrganisationalUnits.FirstOrDefaultAsync(ou => ou.OrgUnitId == id, ct);
        if (unit is null) return NotFound();

        unit.Name = model.Name.Trim();
        unit.Type = model.Type;
        unit.ProvinceId = model.ProvinceId;
        unit.WmaId = model.WmaId;
        unit.CatchmentAreaId = model.CatchmentAreaId;
        unit.ParentOrgUnitId = model.ParentOrgUnitId;
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = $"Organisational unit \"{unit.Name}\" updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var unit = await _db.OrganisationalUnits.FirstOrDefaultAsync(ou => ou.OrgUnitId == id, ct);
        if (unit is null) { TempData["Error"] = "Organisational unit not found."; return RedirectToAction(nameof(Index)); }

        // Block delete if any user, case, or child unit still references it (FKs are SetNull/Restrict —
        // an unchecked delete would silently detach staff and cases from their office).
        if (await _db.Users.AnyAsync(u => u.OrgUnitId == id, ct))
        { TempData["Error"] = $"Cannot delete \"{unit.Name}\": users are still assigned to this office."; return RedirectToAction(nameof(Index)); }

        if (await _db.FileMasters.AnyAsync(fm => fm.OrgUnitId == id, ct))
        { TempData["Error"] = $"Cannot delete \"{unit.Name}\": V&V cases are still assigned to this office."; return RedirectToAction(nameof(Index)); }

        if (await _db.OrganisationalUnits.AnyAsync(ou => ou.ParentOrgUnitId == id, ct))
        { TempData["Error"] = $"Cannot delete \"{unit.Name}\": it is the parent of other offices."; return RedirectToAction(nameof(Index)); }

        _db.OrganisationalUnits.Remove(unit);
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = $"Organisational unit \"{unit.Name}\" deleted.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Enforces the scope rules the OrganisationalUnit model implies: a Catchment unit
    /// must name a catchment; Regional/CMA units must name a WMA; Provincial units must
    /// name a province. National units carry no scope.
    /// </summary>
    private void ValidateScopeCoherence(OrgUnitFormViewModel model)
    {
        if (!UnitTypes.Contains(model.Type))
        {
            ModelState.AddModelError(nameof(model.Type), "Unknown organisational unit type.");
            return;
        }

        switch (model.Type)
        {
            case "Catchment" when model.CatchmentAreaId is null:
                ModelState.AddModelError(nameof(model.CatchmentAreaId), "A Catchment-type unit must be linked to a catchment area.");
                break;
            case "Regional" when model.WmaId is null:
            case "CMA" when model.WmaId is null:
                ModelState.AddModelError(nameof(model.WmaId), "A Regional or CMA unit must be linked to a Water Management Area.");
                break;
            case "Provincial" when model.ProvinceId is null:
                ModelState.AddModelError(nameof(model.ProvinceId), "A Provincial unit must be linked to a province.");
                break;
        }
    }

    private async Task PopulateDropdowns(OrgUnitFormViewModel model, CancellationToken ct)
    {
        model.AvailableTypes = UnitTypes;
        model.AvailableProvinces = await _db.Provinces
            .OrderBy(p => p.ProvinceName)
            .Select(p => new LookupOption { Id = p.ProvinceId, Name = p.ProvinceName })
            .ToListAsync(ct);
        model.AvailableWmas = await _db.WaterManagementAreas
            .OrderBy(w => w.WmaName)
            .Select(w => new LookupOption { Id = w.WmaId, Name = w.WmaName })
            .ToListAsync(ct);
        model.AvailableCatchments = await _db.CatchmentAreas
            .OrderBy(c => c.CatchmentCode)
            .Select(c => new LookupOption { Id = c.CatchmentAreaId, Name = c.CatchmentCode + " — " + c.CatchmentName })
            .ToListAsync(ct);
        model.AvailableParents = await _db.OrganisationalUnits
            .Where(ou => ou.OrgUnitId != model.Id)
            .OrderBy(ou => ou.Name)
            .Select(ou => new LookupOption { Id = ou.OrgUnitId, Name = ou.Name })
            .ToListAsync(ct);
    }
}
