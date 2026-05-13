using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

[Authorize(Policy = DwsPolicies.CanTransitionWorkflow)]
public class DamCalculationController : Controller
{
    private readonly IDamCalculation _repo;
    private readonly ApplicationDBContext _context;

    public DamCalculationController(IDamCalculation repo, ApplicationDBContext context)
    {
        _repo = repo;
        _context = context;
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
            ModelState.AddModelError("", "Property or River not found.");
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
            DamCalculationStatus = vm.DamCalculationStatus
        };

        await _repo.AddCalculationAsync(entity);
        return RedirectToAction(nameof(Index), new { propertyId = vm.PropertyId });
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
            ModelState.AddModelError("RiverId", "River not found.");
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

        await _repo.UpdateCalculationAsync(existing);
        return RedirectToAction(nameof(Index), new { propertyId = existing.PropertyId });
    }

    // POST: DamCalculation/Delete/{id}
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
        RiverName = d.River?.RiverName
    };
}
