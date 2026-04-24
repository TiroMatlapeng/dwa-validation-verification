using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

[Authorize(Policy = DwsPolicies.CanRead)]
public class FileMasterController : Controller
{
    private readonly IFileMaster _fileMasterRepository;
    private readonly ApplicationDBContext _context;
    private readonly IWorkflowService _workflow;
    private readonly IScopedCaseQuery _scope;

    public FileMasterController(
        IFileMaster fileMasterRepository,
        ApplicationDBContext context,
        IWorkflowService workflow,
        IScopedCaseQuery scope)
    {
        _fileMasterRepository = fileMasterRepository;
        _context = context;
        _workflow = workflow;
        _scope = scope;
    }

    // GET: FileMaster
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var query = _scope.FilterFileMasters(_context.FileMasters.AsQueryable(), User);
        var fileMasters = await query
            .Include(fm => fm.Property)
            .OrderBy(fm => fm.FileNumber)
            .ToListAsync();
        return View(fileMasters);
    }

    // GET: FileMaster/Create
    [HttpGet]
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync();
        return View();
    }

    // POST: FileMaster/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
    public async Task<IActionResult> Create(FileMaster fileMaster)
    {
        if (ModelState.IsValid)
        {
            var created = await _fileMasterRepository.AddAsync(fileMaster);
            await _workflow.StartWorkflowAsync(created.FileMasterId);
            return RedirectToAction(nameof(Details), new { id = created.FileMasterId });
        }

        await PopulateDropdownsAsync();
        return View(fileMaster);
    }

    // GET: FileMaster/Edit/{id}
    [HttpGet]
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
    public async Task<IActionResult> Edit(Guid id)
    {
        var fileMaster = await _fileMasterRepository.GetByIdAsync(id);
        if (fileMaster == null)
            return NotFound();
        if (!_scope.IsInScope(fileMaster, User))
            return Forbid();

        await PopulateDropdownsAsync(fileMaster);
        return View(fileMaster);
    }

    // POST: FileMaster/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
    public async Task<IActionResult> Edit(Guid id, FileMaster fileMaster)
    {
        if (id != fileMaster.FileMasterId)
            return BadRequest();

        var existing = await _fileMasterRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();
        if (!_scope.IsInScope(existing, User))
            return Forbid();

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
        var fileMaster = await _fileMasterRepository.GetWithWorkflowAsync(id);
        if (fileMaster == null) return NotFound();
        if (!_scope.IsInScope(fileMaster, User)) return Forbid();

        var vm = new FileMasterDetailsViewModel { FileMaster = fileMaster };

        if (fileMaster.WorkflowInstanceId.HasValue)
        {
            vm.WorkflowInstance = await _context.WorkflowInstances
                .Include(w => w.CurrentWorkflowState)
                .FirstOrDefaultAsync(w => w.WorkflowInstanceId == fileMaster.WorkflowInstanceId);

            vm.History = await _workflow.GetHistoryAsync(fileMaster.WorkflowInstanceId.Value) is IReadOnlyList<WorkflowStepRecord> list
                ? list.ToList()
                : new List<WorkflowStepRecord>();
        }

        vm.AllStates = await _context.WorkflowStates.OrderBy(s => s.DisplayOrder).ToListAsync();
        vm.Letters = fileMaster.LetterIssuances.OrderBy(l => l.IssuedDate).ToList();

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanTransitionWorkflow)]
    public async Task<IActionResult> AdvanceWorkflow(Guid id, string? notes)
    {
        var fm = await _fileMasterRepository.GetByIdAsync(id);
        if (fm == null) return NotFound();
        if (!_scope.IsInScope(fm, User)) return Forbid();

        try
        {
            await _workflow.AdvanceAsync(id, userId: null, notes: notes);
        }
        catch (InvalidOperationException ex)
        {
            TempData["WorkflowError"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // GET: FileMaster/Delete/{id}
    [HttpGet]
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var fileMaster = await _fileMasterRepository.GetByIdAsync(id);
        if (fileMaster == null)
            return NotFound();
        if (!_scope.IsInScope(fileMaster, User))
            return Forbid();

        return View(fileMaster);
    }

    // POST: FileMaster/Delete/{id}
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var fileMaster = await _fileMasterRepository.GetByIdAsync(id);
        if (fileMaster == null)
            return NotFound();
        if (!_scope.IsInScope(fileMaster, User))
            return Forbid();

        await _fileMasterRepository.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }

    private static readonly Dictionary<string, (string LetterName, string TargetState)> LetterActionMap = new()
    {
        ["IssueLetter1"] = ("Letter 1", "S35_Letter1Issued"),
        ["IssueLetter2"] = ("Letter 2", "S35_Letter2Issued"),
        ["IssueLetter3"] = ("Letter 3", "S35_Letter3Issued"),
    };

    private static readonly Dictionary<string, string> ResponseActionMap = new()
    {
        ["MarkLetter1Responded"] = "S35_Letter1Responded",
        ["MarkLetter2Responded"] = "S35_Letter2Responded",
        ["MarkELUConfirmed"]     = "S35_ELUConfirmed",
        ["CloseCase"]            = "Closed",
    };

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanIssueLetter)]
    public async Task<IActionResult> IssueLetter(Guid id, string letterAction, string recipient, string deliveryMethod, DateTime issuedDate)
    {
        var caseFm = await _fileMasterRepository.GetByIdAsync(id);
        if (caseFm == null) return NotFound();
        if (!_scope.IsInScope(caseFm, User)) return Forbid();

        if (!LetterActionMap.TryGetValue(letterAction, out var map))
        {
            TempData["WorkflowError"] = $"Unknown letter action '{letterAction}'.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var letterType = await _context.LetterTypes.SingleOrDefaultAsync(t => t.LetterName == map.LetterName);
        if (letterType == null)
        {
            TempData["WorkflowError"] = $"Letter type '{map.LetterName}' not seeded.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.LetterIssuances.Add(new LetterIssuance
        {
            LetterIssuanceId = Guid.NewGuid(),
            FileMasterId = id,
            LetterTypeId = letterType.LetterTypeId,
            IssuedDate = DateOnly.FromDateTime(issuedDate),
            IssueMethod = deliveryMethod,
            ServingOfficialName = recipient,
            ResponseStatus = "Pending",
            DueDate = DateOnly.FromDateTime(issuedDate.AddDays(60)),
        });
        await _context.SaveChangesAsync();

        try
        {
            await _workflow.TransitionToAsync(id, map.TargetState, userId: null, notes: $"{map.LetterName} issued to {recipient}");
        }
        catch (InvalidOperationException ex)
        {
            TempData["WorkflowError"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanIssueLetter)]
    public async Task<IActionResult> MarkLetterResponse(Guid id, string letterAction)
    {
        if (!ResponseActionMap.TryGetValue(letterAction, out var targetState))
        {
            TempData["WorkflowError"] = $"Unknown response action '{letterAction}'.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var fm = await _context.FileMasters
            .Include(f => f.LetterIssuances)
            .FirstOrDefaultAsync(f => f.FileMasterId == id);
        if (fm == null) return NotFound();
        if (!_scope.IsInScope(fm, User)) return Forbid();

        var latestPending = fm.LetterIssuances
            .Where(l => l.ResponseStatus == "Pending")
            .OrderByDescending(l => l.IssuedDate)
            .FirstOrDefault();
        if (latestPending != null)
        {
            latestPending.ResponseDate = DateOnly.FromDateTime(DateTime.Today);
            latestPending.ResponseStatus = "Agreed";
            latestPending.AgreedWithFindings = true;
        }
        await _context.SaveChangesAsync();

        try
        {
            await _workflow.TransitionToAsync(id, targetState, userId: null, notes: $"State transitioned to {targetState}");
        }
        catch (InvalidOperationException ex)
        {
            TempData["WorkflowError"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
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

        var users = await _context.Users
            .Select(u => new { u.Id, Display = u.FirstName + " " + u.LastName })
            .ToListAsync();
        ViewBag.Validators = new SelectList(users, "Id", "Display", fileMaster?.ValidatorId);
        ViewBag.CapturePersons = new SelectList(users, "Id", "Display", fileMaster?.CapturePersonId);

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
