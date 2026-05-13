using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

[Authorize(Policy = DwsPolicies.CanTransitionWorkflow)]
public class FieldAndCropController : Controller
{
    private readonly IFieldAndCrop _repo;
    private readonly ApplicationDBContext _context;

    public FieldAndCropController(IFieldAndCrop repo, ApplicationDBContext context)
    {
        _repo = repo;
        _context = context;
    }

    // GET: FieldAndCrop/Index?propertyId=...
    [HttpGet]
    [Authorize(Policy = DwsPolicies.CanRead)]
    public async Task<IActionResult> Index(Guid propertyId)
    {
        var property = await _context.Properties.FindAsync(propertyId);
        if (property == null) return NotFound();

        var records = await _repo.GetByPropertyIdAsync(propertyId);
        var vms = records.Select(f => new FieldAndCropViewModel
        {
            FieldAndCropId = f.FieldAndCropId,
            PropertyId = f.PropertyId,
            PeriodId = f.PeriodId,
            FieldNumber = f.FieldNumber,
            FieldArea = f.FieldArea,
            PlantDate = f.PlantDate,
            RotationFactor = f.RotationFactor,
            CropArea = f.CropArea,
            SAPWATCalculationResult = f.SAPWATCalculationResult,
            CropName = f.Crop?.CropName,
            WaterSourceName = f.WaterSource?.WaterSourceName,
            PeriodName = f.Period?.PeriodName
        }).ToList();

        ViewBag.PropertyId = propertyId;
        ViewBag.PropertyRef = property.PropertyReferenceNumber;
        return View(vms);
    }

    // GET: FieldAndCrop/Create?propertyId=...
    [HttpGet]
    public async Task<IActionResult> Create(Guid propertyId)
    {
        var property = await _context.Properties.FindAsync(propertyId);
        if (property == null) return NotFound();

        await PopulateDropdownsAsync();
        var vm = new FieldAndCropViewModel { PropertyId = propertyId };
        ViewBag.PropertyRef = property.PropertyReferenceNumber;
        return View(vm);
    }

    // POST: FieldAndCrop/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FieldAndCropViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            ViewBag.PropertyRef = (await _context.Properties.FindAsync(vm.PropertyId))?.PropertyReferenceNumber;
            return View(vm);
        }

        var crop = await _context.Crops.FindAsync(vm.CropId);
        var waterSource = await _context.WaterSources.FindAsync(vm.WaterSourceId);
        var property = await _context.Properties.FindAsync(vm.PropertyId);
        var period = await _context.Periods.FindAsync(vm.PeriodId);

        if (crop == null || waterSource == null || property == null || period == null)
        {
            ModelState.AddModelError("", "One or more required lookup values were not found.");
            await PopulateDropdownsAsync();
            return View(vm);
        }

        IrrigationSystem? irrigation = vm.IrrigationSystemId.HasValue
            ? await _context.IrrigationSystems.FindAsync(vm.IrrigationSystemId.Value)
            : null;

        var entity = new FieldAndCrop
        {
            FieldAndCropId = Guid.NewGuid(),
            Property = property,
            PropertyId = vm.PropertyId,
            Period = period,
            PeriodId = vm.PeriodId,
            Crop = crop,
            WaterSource = waterSource,
            IrrigationSystem = irrigation,
            FieldNumber = vm.FieldNumber,
            FieldArea = vm.FieldArea,
            PlantDate = vm.PlantDate,
            RotationFactor = vm.RotationFactor,
            CropArea = vm.CropArea,
            SAPWATCalculationResult = vm.SAPWATCalculationResult
        };

        await _repo.AddFieldAndCrop(entity);
        return RedirectToAction(nameof(Index), new { propertyId = vm.PropertyId });
    }

    // GET: FieldAndCrop/Edit/{id}
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return NotFound();

        await PopulateDropdownsAsync(entity);
        var vm = ToViewModel(entity);
        ViewBag.PropertyRef = entity.Property?.PropertyReferenceNumber;
        return View(vm);
    }

    // POST: FieldAndCrop/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(FieldAndCropViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(vm);
        }

        var existing = await _repo.GetByIdAsync(vm.FieldAndCropId);
        if (existing == null) return NotFound();

        var crop = await _context.Crops.FindAsync(vm.CropId);
        var waterSource = await _context.WaterSources.FindAsync(vm.WaterSourceId);
        var period = await _context.Periods.FindAsync(vm.PeriodId);

        if (crop == null || waterSource == null || period == null)
        {
            ModelState.AddModelError("", "One or more required lookup values were not found.");
            await PopulateDropdownsAsync(existing);
            return View(vm);
        }

        existing.Crop = crop;
        existing.WaterSource = waterSource;
        existing.Period = period;
        existing.PeriodId = vm.PeriodId;
        existing.IrrigationSystem = vm.IrrigationSystemId.HasValue
            ? await _context.IrrigationSystems.FindAsync(vm.IrrigationSystemId.Value)
            : null;
        existing.FieldNumber = vm.FieldNumber;
        existing.FieldArea = vm.FieldArea;
        existing.PlantDate = vm.PlantDate;
        existing.RotationFactor = vm.RotationFactor;
        existing.CropArea = vm.CropArea;
        existing.SAPWATCalculationResult = vm.SAPWATCalculationResult;

        await _repo.UpdateFieldAndCrop(existing);
        return RedirectToAction(nameof(Index), new { propertyId = existing.PropertyId });
    }

    // POST: FieldAndCrop/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return NotFound();
        var propertyId = entity.PropertyId;
        await _repo.DeleteAsync(id);
        return RedirectToAction(nameof(Index), new { propertyId });
    }

    private async Task PopulateDropdownsAsync(FieldAndCrop? selected = null)
    {
        ViewBag.Crops = new SelectList(
            await _context.Crops.OrderBy(c => c.CropName).ToListAsync(),
            "CropId", "CropName", selected?.Crop?.CropId);

        ViewBag.WaterSources = new SelectList(
            await _context.WaterSources.OrderBy(w => w.WaterSourceName).ToListAsync(),
            "WaterSourceId", "WaterSourceName", selected?.WaterSource?.WaterSourceId);

        ViewBag.IrrigationSystems = new SelectList(
            await _context.IrrigationSystems.OrderBy(i => i.IrrigationSystemName).ToListAsync(),
            "IrrigationSystemId", "IrrigationSystemName", selected?.IrrigationSystem?.IrrigationSystemId);

        ViewBag.Periods = new SelectList(
            await _context.Periods.OrderBy(p => p.PeriodName).ToListAsync(),
            "PeriodId", "PeriodName", selected?.Period?.PeriodId);
    }

    private static FieldAndCropViewModel ToViewModel(FieldAndCrop f) => new()
    {
        FieldAndCropId = f.FieldAndCropId,
        PropertyId = f.PropertyId,
        PeriodId = f.PeriodId,
        CropId = f.Crop?.CropId ?? Guid.Empty,
        WaterSourceId = f.WaterSource?.WaterSourceId ?? Guid.Empty,
        IrrigationSystemId = f.IrrigationSystem?.IrrigationSystemId,
        FieldNumber = f.FieldNumber,
        FieldArea = f.FieldArea,
        PlantDate = f.PlantDate,
        RotationFactor = f.RotationFactor,
        CropArea = f.CropArea,
        SAPWATCalculationResult = f.SAPWATCalculationResult,
        CropName = f.Crop?.CropName,
        WaterSourceName = f.WaterSource?.WaterSourceName,
        PeriodName = f.Period?.PeriodName
    };
}
