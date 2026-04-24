using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

[Authorize(Policy = DwsPolicies.CanRead)]
public class PropertyController : Controller
{
    private readonly ILogger<PropertyController> _logger;
    private readonly IPropertyInterface _propertyRepository;
    private readonly ApplicationDBContext _context;

    public PropertyController(
        ILogger<PropertyController> logger,
        IPropertyInterface propertyRepository,
        ApplicationDBContext context)
    {
        _logger = logger;
        _propertyRepository = propertyRepository;
        _context = context;
    }

    // GET: Property
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var properties = await _propertyRepository.ListAllAsync();
        return View(properties);
    }

    // GET: Property/Register
    [HttpGet]
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
    public async Task<IActionResult> Register()
    {
        ViewBag.WaterManagementAreas = new SelectList(
            await _context.WaterManagementAreas.OrderBy(w => w.WmaName).ToListAsync(),
            "WmaId", "WmaName");

        ViewBag.CatchmentAreas = new SelectList(
            await _context.CatchmentAreas.OrderBy(c => c.CatchmentCode).ToListAsync(),
            "CatchmentAreaId", "CatchmentCode");

        return View();
    }

    // POST: Property/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
    public async Task<IActionResult> Register(Property property)
    {
        if (ModelState.IsValid)
        {
            await _propertyRepository.AddAsync(property);
            return RedirectToAction(nameof(Index));
        }

        ViewBag.WaterManagementAreas = new SelectList(
            await _context.WaterManagementAreas.OrderBy(w => w.WmaName).ToListAsync(),
            "WmaId", "WmaName");

        ViewBag.CatchmentAreas = new SelectList(
            await _context.CatchmentAreas.OrderBy(c => c.CatchmentCode).ToListAsync(),
            "CatchmentAreaId", "CatchmentCode");

        return View(property);
    }

    // GET: Property/Edit/{id}
    [HttpGet]
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
    public async Task<IActionResult> Edit(Guid id)
    {
        var property = await _propertyRepository.GetByIdAsync(id);

        if (property == null)
        {
            return NotFound();
        }

        ViewBag.WaterManagementAreas = new SelectList(
            await _context.WaterManagementAreas.OrderBy(w => w.WmaName).ToListAsync(),
            "WmaId", "WmaName", property.WmaId);

        ViewBag.CatchmentAreas = new SelectList(
            await _context.CatchmentAreas.OrderBy(c => c.CatchmentCode).ToListAsync(),
            "CatchmentAreaId", "CatchmentCode", property.CatchmentAreaId);

        return View(property);
    }

    // POST: Property/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
    public async Task<IActionResult> Edit(Guid id, Property property)
    {
        if (id != property.PropertyId)
        {
            return BadRequest();
        }

        if (ModelState.IsValid)
        {
            await _propertyRepository.UpdateAsync(property);
            return RedirectToAction(nameof(Index));
        }

        ViewBag.WaterManagementAreas = new SelectList(
            await _context.WaterManagementAreas.OrderBy(w => w.WmaName).ToListAsync(),
            "WmaId", "WmaName", property.WmaId);

        ViewBag.CatchmentAreas = new SelectList(
            await _context.CatchmentAreas.OrderBy(c => c.CatchmentCode).ToListAsync(),
            "CatchmentAreaId", "CatchmentCode", property.CatchmentAreaId);

        return View(property);
    }

    // GET: Property/Details/{id}
    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var property = await _propertyRepository.GetByIdAsync(id);

        if (property == null)
        {
            return NotFound();
        }

        return View(property);
    }

    // GET: Property/Delete/{id}
    [HttpGet]
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var property = await _propertyRepository.GetByIdAsync(id);

        if (property == null)
        {
            return NotFound();
        }

        return View(property);
    }

    // POST: Property/Delete/{id}
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var property = await _propertyRepository.DeleteAsync(id);

        if (property == null)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }
}
