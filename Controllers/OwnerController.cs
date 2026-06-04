using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// BUG-030: Controller-level policy changed from CanAdminister (SystemAdmin only) to
// CanRead (all authenticated staff), so NationalManager and other read roles can view
// property owner lists and details. Mutating actions (Register, Edit, Delete) carry an
// additional action-level [Authorize(Policy = DwsPolicies.CanCapture)] — AtLeastCapturer.
[Authorize(Policy = DwsPolicies.CanRead)]
public class OwnerController : Controller
{
    private readonly ApplicationDBContext _context;
    private readonly ILogger<OwnerController> _logger;

    public OwnerController(ApplicationDBContext context, ILogger<OwnerController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // READ — covered by controller-level CanRead
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var owners = await _context.PropertyOwners
            .Include(o => o.Address)
            .OrderBy(o => o.LastName).ThenBy(o => o.FirstName)
            .ToListAsync();
        return View(owners);
    }

    // MUTATE — Capturer+ required for Register/Edit/Delete
    [HttpGet]
    [Authorize(Policy = DwsPolicies.CanCapture)]
    public async Task<IActionResult> Register()
    {
        await PopulateDropdownsAsync();
        return View(new PropertyOwner { FirstName = string.Empty, LastName = string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCapture)]
    public async Task<IActionResult> Register(PropertyOwner owner)
    {
        if (ModelState.IsValid)
        {
            owner.OwnerId = Guid.NewGuid();
            if (owner.Address != null)
                owner.Address.AddressId = Guid.NewGuid();
            _context.PropertyOwners.Add(owner);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Owner {owner.FirstName} {owner.LastName} registered successfully.";
            return RedirectToAction(nameof(Index));
        }
        await PopulateDropdownsAsync();
        return View(owner);
    }

    [HttpGet]
    [Authorize(Policy = DwsPolicies.CanCapture)]
    public async Task<IActionResult> Edit(Guid id)
    {
        var owner = await _context.PropertyOwners
            .Include(o => o.Address)
            .FirstOrDefaultAsync(o => o.OwnerId == id);
        if (owner == null) return NotFound();
        await PopulateDropdownsAsync();
        return View(owner);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCapture)]
    public async Task<IActionResult> Edit(Guid id, PropertyOwner owner)
    {
        if (id != owner.OwnerId) return BadRequest();
        if (ModelState.IsValid)
        {
            _context.PropertyOwners.Update(owner);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Owner {owner.FirstName} {owner.LastName} updated.";
            return RedirectToAction(nameof(Index));
        }
        await PopulateDropdownsAsync();
        return View(owner);
    }

    [HttpGet]
    [Authorize(Policy = DwsPolicies.CanCapture)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var owner = await _context.PropertyOwners
            .Include(o => o.Address)
            .FirstOrDefaultAsync(o => o.OwnerId == id);
        if (owner == null) return NotFound();

        var ownershipCount = await _context.PropertyOwnerships.CountAsync(po => po.PropertyOwnerId == id);
        ViewBag.OwnershipCount = ownershipCount;
        ViewBag.CanDelete = ownershipCount == 0;

        return View(owner);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCapture)]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var ownershipCount = await _context.PropertyOwnerships.CountAsync(po => po.PropertyOwnerId == id);
        if (ownershipCount > 0)
        {
            TempData["Error"] = $"Cannot delete — this owner has {ownershipCount} property ownership record(s). Remove those first.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        var owner = await _context.PropertyOwners.FindAsync(id);
        if (owner == null) return NotFound();

        _context.PropertyOwners.Remove(owner);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Owner {owner.FirstName} {owner.LastName} deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync()
    {
        ViewBag.CustomerTypes = await _context.CustomerTypes.OrderBy(c => c.CustomerTypeName).ToListAsync();
    }
}
