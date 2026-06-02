using System.Security.Claims;
using dwa_ver_val.Services.Letters;
using dwa_ver_val.Services.Notifications;
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
    private readonly ILawfulnessAssessmentService _assessment;
    private readonly INotificationService _notify;

    public FileMasterController(
        IFileMaster fileMasterRepository,
        ApplicationDBContext context,
        IWorkflowService workflow,
        IScopedCaseQuery scope,
        ILetterService letters,
        ILawfulnessAssessmentService assessment,
        INotificationService notify)
    {
        _fileMasterRepository = fileMasterRepository;
        _context = context;
        _workflow = workflow;
        _scope = scope;
        _letters = letters;
        _assessment = assessment;
        _notify = notify;
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
    public async Task<IActionResult> Index(string? search = null, string? status = null)
    {
        var query = _scope.FilterFileMasters(_context.FileMasters.AsQueryable(), User);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(fm => fm.RegistrationNumber != null && fm.RegistrationNumber.Contains(search)
                || fm.FarmName != null && fm.FarmName.Contains(search)
                || fm.SurveyorGeneralCode != null && fm.SurveyorGeneralCode.Contains(search));

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(fm => fm.ValidationStatusName == status);

        var fileMasters = await query
            .Include(fm => fm.Property)
            .OrderBy(fm => fm.FileNumber)
            .ToListAsync();

        ViewBag.FilterSearch = search;
        ViewBag.FilterStatus = status;

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

        // Workflow gap-fill: surface guard blocking reasons + load PAJA checklist so the
        // _WorkflowPanel can render the amber blocker list and the PAJA-required nag link.
        var currentUserId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        var currentUserGuid = currentUserId is not null && Guid.TryParse(currentUserId, out var parsedGuid) ? parsedGuid : (Guid?)null;
        vm.BlockingReasons = await _workflow.GetBlockingReasonsAsync(fileMaster.FileMasterId, currentUserGuid);
        vm.PAJAChecklist = await _context.PAJAChecklists.FirstOrDefaultAsync(p => p.FileMasterId == fileMaster.FileMasterId);
        vm.LawfulnessAssessmentResult = await _context.LawfulnessAssessmentResults
            .Include(r => r.Gwca)
            .FirstOrDefaultAsync(r => r.FileMasterId == fileMaster.FileMasterId);

        ViewBag.PortalComments = await _context.CaseComments
            .Where(c => c.FileMasterId == id)
            .OrderBy(c => c.SubmittedDate)
            .ToListAsync();

        ViewBag.PortalDocuments = await _context.Documents
            .Where(d => d.FileMasterId == id && d.UploadedByPublicUserId != null)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();

        ViewBag.PortalObjections = await _context.Objections
            .Where(o => o.FileMasterId == id)
            .OrderByDescending(o => o.LodgedDate)
            .ToListAsync();

        // Editable list shown in the panel: internal staff uploads only (external/public uploads
        // appear read-only in _PortalInboxPanel and must not get a Delete button here).
        vm.CaseDocuments = await _context.Documents
            .Where(d => d.FileMasterId == id && d.UploadedByUserId != null)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();

        // Requirement checklist must match what the workflow guards see: ALL documents on the
        // case regardless of uploader (a water user may supply a required doc via the portal).
        var presentTypes = await _context.Documents
            .Where(d => d.FileMasterId == id)
            .Select(d => d.DocumentType)
            .ToListAsync();
        vm.DocumentRequirementStatuses =
            dwa_ver_val.Services.Workflow.Guards.DocumentRequirements.StatusesFor(
                presentTypes.ToHashSet(StringComparer.Ordinal));

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

        // Pass the actual signed-in user so role-based guards (e.g. CpPrePublicReviewGuard
        // which requires AtLeastRegionalManager) can evaluate against the acting principal.
        var userIdStr = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        Guid? userId = Guid.TryParse(userIdStr, out var uid) ? uid : null;

        try
        {
            await _workflow.AdvanceAsync(id, userId: userId, notes: notes);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: FileMaster/RecordCpEvidence/{id}
    // Stamps evidence columns on FileMaster that the per-CP guards check before allowing
    // advancement. Each form posts only the field(s) relevant to the current control point
    // (see _WorkflowPanel.cshtml for the per-CP form fragments).
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCapture)]
    public async Task<IActionResult> RecordCpEvidence(Guid id, CpEvidenceForm form)
    {
        var fm = await _context.FileMasters.FindAsync(id);
        if (fm is null) return NotFound();
        if (!_scope.IsInScope(fm, User)) return Forbid();

        var signedInId = Guid.TryParse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier), out var u) ? u : Guid.Empty;

        if (form.SpatialInfoConfirmed == true) fm.SpatialInfoConfirmedAt ??= DateTime.UtcNow;
        if (form.WarmsReviewed == true) fm.WarmsReviewedAt ??= DateTime.UtcNow;
        if (form.AdditionalInfoReviewed == true) fm.AdditionalInfoReviewedAt ??= DateTime.UtcNow;
        if (form.PrePublicReviewApproved == true)
        {
            // Only RegionalManager+ may approve the pre-public review (NWA S35(2)(d)).
            // Action policy remains CanCapture so Capturers can still record other CP evidence.
            if (!User.IsInRole(DwsRoles.RegionalManager)
                && !User.IsInRole(DwsRoles.NationalManager)
                && !User.IsInRole(DwsRoles.SystemAdmin))
            {
                TempData["Error"] = "Only a Regional Manager or above may approve the pre-public participation review.";
                return RedirectToAction(nameof(Details), new { id });
            }
            fm.PrePublicReviewApprovedAt ??= DateTime.UtcNow;
            if (signedInId != Guid.Empty) fm.PrePublicReviewApprovedById = signedInId;
        }
        if (form.StakeholderWorkshopDate.HasValue) fm.StakeholderWorkshopDate = form.StakeholderWorkshopDate;
        if (form.StakeholderWorkshopVenue is not null) fm.StakeholderWorkshopVenue = form.StakeholderWorkshopVenue;
        if (form.StakeholderWorkshopAttendance.HasValue) fm.StakeholderWorkshopAttendance = form.StakeholderWorkshopAttendance;

        await _context.SaveChangesAsync();
        TempData["Success"] = "Evidence recorded.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: FileMaster/AssessLawfulness/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCapture)]
    public async Task<IActionResult> AssessLawfulness(Guid id)
    {
        var fm = await _fileMasterRepository.GetByIdAsync(id);
        if (fm is null) return NotFound();
        if (!_scope.IsInScope(fm, User)) return Forbid();

        try
        {
            var result = await _assessment.AssessAsync(id);
            TempData["Success"] =
                $"ELU assessment complete ({result.LegalFramework} framework). " +
                $"Lawful irrigation: {result.LawfulIrrigationM3.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} m³  |  " +
                $"Lawful storage: {result.LawfulStorageM3.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} m³";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // GET: FileMaster/PAJAChecklist/{id}
    // Loads (or initialises) the PAJA compliance checklist for a case. Letter 3 (S35(4)
    // ELU certificate) cannot be issued until this checklist is complete (gated in LetterService).
    // ActionName matches the URL/segment the _WorkflowPanel link uses; method name is
    // suffixed to avoid the C# rule against a method sharing its containing type's name
    // (would otherwise clash with the PAJAChecklist model type referenced inside).
    [HttpGet]
    [ActionName("PAJAChecklist")]
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
    public async Task<IActionResult> PAJAChecklistGet(Guid id)
    {
        var fm = await _context.FileMasters.FindAsync(id);
        if (fm is null) return NotFound();
        if (!_scope.IsInScope(fm, User)) return Forbid();

        var checklist = await _context.PAJAChecklists.FirstOrDefaultAsync(p => p.FileMasterId == id);
        ViewBag.FileMasterId = id;
        ViewBag.RegistrationNumber = fm.RegistrationNumber;
        var form = checklist is null ? new PAJAChecklistForm() : new PAJAChecklistForm
        {
            FactualBasis = checklist.FactualBasis,
            LegalBasis = checklist.LegalBasis,
            UserInputConsideration = checklist.UserInputConsideration,
            FinalReasoning = checklist.FinalReasoning,
        };
        ViewBag.IsComplete = checklist?.IsComplete ?? false;
        return View("PAJAChecklist", form);
    }

    // POST: FileMaster/PAJAChecklist/{id}
    // Upserts the PAJAChecklist row. CompletedAt/CompletedById are stamped the first time
    // all four narrative sections are populated together with the timestamp (the moment
    // IsComplete flips true). After that, edits keep the original completion stamp.
    [HttpPost]
    [ActionName("PAJAChecklist")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
    public async Task<IActionResult> PAJAChecklistPost(Guid id, PAJAChecklistForm form)
    {
        var fm = await _context.FileMasters.FindAsync(id);
        if (fm is null) return NotFound();
        if (!_scope.IsInScope(fm, User)) return Forbid();

        var signedInId = Guid.TryParse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier), out var u) ? u : Guid.Empty;

        var existing = await _context.PAJAChecklists.FirstOrDefaultAsync(p => p.FileMasterId == id);
        if (existing is null)
        {
            existing = new PAJAChecklist { PAJAChecklistId = Guid.NewGuid(), FileMasterId = id };
            _context.PAJAChecklists.Add(existing);
        }

        existing.FactualBasis = form.FactualBasis;
        existing.LegalBasis = form.LegalBasis;
        existing.UserInputConsideration = form.UserInputConsideration;
        existing.FinalReasoning = form.FinalReasoning;

        // Stamp completion the moment all four sections are present. CompletedAt being set is
        // itself part of PAJAChecklist.IsComplete, so we set it provisionally before re-checking.
        var willBeComplete = !string.IsNullOrWhiteSpace(existing.FactualBasis)
                          && !string.IsNullOrWhiteSpace(existing.LegalBasis)
                          && !string.IsNullOrWhiteSpace(existing.UserInputConsideration)
                          && !string.IsNullOrWhiteSpace(existing.FinalReasoning);
        if (willBeComplete && !existing.CompletedAt.HasValue)
        {
            existing.CompletedAt = DateTime.UtcNow;
            if (signedInId != Guid.Empty) existing.CompletedById = signedInId;
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = existing.IsComplete ? "PAJA checklist completed." : "PAJA checklist saved (not yet complete).";
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

        var letterCount        = await _context.LetterIssuances.CountAsync(l => l.FileMasterId == id);
        var authorisationCount = await _context.Authorisations.CountAsync(a => a.FileMasterId == id);
        var workflowCount      = await _context.WorkflowInstances.CountAsync(w => w.FileMasterId == id);

        ViewBag.BlockingLetters        = letterCount;
        ViewBag.BlockingAuthorisations = authorisationCount;
        ViewBag.BlockingWorkflow       = workflowCount;
        ViewBag.CanDelete              = letterCount == 0 && authorisationCount == 0 && workflowCount == 0;

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

        var letterCount        = await _context.LetterIssuances.CountAsync(l => l.FileMasterId == id);
        var authorisationCount = await _context.Authorisations.CountAsync(a => a.FileMasterId == id);
        var workflowCount      = await _context.WorkflowInstances.CountAsync(w => w.FileMasterId == id);

        if (letterCount > 0 || authorisationCount > 0 || workflowCount > 0)
        {
            var reasons = new List<string>();
            if (letterCount > 0)        reasons.Add($"{letterCount} letter(s)");
            if (authorisationCount > 0) reasons.Add($"{authorisationCount} authorisation(s)");
            if (workflowCount > 0)      reasons.Add($"{workflowCount} workflow instance(s)");
            TempData["Error"] = $"Cannot delete this case — it has {string.Join(" and ", reasons)} linked to it. Remove those records first.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        try
        {
            await _fileMasterRepository.DeleteAsync(id);
            TempData["Success"] = $"Case {fileMaster.RegistrationNumber ?? id.ToString()} has been deleted.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            TempData["Error"] = "Delete failed — this case has related records that prevent deletion.";
            return RedirectToAction(nameof(Delete), new { id });
        }
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
        // Section 33(2) Kader Asmal Declaration
        ["IssueS33_2"]     = ("S33_2_Decl",  "S33_2_DeclarationIssued"),
    };

    // These letter codes are issued exactly once per case in the S35/S33 statutory process.
    // Re-issuance after ANY prior issuance (regardless of ResponseStatus) is blocked.
    private static readonly HashSet<string> OneTimeLetterCodes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "S35_L1",      // S35(1) notice — served once; re-issue would restart the statutory clock
            "S35_L3",      // S35(4) ELU certificate — issued once on confirmation
            "S35_L4A",     // S53(1) notice of intent — one per unlawful-use finding
            "S35_L4_5",    // S53(1) directive to stop — one per unlawful-use finding
            "S33_3a_Decl", // S33(3)(a) declaration — ELU declared on individual application
            "S33_3b_Decl", // S33(3)(b) declaration — ELU declared on individual application
            "S33_2_Decl",  // S33(2) Kader Asmal Declaration — issued exactly once per case
        };

    // letterAction values that are pure response/determination updates — no PDF generated.
    // StampsAgreement = true for actions where the water user actively responded/agreed;
    // false for DWS-side determinations (MarkELUConfirmed, MarkUnlawfulUseFound, CloseCase)
    // which must never produce a legal record claiming the user agreed with findings.
    private static readonly Dictionary<string, (string TargetState, bool StampsAgreement)> ResponseActionMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["MarkLetter1Responded"]  = ("S35_Letter1Responded", true),
            ["MarkLetter1AResponded"] = ("S35_Letter1Responded", true),   // 1A response feeds the same downstream paths as Letter 1 response.
            ["MarkLetter2Responded"]  = ("S35_Letter2Responded", true),
            ["MarkLetter2AResponded"] = ("S35_Letter2Responded", true),
            ["MarkELUConfirmed"]      = ("S35_ELUConfirmed",     false),
            ["MarkUnlawfulUseFound"]  = ("S35_UnlawfulUseFound", false),
            ["CloseCase"]             = ("Closed",               false),
        };

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanIssueLetter)]
    public async Task<IActionResult> IssueLetter(Guid id, string letterAction, string recipient, string deliveryMethod, DateTime issuedDate, CancellationToken ct)
    {
        var caseFm = await _fileMasterRepository.GetByIdAsync(id);
        if (caseFm == null) return NotFound();
        if (!_scope.IsInScope(caseFm, User)) return Forbid();

        if (!LetterActionMap.TryGetValue(letterAction, out var map))
        {
            TempData["Error"] = $"Unknown letter action '{letterAction}'.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // BUG-014: verify the case is in the correct prerequisite workflow state for THIS
        // letter BEFORE creating any LetterIssuance row. Previously the letter was written
        // first and the workflow transition attempted second — if the transition was rejected
        // (wrong current state / guard), the case was left with an orphaned letter record and
        // an unchanged workflow state. Fail fast here with no DB write.
        var prereq = await _workflow.CanIssueLetterAsync(id, map.LetterCode);
        if (!prereq.Allowed)
        {
            TempData["Error"] = prereq.Reason;
            return RedirectToAction(nameof(Details), new { id });
        }

        // Idempotency: block re-issuance based on letter type.
        // One-time letters: block if ANY prior issuance exists regardless of ResponseStatus.
        // Repeatable letters (L1A, L2, L2A): block only while a Pending issuance exists.
        var existingLetterType = await _context.LetterTypes
            .SingleOrDefaultAsync(t => t.LetterName == map.LetterCode);
        if (existingLetterType is not null)
        {
            bool alreadyIssued;
            if (OneTimeLetterCodes.Contains(map.LetterCode))
            {
                alreadyIssued = await _context.LetterIssuances.AnyAsync(
                    l => l.FileMasterId == id
                      && l.LetterTypeId == existingLetterType.LetterTypeId);
            }
            else
            {
                alreadyIssued = await _context.LetterIssuances.AnyAsync(
                    l => l.FileMasterId == id
                      && l.LetterTypeId == existingLetterType.LetterTypeId
                      && l.ResponseStatus == "Pending");
            }

            if (alreadyIssued)
            {
                TempData["Error"] = OneTimeLetterCodes.Contains(map.LetterCode)
                    ? $"A {map.LetterCode} letter has already been issued on this case and cannot be re-issued."
                    : $"A {map.LetterCode} letter is already pending a response. Resolve the existing letter before issuing a new one.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // S33(2) declaration requires rates-paid confirmation on the case record.
        if (string.Equals(map.LetterCode, "S33_2_Decl", StringComparison.OrdinalIgnoreCase)
            && !caseFm.S33_2_RatesPaidConfirmed)
        {
            TempData["Error"] = "S33(2) declaration cannot be issued until irrigation board rates paid up to " +
                                "30 September 1998 are confirmed on the case record.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // S33(2) declaration must carry an ELU volume — the declaration letter lists the lawful volume.
        if (string.Equals(map.LetterCode, "S33_2_Decl", StringComparison.OrdinalIgnoreCase)
            && caseFm.EntitlementId is null)
        {
            TempData["Error"] = "S33(2) declaration cannot be issued until an ELU entitlement (volume) is linked to this case.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // For S33(2), auto-populate the irrigation board name from the case record.
        if (string.Equals(map.LetterCode, "S33_2_Decl", StringComparison.OrdinalIgnoreCase))
            await _context.Entry(caseFm).Reference(f => f.S33_2_IrrigationBoard).LoadAsync(ct);

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
                SignedByUserId: signedInId == Guid.Empty ? (Guid?)null : signedInId,
                SignedByDisplayName: displayName,
                SignedByTitle: role,
                SignedByOrgUnit: orgUnit,
                IrrigationBoardName: string.Equals(map.LetterCode, "S33_2_Decl", StringComparison.OrdinalIgnoreCase)
                    ? caseFm.S33_2_IrrigationBoard?.IrrigationBoardName
                    : null,
                LawfulVolumeM3: string.Equals(map.LetterCode, "S33_2_Decl", StringComparison.OrdinalIgnoreCase)
                    ? caseFm.Entitlement?.Volume
                    : null));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = $"Could not issue letter: {ex.Message}";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            await _workflow.TransitionToAsync(id, map.TargetState, userId: signedInId == Guid.Empty ? null : signedInId,
                notes: $"{map.LetterCode} issued to {recipient}");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = $"Letter issued but workflow transition failed: {ex.Message} Please contact your system administrator.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            var property = await _context.FileMasters
                .Where(f => f.FileMasterId == id)
                .Select(f => new { f.PropertyId })
                .FirstOrDefaultAsync();
            if (property is not null)
            {
                var linkedUsers = await _context.PublicUserProperties
                    .Where(p => p.PropertyId == property.PropertyId
                             && p.Status == dwa_ver_val.Models.Enums.PropertyClaimStatus.Approved)
                    .Select(p => p.PublicUserId)
                    .ToListAsync();
                foreach (var uid in linkedUsers)
                    await _notify.NotifyPublicUserAsync(uid, id, "Letter",
                        "A letter has been issued on your V&V case",
                        $"A letter has been issued on your case. Log in to the portal to view and respond.",
                        actionUrl: null);
            }
        }
        catch (Exception notifyEx)
        {
            // Notification failure must not obscure the primary operation — letter was already issued.
            var logger = HttpContext.RequestServices.GetService<Microsoft.Extensions.Logging.ILogger<FileMasterController>>();
            logger?.LogError("FileMasterController.IssueLetter: notification failed for FileMaster {Id}. Error: {ErrorType}: {ErrorMessage}",
                id, notifyEx.GetType().Name, notifyEx.Message);
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
        byte[] bytes;
        try
        {
            bytes = await blobs.ReadAsync(issuance.BlobPath);
        }
        catch (FileNotFoundException)
        {
            return NotFound("Letter PDF not found on server. Please contact the system administrator.");
        }
        catch (DirectoryNotFoundException)
        {
            return NotFound("Letter PDF not found on server. Please contact the system administrator.");
        }
        if (bytes.Length == 0)
            return NotFound("Letter PDF content is empty.");

        var fileName = $"{issuance.LetterType?.LetterName ?? "letter"}-{issuance.IssuedDate:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanIssueLetter)]
    public async Task<IActionResult> MarkLetterResponse(Guid id, string letterAction)
    {
        if (!ResponseActionMap.TryGetValue(letterAction, out var map))
        {
            TempData["Error"] = $"Unknown response action '{letterAction}'.";
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
            if (map.StampsAgreement)
            {
                latestPending.ResponseStatus = "Agreed";
                latestPending.AgreedWithFindings = true;
            }
            else
            {
                latestPending.ResponseStatus = "Closed";
            }
        }
        await _context.SaveChangesAsync();

        try
        {
            await _workflow.TransitionToAsync(id, map.TargetState, userId: null, notes: $"State transitioned to {map.TargetState}");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
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

        ViewBag.IrrigationBoards = new SelectList(
            await _context.IrrigationBoards.OrderBy(b => b.IrrigationBoardName).ToListAsync(),
            nameof(IrrigationBoard.IrrigationBoardId),
            nameof(IrrigationBoard.IrrigationBoardName),
            fileMaster?.S33_2_IrrigationBoardId);
    }

    // ── Portal Inbox ──

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCapture)]
    public async Task<IActionResult> MarkCommentRead(Guid id, Guid commentId, CancellationToken ct)
    {
        var fm = await _fileMasterRepository.GetByIdAsync(id);
        if (fm is null) return NotFound();
        if (!_scope.IsInScope(fm, User)) return Forbid();

        var comment = await _context.CaseComments.FindAsync(new object[] { commentId }, ct);
        if (comment is null || comment.FileMasterId != id) return NotFound();
        comment.ReadByDWSDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = DwsPolicies.CanCapture)]
    public async Task<IActionResult> PortalReply(Guid id, Guid? parentCommentId,
        string replyText, CancellationToken ct)
    {
        var fm = await _fileMasterRepository.GetByIdAsync(id);
        if (fm is null) return NotFound();
        if (!_scope.IsInScope(fm, User)) return Forbid();

        if (string.IsNullOrWhiteSpace(replyText))
        {
            TempData["Error"] = "Reply text cannot be empty.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (replyText.Length > 4000)
        {
            TempData["Error"] = "Reply text cannot exceed 4 000 characters.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Validate parent comment before persisting.
        if (parentCommentId.HasValue)
        {
            var parentCheck = await _context.CaseComments.FindAsync(new object[] { parentCommentId.Value }, ct);
            if (parentCheck is null)
            {
                // Parent deleted — de-thread gracefully (post as root comment).
                parentCommentId = null;
            }
            else if (parentCheck.FileMasterId != id)
            {
                // Parent exists but belongs to a different case — reject.
                TempData["Error"] = "The comment you are replying to does not belong to this case.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _context.CaseComments.Add(new CaseComment
        {
            CommentId = Guid.NewGuid(),
            FileMasterId = id,
            ApplicationUserId = Guid.TryParse(userId, out var callerGuid) ? callerGuid : null,
            AuthorType = "DWSOfficial",
            ParentCommentId = parentCommentId,
            CommentText = replyText,
            SubmittedDate = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(ct);

        if (parentCommentId.HasValue)
        {
            var parent = await _context.CaseComments.FindAsync(new object[] { parentCommentId.Value }, ct);
            // Guard: only notify if the parent comment belongs to THIS case.
            if (parent is not null && parent.FileMasterId == id && parent.PublicUserId.HasValue)
                await _notify.NotifyPublicUserAsync(parent.PublicUserId!.Value, id, "Reply",
                    "DWS has responded to your comment",
                    replyText.Length > 200 ? replyText[..200] + "..." : replyText,
                    actionUrl: null, ct);
        }

        TempData["Success"] = "Reply posted.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
