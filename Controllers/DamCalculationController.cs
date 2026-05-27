using dwa_ver_val.Services.Calculator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

[Authorize(Policy = DwsPolicies.CanCapture)]
public class DamCalculationController : Controller
{
    private readonly IDamCalculation _repo;
    private readonly ApplicationDBContext _context;
    private readonly ICalculatorService _calculator;
    private readonly IScopedCaseQuery _scope;

    public DamCalculationController(IDamCalculation repo, ApplicationDBContext context, ICalculatorService calculator, IScopedCaseQuery scope)
    {
        _repo = repo;
        _context = context;
        _calculator = calculator;
        _scope = scope;
    }

    // GET: DamCalculation/Index?propertyId=...
    [HttpGet]
    [Authorize(Policy = DwsPolicies.CanRead)]
    public async Task<IActionResult> Index(Guid propertyId)
    {
        var property = await _context.Properties.FindAsync(propertyId);
        if (property == null) return NotFound();

        var records = await _repo.GetByPropertyIdAsync(propertyId);
        var vms = records.Select(d => new DamCalculationViewModel
        {
            DamCalculationId = d.DamCalculationId,
            PropertyId = d.PropertyId,
            RiverId = d.RiverId,
            SateliteImageId = d.SateliteImageId,
            CalculationDate = d.CalculationDate,
            SateliteSurveyDate = d.SateliteSurveyDate,
            DamNumber = d.DamNumber,
            DamCapacity = d.DamCapacity,
            DamCalculationStatus = d.DamCalculationStatus,
            RiverName = d.River?.RiverName
        }).ToList();

        ViewBag.PropertyId = propertyId;
        ViewBag.PropertyRef = property.PropertyReferenceNumber;
        return View(vms);
    }

    // GET: DamCalculation/Create?propertyId=...
    [HttpGet]
    public async Task<IActionResult> Create(Guid propertyId)
    {
        var property = await _context.Properties.FindAsync(propertyId);
        if (property == null) return NotFound();

        await PopulateDropdownsAsync();
        var vm = new DamCalculationViewModel
        {
            PropertyId = propertyId,
            CalculationDate = DateOnly.FromDateTime(DateTime.Today),
            SateliteSurveyDate = DateOnly.FromDateTime(DateTime.Today),
            DamCalculationStatus = DamCalculationStatus.IN_PROGRESS
        };
        ViewBag.PropertyRef = property.PropertyReferenceNumber;
        return View(vm);
    }

    // POST: DamCalculation/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DamCalculationViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            ViewBag.PropertyRef = (await _context.Properties.FindAsync(vm.PropertyId))?.PropertyReferenceNumber;
            return View(vm);
        }

        var property = await _context.Properties.FindAsync(vm.PropertyId);
        var river = await _context.Rivers.FindAsync(vm.RiverId);

        if (property == null || river == null)
        {
            if (property == null) ModelState.AddModelError("", $"Property with ID {vm.PropertyId} was not found. Return to the case file and try again.");
            if (river == null)    ModelState.AddModelError("RiverId", "The selected River was not found. Please select a valid river.");
            await PopulateDropdownsAsync();
            return View(vm);
        }

        var entity = new DamCalculation
        {
            DamCalculationId = Guid.NewGuid(),
            Property = property,
            PropertyId = vm.PropertyId,
            River = river,
            RiverId = vm.RiverId,
            SateliteImageId = vm.SateliteImageId,
            CalculationDate = vm.CalculationDate,
            SateliteSurveyDate = vm.SateliteSurveyDate,
            DamNumber = vm.DamNumber,
            DamCapacity = vm.DamCapacity,
            DamCalculationStatus = vm.DamCalculationStatus,
            CalculationMethod = vm.CalculationMethod,
            WallLength = vm.WallLength,
            Fetch = vm.Fetch,
            RiverDistance = vm.RiverDistance,
            ContourDifference = vm.ContourDifference,
            DamArea = vm.DamArea,
            DamDepth = vm.DamDepth,
            ShapeFactor = vm.ShapeFactor,
        };

        await _repo.AddCalculationAsync(entity);
        TempData["Success"] = "Dam Calculation record added. Enter Appendix D inputs and click Calculate Capacity.";
        return RedirectToAction(nameof(Edit), new { id = entity.DamCalculationId });
    }

    // GET: DamCalculation/Edit/{id}
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return NotFound();

        await PopulateDropdownsAsync(entity.RiverId);
        var vm = ToViewModel(entity);
        ViewBag.PropertyRef = entity.Property?.PropertyReferenceNumber;
        return View(vm);
    }

    // POST: DamCalculation/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(DamCalculationViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(vm.RiverId);
            return View(vm);
        }

        var existing = await _repo.GetByIdAsync(vm.DamCalculationId);
        if (existing == null) return NotFound();

        var river = await _context.Rivers.FindAsync(vm.RiverId);
        if (river == null)
        {
            ModelState.AddModelError("RiverId", "The selected River was not found. Please select a valid river from the list.");
            await PopulateDropdownsAsync(vm.RiverId);
            return View(vm);
        }

        existing.River = river;
        existing.RiverId = vm.RiverId;
        existing.SateliteImageId = vm.SateliteImageId;
        existing.CalculationDate = vm.CalculationDate;
        existing.SateliteSurveyDate = vm.SateliteSurveyDate;
        existing.DamNumber = vm.DamNumber;
        existing.DamCapacity = vm.DamCapacity;
        existing.DamCalculationStatus = vm.DamCalculationStatus;
        existing.CalculationMethod = vm.CalculationMethod;
        existing.WallLength = vm.WallLength;
        existing.Fetch = vm.Fetch;
        existing.RiverDistance = vm.RiverDistance;
        existing.ContourDifference = vm.ContourDifference;
        existing.DamArea = vm.DamArea;
        existing.DamDepth = vm.DamDepth;
        existing.ShapeFactor = vm.ShapeFactor;

        await _repo.UpdateCalculationAsync(existing);
        TempData["Success"] = "Dam Calculation record updated successfully.";
        return RedirectToAction(nameof(Index), new { propertyId = existing.PropertyId });
    }

    // POST: DamCalculation/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanTransitionWorkflow)]
    public async Task<IActionResult> Delete(Guid id, Guid propertyId)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null)
        {
            TempData["Error"] = "Dam Calculation record not found — it may have already been deleted.";
            return RedirectToAction(nameof(Index), new { propertyId });
        }
        var entityPropertyId = entity.PropertyId;
        await _repo.DeleteAsync(id);
        TempData["Success"] = "Dam Calculation record deleted.";
        return RedirectToAction(nameof(Index), new { propertyId = entityPropertyId });
    }

    // POST: DamCalculation/Calculate/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCapture)]
    public async Task<IActionResult> Calculate(Guid id)
    {
        var entity = await _context.DamCalculations.FindAsync(id);
        if (entity is null) return NotFound();

        var fileMaster = await _context.FileMasters.FirstOrDefaultAsync(fm => fm.PropertyId == entity.PropertyId);
        if (fileMaster is null) return NotFound();
        if (!_scope.IsInScope(fileMaster, User)) return Forbid();

        try
        {
            var capacity = await _calculator.ComputeDamVolumeAsync(id);
            TempData["Success"] = $"Dam capacity calculated: {capacity.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} m³";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Edit), new { id });
    }

    private async Task PopulateDropdownsAsync(Guid? selectedRiverId = null)
    {
        ViewBag.Rivers = new SelectList(
            await _context.Rivers.OrderBy(r => r.RiverName).ToListAsync(),
            "RiverId", "RiverName", selectedRiverId);

        ViewBag.StatusList = new SelectList(
            Enum.GetValues<DamCalculationStatus>()
                .Select(s => new { Value = s.ToString(), Text = s.ToString().Replace("_", " ") }),
            "Value", "Text");
    }

    private static DamCalculationViewModel ToViewModel(DamCalculation d) => new()
    {
        DamCalculationId = d.DamCalculationId,
        PropertyId = d.PropertyId,
        RiverId = d.RiverId,
        SateliteImageId = d.SateliteImageId,
        CalculationDate = d.CalculationDate,
        SateliteSurveyDate = d.SateliteSurveyDate,
        DamNumber = d.DamNumber,
        DamCapacity = d.DamCapacity,
        DamCalculationStatus = d.DamCalculationStatus,
        RiverName = d.River?.RiverName,
        // new input fields:
        CalculationMethod = d.CalculationMethod,
        WallLength = d.WallLength,
        Fetch = d.Fetch,
        RiverDistance = d.RiverDistance,
        ContourDifference = d.ContourDifference,
        DamArea = d.DamArea,
        DamDepth = d.DamDepth,
        ShapeFactor = d.ShapeFactor,
    };
}
