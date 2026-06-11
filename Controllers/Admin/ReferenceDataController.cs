using dwa_ver_val.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Controllers.Admin;

/// <summary>
/// Admin CRUD for the core reference/lookup tables used by capture forms:
/// Rivers, CatchmentAreas, IrrigationBoards, Crops, WaterSources, IrrigationSystems.
/// Every delete is guarded by an FK-usage check so in-use lookups can never be orphaned.
/// </summary>
[Authorize(Policy = DwsPolicies.CanAdminister)]
[Route("Admin/[controller]/[action]")]
public class ReferenceDataController : Controller
{
    private readonly ApplicationDBContext _db;

    public ReferenceDataController(ApplicationDBContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = new ReferenceDataIndexViewModel
        {
            Rivers = await _db.Rivers.OrderBy(r => r.RiverName).ToListAsync(ct),
            CatchmentAreas = await _db.CatchmentAreas
                .Include(c => c.WaterManagementArea)
                .OrderBy(c => c.CatchmentCode).ToListAsync(ct),
            IrrigationBoards = await _db.IrrigationBoards.OrderBy(b => b.IrrigationBoardName).ToListAsync(ct),
            Crops = await _db.Crops.Include(c => c.CropType).OrderBy(c => c.CropName).ToListAsync(ct),
            WaterSources = await _db.WaterSources.OrderBy(w => w.WaterSourceName).ToListAsync(ct),
            IrrigationSystems = await _db.IrrigationSystems.OrderBy(i => i.IrrigationSystemName).ToListAsync(ct)
        };
        return View(model);
    }

    // ── Rivers ──────────────────────────────────────────────

    [HttpGet]
    public IActionResult CreateRiver() => View(new RiverFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRiver(RiverFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        if (await _db.Rivers.AnyAsync(r => r.RiverName == model.RiverName.Trim(), ct))
        {
            ModelState.AddModelError(nameof(model.RiverName), "A river with this name already exists.");
            return View(model);
        }
        _db.Rivers.Add(new River { RiverId = Guid.NewGuid(), RiverName = model.RiverName.Trim() });
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "River added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> EditRiver(Guid id, CancellationToken ct)
    {
        var r = await _db.Rivers.FindAsync(new object[] { id }, ct);
        if (r is null) return NotFound();
        return View(new RiverFormViewModel { Id = r.RiverId, RiverName = r.RiverName });
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRiver(Guid id, RiverFormViewModel model, CancellationToken ct)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);
        var r = await _db.Rivers.FindAsync(new object[] { id }, ct);
        if (r is null) return NotFound();
        r.RiverName = model.RiverName.Trim();
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "River updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRiver(Guid id, CancellationToken ct)
    {
        var r = await _db.Rivers.FindAsync(new object[] { id }, ct);
        if (r is null) { TempData["Error"] = "River not found."; return RedirectToAction(nameof(Index)); }
        if (await _db.DamCalculations.AnyAsync(d => d.RiverId == id, ct))
        { TempData["Error"] = $"Cannot delete \"{r.RiverName}\": it is used by dam calculations."; return RedirectToAction(nameof(Index)); }
        _db.Rivers.Remove(r);
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "River deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── CatchmentAreas ──────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> CreateCatchment(CancellationToken ct)
        => View(new CatchmentAreaFormViewModel { AvailableWmas = await LoadWmas(ct) });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCatchment(CatchmentAreaFormViewModel model, CancellationToken ct)
    {
        if (await _db.CatchmentAreas.AnyAsync(c => c.CatchmentCode == model.CatchmentCode.Trim(), ct))
            ModelState.AddModelError(nameof(model.CatchmentCode), "A catchment with this code already exists.");
        if (!ModelState.IsValid) { model.AvailableWmas = await LoadWmas(ct); return View(model); }

        _db.CatchmentAreas.Add(new CatchmentArea
        {
            CatchmentAreaId = Guid.NewGuid(),
            CatchmentCode = model.CatchmentCode.Trim(),
            CatchmentName = model.CatchmentName.Trim(),
            WmaId = model.WmaId
        });
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Catchment area added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> EditCatchment(Guid id, CancellationToken ct)
    {
        var c = await _db.CatchmentAreas.FindAsync(new object[] { id }, ct);
        if (c is null) return NotFound();
        return View(new CatchmentAreaFormViewModel
        {
            Id = c.CatchmentAreaId,
            CatchmentCode = c.CatchmentCode,
            CatchmentName = c.CatchmentName,
            WmaId = c.WmaId,
            AvailableWmas = await LoadWmas(ct)
        });
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCatchment(Guid id, CatchmentAreaFormViewModel model, CancellationToken ct)
    {
        if (id != model.Id) return BadRequest();
        if (await _db.CatchmentAreas.AnyAsync(c => c.CatchmentAreaId != id && c.CatchmentCode == model.CatchmentCode.Trim(), ct))
            ModelState.AddModelError(nameof(model.CatchmentCode), "A catchment with this code already exists.");
        if (!ModelState.IsValid) { model.AvailableWmas = await LoadWmas(ct); return View(model); }

        var c = await _db.CatchmentAreas.FindAsync(new object[] { id }, ct);
        if (c is null) return NotFound();
        c.CatchmentCode = model.CatchmentCode.Trim();
        c.CatchmentName = model.CatchmentName.Trim();
        c.WmaId = model.WmaId;
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Catchment area updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCatchment(Guid id, CancellationToken ct)
    {
        var c = await _db.CatchmentAreas.FindAsync(new object[] { id }, ct);
        if (c is null) { TempData["Error"] = "Catchment not found."; return RedirectToAction(nameof(Index)); }
        if (await _db.Properties.AnyAsync(p => p.CatchmentAreaId == id, ct)
            || await _db.FileMasters.AnyAsync(f => f.CatchmentAreaId == id, ct)
            || await _db.OrganisationalUnits.AnyAsync(o => o.CatchmentAreaId == id, ct))
        { TempData["Error"] = $"Cannot delete \"{c.CatchmentCode}\": it is referenced by properties, cases, or offices."; return RedirectToAction(nameof(Index)); }
        _db.CatchmentAreas.Remove(c);
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Catchment area deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── IrrigationBoards ────────────────────────────────────

    [HttpGet]
    public IActionResult CreateIrrigationBoard() => View(new IrrigationBoardFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateIrrigationBoard(IrrigationBoardFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        _db.IrrigationBoards.Add(new IrrigationBoard
        {
            IrrigationBoardId = Guid.NewGuid(),
            IrrigationBoardName = model.IrrigationBoardName.Trim(),
            IrrigationBoardPNumber = model.IrrigationBoardPNumber,
            EmailAddress = model.EmailAddress
        });
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Irrigation board added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> EditIrrigationBoard(Guid id, CancellationToken ct)
    {
        var b = await _db.IrrigationBoards.FindAsync(new object[] { id }, ct);
        if (b is null) return NotFound();
        return View(new IrrigationBoardFormViewModel
        {
            Id = b.IrrigationBoardId,
            IrrigationBoardName = b.IrrigationBoardName,
            IrrigationBoardPNumber = b.IrrigationBoardPNumber,
            EmailAddress = b.EmailAddress
        });
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditIrrigationBoard(Guid id, IrrigationBoardFormViewModel model, CancellationToken ct)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);
        var b = await _db.IrrigationBoards.FindAsync(new object[] { id }, ct);
        if (b is null) return NotFound();
        b.IrrigationBoardName = model.IrrigationBoardName.Trim();
        b.IrrigationBoardPNumber = model.IrrigationBoardPNumber;
        b.EmailAddress = model.EmailAddress;
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Irrigation board updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteIrrigationBoard(Guid id, CancellationToken ct)
    {
        var b = await _db.IrrigationBoards.FindAsync(new object[] { id }, ct);
        if (b is null) { TempData["Error"] = "Irrigation board not found."; return RedirectToAction(nameof(Index)); }
        if (await _db.FileMasters.AnyAsync(f => f.S33_2_IrrigationBoardId == id, ct)
            || await _db.LetterIssuances.AnyAsync(l => l.IrrigationBoardId == id, ct))
        { TempData["Error"] = $"Cannot delete \"{b.IrrigationBoardName}\": it is referenced by cases or letters."; return RedirectToAction(nameof(Index)); }
        _db.IrrigationBoards.Remove(b);
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Irrigation board deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── Crops ───────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> CreateCrop(CancellationToken ct)
        => View(new CropFormViewModel { AvailableCropTypes = await LoadCropTypes(ct) });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCrop(CropFormViewModel model, CancellationToken ct)
    {
        if (await _db.Crops.AnyAsync(c => c.CropName == model.CropName.Trim(), ct))
            ModelState.AddModelError(nameof(model.CropName), "A crop with this name already exists.");
        if (!ModelState.IsValid) { model.AvailableCropTypes = await LoadCropTypes(ct); return View(model); }

        var crop = new Crop { CropId = Guid.NewGuid(), CropName = model.CropName.Trim() };
        crop.CropType = await ResolveCropType(model.CropTypeId, ct);
        _db.Crops.Add(crop);
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Crop added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> EditCrop(Guid id, CancellationToken ct)
    {
        var c = await _db.Crops.Include(x => x.CropType).FirstOrDefaultAsync(x => x.CropId == id, ct);
        if (c is null) return NotFound();
        return View(new CropFormViewModel
        {
            Id = c.CropId,
            CropName = c.CropName,
            CropTypeId = c.CropType?.CropTypeId,
            AvailableCropTypes = await LoadCropTypes(ct)
        });
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCrop(Guid id, CropFormViewModel model, CancellationToken ct)
    {
        if (id != model.Id) return BadRequest();
        if (await _db.Crops.AnyAsync(c => c.CropId != id && c.CropName == model.CropName.Trim(), ct))
            ModelState.AddModelError(nameof(model.CropName), "A crop with this name already exists.");
        if (!ModelState.IsValid) { model.AvailableCropTypes = await LoadCropTypes(ct); return View(model); }

        var c = await _db.Crops.Include(x => x.CropType).FirstOrDefaultAsync(x => x.CropId == id, ct);
        if (c is null) return NotFound();
        c.CropName = model.CropName.Trim();
        c.CropType = await ResolveCropType(model.CropTypeId, ct);
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Crop updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCrop(Guid id, CancellationToken ct)
    {
        var c = await _db.Crops.FindAsync(new object[] { id }, ct);
        if (c is null) { TempData["Error"] = "Crop not found."; return RedirectToAction(nameof(Index)); }
        if (await _db.FieldAndCrops.AnyAsync(f => f.Crop.CropId == id, ct)
            || await _db.CropWaterRates.AnyAsync(r => r.CropId == id, ct))
        { TempData["Error"] = $"Cannot delete \"{c.CropName}\": it is used by field/crop records or water rates."; return RedirectToAction(nameof(Index)); }
        _db.Crops.Remove(c);
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Crop deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── WaterSources ────────────────────────────────────────

    [HttpGet]
    public IActionResult CreateWaterSource() => View(new WaterSourceFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateWaterSource(WaterSourceFormViewModel model, CancellationToken ct)
    {
        if (await _db.WaterSources.AnyAsync(w => w.WaterSourceName == model.WaterSourceName.Trim(), ct))
            ModelState.AddModelError(nameof(model.WaterSourceName), "A water source with this name already exists.");
        if (!ModelState.IsValid) return View(model);
        _db.WaterSources.Add(new WaterSource
        {
            WaterSourceId = Guid.NewGuid(),
            WaterSourceName = model.WaterSourceName.Trim(),
            WaterSourceType = model.WaterSourceType
        });
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Water source added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> EditWaterSource(Guid id, CancellationToken ct)
    {
        var w = await _db.WaterSources.FindAsync(new object[] { id }, ct);
        if (w is null) return NotFound();
        return View(new WaterSourceFormViewModel
        {
            Id = w.WaterSourceId,
            WaterSourceName = w.WaterSourceName,
            WaterSourceType = w.WaterSourceType
        });
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditWaterSource(Guid id, WaterSourceFormViewModel model, CancellationToken ct)
    {
        if (id != model.Id) return BadRequest();
        if (await _db.WaterSources.AnyAsync(w => w.WaterSourceId != id && w.WaterSourceName == model.WaterSourceName.Trim(), ct))
            ModelState.AddModelError(nameof(model.WaterSourceName), "A water source with this name already exists.");
        if (!ModelState.IsValid) return View(model);
        var w = await _db.WaterSources.FindAsync(new object[] { id }, ct);
        if (w is null) return NotFound();
        w.WaterSourceName = model.WaterSourceName.Trim();
        w.WaterSourceType = model.WaterSourceType;
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Water source updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteWaterSource(Guid id, CancellationToken ct)
    {
        var w = await _db.WaterSources.FindAsync(new object[] { id }, ct);
        if (w is null) { TempData["Error"] = "Water source not found."; return RedirectToAction(nameof(Index)); }
        if (await _db.FieldAndCrops.AnyAsync(f => f.WaterSource.WaterSourceId == id, ct)
            || await _db.Irrigations.AnyAsync(i => i.WaterSourceId == id, ct))
        { TempData["Error"] = $"Cannot delete \"{w.WaterSourceName}\": it is used by field/crop or irrigation records."; return RedirectToAction(nameof(Index)); }
        _db.WaterSources.Remove(w);
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Water source deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── IrrigationSystems ───────────────────────────────────

    [HttpGet]
    public IActionResult CreateIrrigationSystem() => View(new IrrigationSystemFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateIrrigationSystem(IrrigationSystemFormViewModel model, CancellationToken ct)
    {
        if (await _db.IrrigationSystems.AnyAsync(i => i.IrrigationSystemName == model.IrrigationSystemName.Trim(), ct))
            ModelState.AddModelError(nameof(model.IrrigationSystemName), "A system with this name already exists.");
        if (!ModelState.IsValid) return View(model);
        _db.IrrigationSystems.Add(new IrrigationSystem
        {
            IrrigationSystemId = Guid.NewGuid(),
            IrrigationSystemName = model.IrrigationSystemName.Trim(),
            IrrigationSystemDescription = model.IrrigationSystemDescription,
            IrrigationSystemModel = model.IrrigationSystemModel
        });
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Irrigation system added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> EditIrrigationSystem(Guid id, CancellationToken ct)
    {
        var i = await _db.IrrigationSystems.FindAsync(new object[] { id }, ct);
        if (i is null) return NotFound();
        return View(new IrrigationSystemFormViewModel
        {
            Id = i.IrrigationSystemId,
            IrrigationSystemName = i.IrrigationSystemName,
            IrrigationSystemDescription = i.IrrigationSystemDescription,
            IrrigationSystemModel = i.IrrigationSystemModel
        });
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditIrrigationSystem(Guid id, IrrigationSystemFormViewModel model, CancellationToken ct)
    {
        if (id != model.Id) return BadRequest();
        if (await _db.IrrigationSystems.AnyAsync(i => i.IrrigationSystemId != id && i.IrrigationSystemName == model.IrrigationSystemName.Trim(), ct))
            ModelState.AddModelError(nameof(model.IrrigationSystemName), "A system with this name already exists.");
        if (!ModelState.IsValid) return View(model);
        var i = await _db.IrrigationSystems.FindAsync(new object[] { id }, ct);
        if (i is null) return NotFound();
        i.IrrigationSystemName = model.IrrigationSystemName.Trim();
        i.IrrigationSystemDescription = model.IrrigationSystemDescription;
        i.IrrigationSystemModel = model.IrrigationSystemModel;
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Irrigation system updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteIrrigationSystem(Guid id, CancellationToken ct)
    {
        var i = await _db.IrrigationSystems.FindAsync(new object[] { id }, ct);
        if (i is null) { TempData["Error"] = "Irrigation system not found."; return RedirectToAction(nameof(Index)); }
        if (await _db.FieldAndCrops.AnyAsync(f => f.IrrigationSystem != null && f.IrrigationSystem.IrrigationSystemId == id, ct)
            || await _db.CropWaterRates.AnyAsync(r => r.IrrigationSystemId == id, ct))
        { TempData["Error"] = $"Cannot delete \"{i.IrrigationSystemName}\": it is used by field/crop or water-rate records."; return RedirectToAction(nameof(Index)); }
        _db.IrrigationSystems.Remove(i);
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Irrigation system deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── Helpers ─────────────────────────────────────────────

    private async Task<CropType?> ResolveCropType(Guid? cropTypeId, CancellationToken ct)
        => cropTypeId is null ? null : await _db.CropTypes.FindAsync(new object[] { cropTypeId.Value }, ct);

    private async Task<IEnumerable<LookupOption>> LoadWmas(CancellationToken ct)
        => await _db.WaterManagementAreas.OrderBy(w => w.WmaName)
            .Select(w => new LookupOption { Id = w.WmaId, Name = w.WmaName }).ToListAsync(ct);

    private async Task<IEnumerable<LookupOption>> LoadCropTypes(CancellationToken ct)
        => await _db.CropTypes.OrderBy(c => c.CropTypeName)
            .Select(c => new LookupOption { Id = c.CropTypeId, Name = c.CropTypeName }).ToListAsync(ct);
}
