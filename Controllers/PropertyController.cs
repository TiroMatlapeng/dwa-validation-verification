using System.Security.Claims;
using dwa_ver_val.Services.Audit;
using dwa_ver_val.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

[Authorize(Policy = DwsPolicies.CanRead)]
public class PropertyController : Controller
{
    private const string StatusActive = "Active";
    private const string StatusSubdivided = "Subdivided";
    private const string StatusConsolidated = "Consolidated";

    private readonly ILogger<PropertyController> _logger;
    private readonly IPropertyInterface _propertyRepository;
    private readonly ApplicationDBContext _context;
    private readonly IScopedCaseQuery _scope;
    private readonly IAuditService _audit;

    public PropertyController(
        ILogger<PropertyController> logger,
        IPropertyInterface propertyRepository,
        ApplicationDBContext context,
        IScopedCaseQuery scope,
        IAuditService audit)
    {
        _logger = logger;
        _propertyRepository = propertyRepository;
        _context = context;
        _scope = scope;
        _audit = audit;
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
        var property = await _context.Properties
            .Include(p => p.Address)
            .Include(p => p.WaterManagementArea)
            .Include(p => p.CatchmentArea)
            .Include(p => p.FileMasters)
            .Include(p => p.ParentProperty)
            .Include(p => p.SuccessorProperty)
            .Include(p => p.ChildProperties)
            .FirstOrDefaultAsync(p => p.PropertyId == id);

        if (property == null)
        {
            return NotFound();
        }

        // Properties whose Successor points back here (consolidation predecessors).
        var predecessors = await _context.Properties
            .AsNoTracking()
            .Where(p => p.SuccessorPropertyId == id)
            .OrderBy(p => p.SGCode)
            .ToListAsync();

        ViewBag.Lineage = new PropertyLineageViewModel
        {
            Parent = property.ParentProperty,
            Successor = property.SuccessorProperty,
            Children = property.ChildProperties.OrderBy(c => c.SGCode).ToList(),
            ConsolidationPredecessors = predecessors
        };

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

    // GET: Property/Subdivide/{id}
    [HttpGet]
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
    public async Task<IActionResult> Subdivide(Guid id)
    {
        var property = await _context.Properties
            .Include(p => p.WaterManagementArea)
            .FirstOrDefaultAsync(p => p.PropertyId == id);

        if (property == null) return NotFound();
        if (!IsInScope(property)) return Forbid();

        if (property.PropertyStatus is StatusSubdivided or StatusConsolidated)
        {
            TempData["Error"] = $"Property {property.SGCode} is already {property.PropertyStatus} and cannot be subdivided.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var vm = new SubdivideViewModel
        {
            ParentProperty = property,
            Children = new List<SubdivideChildRow> { new(), new(), new() }
        };
        return View(vm);
    }

    // POST: Property/Subdivide/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
    public async Task<IActionResult> Subdivide(Guid id, SubdivideViewModel form)
    {
        var parent = await _context.Properties.FirstOrDefaultAsync(p => p.PropertyId == id);
        if (parent == null) return NotFound();
        if (!IsInScope(parent)) return Forbid();

        if (parent.PropertyStatus is StatusSubdivided or StatusConsolidated)
        {
            TempData["Error"] = $"Property {parent.SGCode} is already {parent.PropertyStatus} and cannot be subdivided.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var validRows = (form.Children ?? new List<SubdivideChildRow>())
            .Where(r => !string.IsNullOrWhiteSpace(r.SGCode))
            .ToList();

        if (validRows.Count < 2)
        {
            ModelState.AddModelError(string.Empty, "Subdivision requires at least 2 child properties.");
        }

        foreach (var row in validRows)
        {
            if (row.PropertySize <= 0m)
            {
                ModelState.AddModelError(string.Empty, $"Child '{row.SGCode}' must have a property size greater than zero.");
            }
        }

        if (!ModelState.IsValid)
        {
            form.ParentProperty = parent;
            return View(form);
        }

        var newChildren = validRows.Select(row => new Property
        {
            PropertyId = Guid.NewGuid(),
            SGCode = row.SGCode,
            PropertyReferenceNumber = row.PropertyReferenceNumber,
            PropertySize = row.PropertySize,
            WmaId = parent.WmaId,
            CatchmentAreaId = parent.CatchmentAreaId,
            QuaternaryDrainage = parent.QuaternaryDrainage,
            // MVP: children inherit the parent's AddressId (FK reuse, not deep-clone of the
            // Address row). Per-child distinct addresses are a follow-up; capture via the
            // Property/Edit page after subdivision lands.
            AddressId = parent.AddressId,
            PropertyStatus = StatusActive,
            ParentPropertyId = parent.PropertyId
        }).ToList();

        var previousStatus = parent.PropertyStatus ?? StatusActive;
        parent.PropertyStatus = StatusSubdivided;
        _context.Properties.AddRange(newChildren);

        var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;
        try
        {
            await _context.SaveChangesAsync();

            var userId = GetUserId();
            var userName = User.Identity?.Name;

            await _audit.LogAsync(new AuditEvent(
                EntityType: nameof(Property),
                EntityId: parent.PropertyId.ToString(),
                Action: "PropertySubdivided",
                UserId: userId,
                UserDisplayName: userName,
                FromValue: previousStatus,
                ToValue: StatusSubdivided,
                Reason: $"Subdivided into {newChildren.Count} properties: {string.Join(", ", newChildren.Select(c => c.SGCode))}"));

            foreach (var child in newChildren)
            {
                await _audit.LogAsync(new AuditEvent(
                    EntityType: nameof(Property),
                    EntityId: child.PropertyId.ToString(),
                    Action: "PropertyCreated",
                    UserId: userId,
                    UserDisplayName: userName,
                    ToValue: StatusActive,
                    Reason: $"Created by subdivision of {parent.SGCode} ({parent.PropertyId})"));
            }

            if (transaction is not null) await transaction.CommitAsync();
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }

        TempData["Success"] = $"Subdivided {parent.SGCode} into {newChildren.Count} new properties.";
        return RedirectToAction(nameof(Details), new { id = parent.PropertyId });
    }

    // GET: Property/Consolidate
    [HttpGet]
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
    public async Task<IActionResult> Consolidate()
    {
        var available = await LoadConsolidationCandidatesAsync();
        return View(new ConsolidateViewModel { AvailableProperties = available });
    }

    // POST: Property/Consolidate
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
    public async Task<IActionResult> Consolidate(ConsolidateViewModel form)
    {
        var sourceIds = form.Sources?.Distinct().ToArray() ?? Array.Empty<Guid>();

        if (sourceIds.Length < 2)
        {
            ModelState.AddModelError(nameof(form.Sources), "Select at least 2 source properties to consolidate.");
            form.AvailableProperties = await LoadConsolidationCandidatesAsync();
            return View(form);
        }

        var sources = await _scope
            .FilterProperties(_context.Properties.AsQueryable(), User)
            .Where(p => sourceIds.Contains(p.PropertyId))
            .ToListAsync();

        if (sources.Count != sourceIds.Length)
        {
            ModelState.AddModelError(nameof(form.Sources), "One or more selected properties were not found in your scope.");
            form.AvailableProperties = await LoadConsolidationCandidatesAsync();
            return View(form);
        }

        if (sources.Any(p => p.PropertyStatus is not null && p.PropertyStatus != StatusActive))
        {
            ModelState.AddModelError(nameof(form.Sources), "Only Active properties can be consolidated.");
            form.AvailableProperties = await LoadConsolidationCandidatesAsync();
            return View(form);
        }

        var distinctWmas = sources.Select(p => p.WmaId).Distinct().ToList();
        if (distinctWmas.Count > 1)
        {
            ModelState.AddModelError(nameof(form.Sources), "All selected properties must belong to the same Water Management Area.");
            form.AvailableProperties = await LoadConsolidationCandidatesAsync();
            return View(form);
        }

        if (!ModelState.IsValid)
        {
            form.AvailableProperties = await LoadConsolidationCandidatesAsync();
            return View(form);
        }

        var anchor = sources[0];
        var totalSize = sources.Sum(p => p.PropertySize);
        var newProperty = new Property
        {
            PropertyId = Guid.NewGuid(),
            SGCode = form.SGCode,
            PropertyReferenceNumber = form.PropertyReferenceNumber,
            PropertySize = form.OverridePropertySize ?? totalSize,
            WmaId = anchor.WmaId,
            CatchmentAreaId = anchor.CatchmentAreaId,
            QuaternaryDrainage = anchor.QuaternaryDrainage,
            AddressId = anchor.AddressId,
            PropertyStatus = StatusActive
        };
        _context.Properties.Add(newProperty);

        foreach (var src in sources)
        {
            src.SuccessorPropertyId = newProperty.PropertyId;
            src.PropertyStatus = StatusConsolidated;
        }

        var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;
        try
        {
            await _context.SaveChangesAsync();

            var userId = GetUserId();
            var userName = User.Identity?.Name;

            await _audit.LogAsync(new AuditEvent(
                EntityType: nameof(Property),
                EntityId: newProperty.PropertyId.ToString(),
                Action: "PropertyCreated",
                UserId: userId,
                UserDisplayName: userName,
                ToValue: StatusActive,
                Reason: $"Created by consolidation of {sources.Count} properties: {string.Join(", ", sources.Select(s => s.SGCode))}"));

            foreach (var src in sources)
            {
                await _audit.LogAsync(new AuditEvent(
                    EntityType: nameof(Property),
                    EntityId: src.PropertyId.ToString(),
                    Action: "PropertyConsolidated",
                    UserId: userId,
                    UserDisplayName: userName,
                    FromValue: StatusActive,
                    ToValue: StatusConsolidated,
                    Reason: $"Consolidated into {newProperty.SGCode} ({newProperty.PropertyId})"));
            }

            if (transaction is not null) await transaction.CommitAsync();
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }

        TempData["Success"] = $"Consolidated {sources.Count} properties into {newProperty.SGCode}.";
        return RedirectToAction(nameof(Details), new { id = newProperty.PropertyId });
    }

    private Task<List<Property>> LoadConsolidationCandidatesAsync() =>
        _scope.FilterProperties(_context.Properties.AsQueryable(), User)
            .Where(p => p.PropertyStatus == StatusActive || p.PropertyStatus == null)
            .Include(p => p.WaterManagementArea)
            .OrderBy(p => p.SGCode)
            .AsNoTracking()
            .ToListAsync();

    private bool IsInScope(Property property)
    {
        if (User.IsInRole(DwsRoles.SystemAdmin) || User.IsInRole(DwsRoles.NationalManager))
            return true;
        var wmaClaim = User.FindFirst("wmaId")?.Value;
        if (!Guid.TryParse(wmaClaim, out var wmaId)) return false;
        return property.WmaId == wmaId;
    }

    private Guid? GetUserId()
    {
        var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
