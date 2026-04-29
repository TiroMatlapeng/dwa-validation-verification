using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize(Policy = DwsPolicies.CanRead)]
public class ValidationController : Controller
{
    private readonly ApplicationDBContext _context;
    private readonly IScopedCaseQuery _scope;

    public ValidationController(ApplicationDBContext context, IScopedCaseQuery scope)
    {
        _context = context;
        _scope = scope;
    }

    public async Task<IActionResult> Index()
    {
        // Ongoing validations = FileMasters whose workflow is not yet terminal.
        // Scope-filter so a Validator only sees cases in their WMA, NationalManager sees all.
        var scoped = _scope.FilterFileMasters(_context.FileMasters.AsQueryable(), User);

        var items = await (
            from fm in scoped
            join wi in _context.WorkflowInstances on fm.WorkflowInstanceId equals wi.WorkflowInstanceId into wj
            from wi in wj.DefaultIfEmpty()
            join ws in _context.WorkflowStates on (wi != null ? wi.CurrentWorkflowStateId : Guid.Empty) equals ws.WorkflowStateId into wsj
            from ws in wsj.DefaultIfEmpty()
            where wi == null || ws == null || !ws.IsTerminal
            orderby fm.FileCreatedDate descending
            select new OngoingValidationRow
            {
                FileMasterId = fm.FileMasterId,
                CaseNumber = fm.CaseNumber ?? fm.RegistrationNumber,
                FarmName = fm.FarmName,
                PropertyReference = fm.Property != null ? (fm.Property.SGCode ?? fm.Property.PropertyReferenceNumber ?? "—") : "—",
                AssessmentTrack = fm.AssessmentTrack ?? "S35_Verification",
                CurrentPhase = ws != null ? ws.Phase : "Not started",
                CurrentStateName = ws != null ? ws.StateName : "Unstarted",
                FileCreatedDate = fm.FileCreatedDate,
                ValidationStatus = fm.ValidationStatusName ?? "In Process"
            }).ToListAsync();

        return View(items);
    }
}

public class OngoingValidationRow
{
    public Guid FileMasterId { get; set; }
    public required string CaseNumber { get; set; }
    public required string FarmName { get; set; }
    public required string PropertyReference { get; set; }
    public required string AssessmentTrack { get; set; }
    public required string CurrentPhase { get; set; }
    public required string CurrentStateName { get; set; }
    public DateOnly FileCreatedDate { get; set; }
    public required string ValidationStatus { get; set; }
}
