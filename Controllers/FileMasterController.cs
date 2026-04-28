using dwa_ver_val.Services.Letters;
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
    private readonly ILetterService _letters;

    public FileMasterController(
        IFileMaster fileMasterRepository,
        ApplicationDBContext context,
        IWorkflowService workflow,
        IScopedCaseQuery scope,
        ILetterService letters)
    {
        _fileMasterRepository = fileMasterRepository;
        _context = context;
        _workflow = workflow;
        _scope = scope;
        _letters = letters;
    }

    // GET: FileMaster/LetterPreview/{id}?code=S35_L1
    // Returns an on-the-fly preview PDF for the given case + letter code. Scope-guarded like the rest.
    [HttpGet]
    public async Task<IActionResult> LetterPreview(Guid id, string code)
    {
        var fm = await _fileMasterRepository.GetByIdAsync(id);
        if (fm == null) return NotFound();
        if (!_scope.IsInScope(fm, User)) return Forbid();
        var bytes = await _letters.RenderPreviewAsync(id, code);
        return File(bytes, "application/pdf", $"preview-{code}-{id:N}.pdf");
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

        // Audit trail for this case — includes FileMaster-entity events (workflow transitions, etc.)
        // and letter-entity events linked to issuances on this case.
        var letterIds = fileMaster.LetterIssuances.Select(l => l.LetterIssuanceId.ToString()).ToList();
        vm.AuditTrail = await _context.AuditLogs
            .Where(a =>
                (a.EntityType == nameof(FileMaster) && a.EntityId == id.ToString())
                || (a.EntityType == nameof(LetterIssuance) && letterIds.Contains(a.EntityId)))
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();

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

    // letterAction (HTML form value) -> (LetterType.LetterName / template code, target workflow state).
    // The LetterCode is the canonical short code consumed by ILetterTemplate registrations,
    // matching SeedDataService.SeedLetterTypesAsync.
    private static readonly Dictionary<string, (string LetterCode, string TargetState)> LetterActionMap = new()
    {
        // Section 35 verification track
        ["IssueLetter1"]   = ("S35_L1",      "S35_Letter1Issued"),
        ["IssueLetter1A"]  = ("S35_L1A",     "S35_Letter1AIssued"),
        ["IssueLetter2"]   = ("S35_L2",      "S35_Letter2Issued"),
        ["IssueLetter2A"]  = ("S35_L2A",     "S35_Letter2AIssued"),
        ["IssueLetter3"]   = ("S35_L3",      "S35_Letter3Issued"),
        ["IssueLetter4A"]  = ("S35_L4A",     "S35_Letter4AIssued"),
        ["IssueLetter4_5"] = ("S35_L4_5",    "S35_Letter4And5Issued"),
        // Section 33(3) declaration track (S33(2) Kader Asmal is auto-issued via track-skip in WorkflowService)
        ["IssueS33_3a"]    = ("S33_3a_Decl", "S33_3_DeclarationIssued"),
        ["IssueS33_3b"]    = ("S33_3b_Decl", "S33_3_DeclarationIssued"),
    };

    // letterAction values that are pure response/determination updates — no PDF generated.
    private static readonly Dictionary<string, string> ResponseActionMap = new()
    {
        ["MarkLetter1Responded"]  = "S35_Letter1Responded",
        ["MarkLetter1AResponded"] = "S35_Letter1Responded",   // 1A response feeds the same downstream paths as Letter 1 response.
        ["MarkLetter2Responded"]  = "S35_Letter2Responded",
        ["MarkLetter2AResponded"] = "S35_Letter2Responded",
        ["MarkELUConfirmed"]      = "S35_ELUConfirmed",
        ["MarkUnlawfulUseFound"]  = "S35_UnlawfulUseFound",
        ["CloseCase"]             = "Closed",
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

        // Build the IssueLetterRequest, falling back gracefully when the signed-in user
        // hasn't fully populated their profile (no FirstName/LastName claim).
        var signedInId = Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var u)
            ? u
            : Guid.Empty;
        var displayName = User.FindFirst("displayName")?.Value
                          ?? User.Identity?.Name
                          ?? "(unknown signatory)";
        var orgUnit = caseFm.OrgUnit?.Name ?? "DWS Regional Office";
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Regional Manager";

        try
        {
            await _letters.IssueAsync(id, map.LetterCode, new dwa_ver_val.Services.Letters.IssueLetterRequest(
                RecipientName: recipient,
                RecipientAddress: null,
                IssueMethod: deliveryMethod,
                IssueDate: DateOnly.FromDateTime(issuedDate),
                DueDate: DateOnly.FromDateTime(issuedDate.AddDays(60)),
                ServedByOfficialId: string.Equals(deliveryMethod, "InPerson", StringComparison.OrdinalIgnoreCase) && signedInId != Guid.Empty
                    ? signedInId
                    : null,
                AdditionalNotes: null,
                SignedByUserId: signedInId,
                SignedByDisplayName: displayName,
                SignedByTitle: role,
                SignedByOrgUnit: orgUnit));
        }
        catch (InvalidOperationException ex)
        {
            TempData["WorkflowError"] = $"Could not issue letter: {ex.Message}";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            await _workflow.TransitionToAsync(id, map.TargetState, userId: signedInId == Guid.Empty ? null : signedInId,
                notes: $"{map.LetterCode} issued to {recipient}");
        }
        catch (InvalidOperationException ex)
        {
            TempData["WorkflowError"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Returns the signed PDF for an issued letter, scope-guarded.</summary>
    [HttpGet]
    public async Task<IActionResult> LetterPdf(Guid id, Guid letterIssuanceId)
    {
        var fm = await _fileMasterRepository.GetByIdAsync(id);
        if (fm is null) return NotFound();
        if (!_scope.IsInScope(fm, User)) return Forbid();

        var issuance = await _context.LetterIssuances
            .Include(l => l.LetterType)
            .FirstOrDefaultAsync(l => l.LetterIssuanceId == letterIssuanceId && l.FileMasterId == id);
        if (issuance is null || string.IsNullOrEmpty(issuance.BlobPath)) return NotFound();

        var blobs = HttpContext.RequestServices.GetRequiredService<dwa_ver_val.Services.Letters.IBlobStore>();
        var bytes = await blobs.ReadAsync(issuance.BlobPath);
        var fileName = $"{issuance.LetterType?.LetterName ?? "letter"}-{issuance.IssuedDate:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", fileName);
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
