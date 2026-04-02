using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

public class FileMasterController : Controller
{
    private readonly IFileMaster _fileMasterRepository;
    private readonly ApplicationDBContext _context;

    public FileMasterController(IFileMaster fileMasterRepository, ApplicationDBContext context)
    {
        _fileMasterRepository = fileMasterRepository;
        _context = context;
    }

    // GET: FileMaster
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var fileMasters = await _fileMasterRepository.ListAllAsync();
        return View(fileMasters);
    }

    // GET: FileMaster/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync();
        return View();
    }

    // POST: FileMaster/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FileMaster fileMaster)
    {
        if (ModelState.IsValid)
        {
            await _fileMasterRepository.AddAsync(fileMaster);
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync();
        return View(fileMaster);
    }

    // GET: FileMaster/Edit/{id}
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var fileMaster = await _fileMasterRepository.GetByIdAsync(id);
        if (fileMaster == null)
            return NotFound();

        await PopulateDropdownsAsync(fileMaster);
        return View(fileMaster);
    }

    // POST: FileMaster/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, FileMaster fileMaster)
    {
        if (id != fileMaster.FileMasterId)
            return BadRequest();

        if (ModelState.IsValid)
        {
            await _fileMasterRepository.UpdateAsync(fileMaster);
            return RedirectToAction(nameof(Index));
        }

        await PopulateDropdownsAsync(fileMaster);
        return View(fileMaster);
    }

    // GET: FileMaster/Details/{id}
    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var fileMaster = await _fileMasterRepository.GetByIdAsync(id);
        if (fileMaster == null)
            return NotFound();

        return View(fileMaster);
    }

    // GET: FileMaster/Delete/{id}
    [HttpGet]
    public async Task<IActionResult> Delete(Guid id)
    {
        var fileMaster = await _fileMasterRepository.GetByIdAsync(id);
        if (fileMaster == null)
            return NotFound();

        return View(fileMaster);
    }

    // POST: FileMaster/Delete/{id}
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        await _fileMasterRepository.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync(FileMaster? fileMaster = null)
    {
        var properties = await _context.Properties
            .Select(p => new { p.PropertyId, Display = p.SGCode ?? p.PropertyReferenceNumber ?? p.PropertyId.ToString() })
            .ToListAsync();
        ViewBag.Properties = new SelectList(properties, "PropertyId", "Display", fileMaster?.PropertyId);

        ViewBag.OrgUnits = new SelectList(
            await _context.OrganisationalUnits.OrderBy(o => o.Name).ToListAsync(),
            "OrgUnitId", "Name", fileMaster?.OrgUnitId);

        var users = await _context.ApplicationUsers
            .Select(u => new { u.ApplicationUserId, Display = u.FirstName + " " + u.LastName })
            .ToListAsync();
        ViewBag.Validators = new SelectList(users, "ApplicationUserId", "Display", fileMaster?.ValidatorId);
        ViewBag.CapturePersons = new SelectList(users, "ApplicationUserId", "Display", fileMaster?.CapturePersonId);

        ViewBag.CatchmentAreas = new SelectList(
            await _context.CatchmentAreas.OrderBy(c => c.CatchmentCode).ToListAsync(),
            "CatchmentAreaId", "CatchmentCode", fileMaster?.CatchmentAreaId);

        ViewBag.AssessmentTracks = new SelectList(
            new[]
            {
                new { Value = "S35_Verification", Text = "S35 Verification" },
                new { Value = "S33_2_Declaration", Text = "S33(2) Declaration" },
                new { Value = "S33_3_Declaration", Text = "S33(3) Declaration" }
            },
            "Value", "Text", fileMaster?.AssessmentTrack);
    }
}
