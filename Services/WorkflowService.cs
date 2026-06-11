using dwa_ver_val.Services.Audit;
using dwa_ver_val.Services.Workflow;
using Microsoft.EntityFrameworkCore;

public class WorkflowService : IWorkflowService
{
    private const string S33_2_SkipTargetStateName = "S33_2_ReadyForDeclaration";
    private static readonly string[] CpsSkippedOnS33_2 = { "CP5", "CP6", "CP7", "CP8", "CP9", "CP11", "CP_PrePublicReview", "CP_StakeholderWorkshop" };

    private readonly ApplicationDBContext _context;
    private readonly IEnumerable<ITransitionGuard> _guards;
    private readonly IAuditService _audit;

    public WorkflowService(
        ApplicationDBContext context,
        IEnumerable<ITransitionGuard> guards,
        IAuditService audit)
    {
        _context = context;
        _guards = guards;
        _audit = audit;
    }

    public async Task<WorkflowInstance> StartWorkflowAsync(Guid fileMasterId)
    {
        var fileMaster = await _context.FileMasters.FindAsync(fileMasterId)
            ?? throw new InvalidOperationException($"FileMaster {fileMasterId} not found.");

        if (fileMaster.WorkflowInstanceId.HasValue)
            throw new InvalidOperationException("Workflow already started for this case.");

        var firstState = await _context.WorkflowStates
            .OrderBy(s => s.DisplayOrder)
            .FirstAsync();

        var instance = new WorkflowInstance
        {
            WorkflowInstanceId = Guid.NewGuid(),
            FileMasterId = fileMasterId,
            CurrentWorkflowStateId = firstState.WorkflowStateId,
            Status = "Active",
            CreatedDate = DateTime.UtcNow,
        };
        _context.WorkflowInstances.Add(instance);

        _context.WorkflowStepRecords.Add(new WorkflowStepRecord
        {
            WorkflowStepRecordId = Guid.NewGuid(),
            WorkflowInstanceId = instance.WorkflowInstanceId,
            WorkflowStateId = firstState.WorkflowStateId,
            StepStatus = "InProgress",
            StartedDate = DateTime.UtcNow,
        });

        fileMaster.WorkflowInstanceId = instance.WorkflowInstanceId;
        await _context.SaveChangesAsync();

        await _audit.LogAsync(new AuditEvent(
            EntityType: nameof(FileMaster),
            EntityId: fileMasterId.ToString(),
            Action: "WorkflowStarted",
            ToValue: firstState.StateName));
        return instance;
    }

    public async Task<WorkflowInstance> AdvanceAsync(Guid fileMasterId, Guid? userId, string? notes)
    {
        var instance = await LoadInstanceAsync(fileMasterId);
        var fileMaster = await _context.FileMasters.FindAsync(fileMasterId)
            ?? throw new InvalidOperationException($"FileMaster {fileMasterId} not found.");
        var currentState = await _context.WorkflowStates.FindAsync(instance.CurrentWorkflowStateId)
            ?? throw new InvalidOperationException("Current state not found.");

        if (currentState.IsTerminal)
            throw new InvalidOperationException($"Case is at terminal state '{currentState.StateName}'.");

        if (string.Equals(currentState.StateName, "S33_2_ReadyForDeclaration", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Direct workflow advance is not permitted from S33_2_ReadyForDeclaration. " +
                "Issue the S33(2) Kader Asmal Declaration letter via the Letters panel to proceed.");

        var nextState = await ResolveNextStateAsync(fileMaster, currentState)
            ?? throw new InvalidOperationException("No further states available.");

        return await MoveToStateAsync(instance, fileMaster, currentState, nextState, userId, notes);
    }

    public async Task<WorkflowInstance> TransitionToAsync(Guid fileMasterId, string targetStateName, Guid? userId, string? notes)
    {
        var instance = await LoadInstanceAsync(fileMasterId);
        var fileMaster = await _context.FileMasters.FindAsync(fileMasterId)
            ?? throw new InvalidOperationException($"FileMaster {fileMasterId} not found.");
        var currentState = await _context.WorkflowStates.FindAsync(instance.CurrentWorkflowStateId)
            ?? throw new InvalidOperationException("Current state not found.");
        var target = await _context.WorkflowStates.SingleOrDefaultAsync(s => s.StateName == targetStateName)
            ?? throw new InvalidOperationException($"Workflow state '{targetStateName}' not found.");
        return await MoveToStateAsync(instance, fileMaster, currentState, target, userId, notes);
    }

    public async Task<IReadOnlyList<WorkflowStepRecord>> GetHistoryAsync(Guid workflowInstanceId)
    {
        return await _context.WorkflowStepRecords
            .Include(s => s.WorkflowState)
            .Where(s => s.WorkflowInstanceId == workflowInstanceId)
            .OrderBy(s => s.StartedDate)
            .ToListAsync();
    }

    public async Task<WorkflowInstance?> GetInstanceForFileAsync(Guid fileMasterId)
    {
        return await _context.WorkflowInstances
            .Include(w => w.CurrentWorkflowState)
            .FirstOrDefaultAsync(w => w.FileMasterId == fileMasterId);
    }

    /// <summary>
    /// Resolves the next workflow state, honouring AssessmentTrack:
    /// on S33_2 track, transitions that would land in CP5–CP9 are redirected to
    /// the S33_2_ReadyForDeclaration holding state (non-terminal).
    /// </summary>
    private async Task<WorkflowState?> ResolveNextStateAsync(FileMaster fileMaster, WorkflowState currentState)
    {
        var defaultNext = await _context.WorkflowStates
            .Where(s => s.DisplayOrder > currentState.DisplayOrder)
            .OrderBy(s => s.DisplayOrder)
            .FirstOrDefaultAsync();
        if (defaultNext is null) return null;

        if (string.Equals(fileMaster.AssessmentTrack, "S33_2_Declaration", StringComparison.OrdinalIgnoreCase)
            && CpsSkippedOnS33_2.Any(cp => defaultNext.StateName.StartsWith(cp, StringComparison.OrdinalIgnoreCase)))
        {
            return await _context.WorkflowStates.SingleOrDefaultAsync(s => s.StateName == S33_2_SkipTargetStateName)
                ?? throw new InvalidOperationException(
                    $"S33(2) skip target state '{S33_2_SkipTargetStateName}' was not found in WorkflowStates. " +
                    "Ensure SeedWorkflowStatesAsync has been run.");
        }

        return defaultNext;
    }

    // States from which the operative next action is issuing a letter / declaration
    // (handled by FileMasterController.IssueLetter via TransitionToAsync), NOT the
    // sequential AdvanceAsync path. GetBlockingReasonsAsync surfaces *advance* guard
    // denials; once a case is in (or about to enter) the letter phase, those advance
    // reasons are irrelevant and only confuse the operator — so we suppress them.
    // CP_StakeholderWorkshop is the launch point for the letter sub-process, so advance-guard
    // banners are irrelevant there. CP9_SFRACalculated is intentionally NOT treated as a letter
    // phase: the case still advances CP9 → CP11 → CP_PrePublicReview → CP_StakeholderWorkshop,
    // and its advance-blocking reasons must remain visible so the operator can complete those
    // control points. (Mirrors FileMasterDetailsViewModel.IsReadyForLetters.)
    private static bool IsLetterPhaseState(WorkflowState state) =>
        state.StateName is "CP_StakeholderWorkshop"
        || state.StateName.StartsWith("S35_", StringComparison.OrdinalIgnoreCase)
        || state.StateName.StartsWith("S33_", StringComparison.OrdinalIgnoreCase);

    public async Task<List<string>> GetBlockingReasonsAsync(Guid fileMasterId, Guid? userId)
    {
        var instance = await GetInstanceForFileAsync(fileMasterId);
        if (instance is null) return new();

        var fileMaster = await _context.FileMasters.FindAsync(fileMasterId);
        if (fileMaster is null) return new();

        var currentState = await _context.WorkflowStates.FindAsync(instance.CurrentWorkflowStateId);
        if (currentState is null || currentState.IsTerminal) return new();

        // BUG-012: in the letter/declaration phase the next action is "issue a letter",
        // not "advance to the next CP". Returning advance-guard denials here produced
        // stale, irrelevant banners (e.g. a CP-leaving guard message on a case that has
        // long since reached CP9 / the letter sub-states). Suppress them.
        if (IsLetterPhaseState(currentState)) return new();

        var nextState = await ResolveNextStateAsync(fileMaster, currentState);
        if (nextState is null) return new();

        var (user, userRoles) = await LoadUserContextAsync(userId);

        // Only run guards relevant to THIS transition. Each guard self-scopes via
        // IsLeaving(currentState) / TargetState checks, so iterating all registered
        // guards yields at most the guard(s) governing the current→next move; guards
        // for already-passed control points return GuardResult.Ok and add nothing.
        var reasons = new List<string>();
        var ctx = new GuardContext(fileMaster, currentState, nextState, user, userRoles);
        foreach (var guard in _guards)
        {
            var result = await guard.CheckAsync(ctx);
            if (!result.Allowed && result.Reason is not null)
                reasons.Add(result.Reason);
        }
        return reasons;
    }

    // BUG-014: prerequisite current-state(s) each letter may be issued from. Keyed on the
    // canonical letter code (LetterActionMap.LetterCode in FileMasterController). The values
    // mirror FileMasterDetailsViewModel.AvailableLetterActions — the only states from which
    // each issue action is surfaced in the UI. Issuing from any other state would create a
    // LetterIssuance row that the subsequent TransitionToAsync could not legitimately follow.
    private static readonly Dictionary<string, string[]> LetterPrerequisiteStates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // S35 verification track
            ["S35_L1"]   = new[] { "CP9_SFRACalculated", "CP_StakeholderWorkshop" },
            ["S35_L1A"]  = new[] { "S35_Letter1Issued" },
            ["S35_L2"]   = new[] { "S35_Letter1Responded" },
            ["S35_L2A"]  = new[] { "S35_Letter2Issued" },
            ["S35_L3"]   = new[] { "S35_Letter1Responded", "S35_Letter2Responded" },
            ["S35_L4A"]  = new[] { "S35_UnlawfulUseFound" },
            ["S35_L4_5"] = new[] { "S35_Letter4AIssued" },
            // S33(3) individual-application declarations
            ["S33_3a_Decl"] = new[] { "CP9_SFRACalculated", "CP_StakeholderWorkshop" },
            ["S33_3b_Decl"] = new[] { "CP9_SFRACalculated", "CP_StakeholderWorkshop" },
            // S33(2) Kader Asmal declaration — only from the holding state
            ["S33_2_Decl"]  = new[] { "S33_2_ReadyForDeclaration" },
        };

    public async Task<LetterIssuanceCheck> CanIssueLetterAsync(Guid fileMasterId, string letterCode)
    {
        if (!LetterPrerequisiteStates.TryGetValue(letterCode, out var allowedStates))
            return LetterIssuanceCheck.Deny($"Unknown letter code '{letterCode}'.");

        var instance = await GetInstanceForFileAsync(fileMasterId);
        if (instance is null)
            return LetterIssuanceCheck.Deny("This case has no workflow instance, so no letter can be issued.");

        var currentState = await _context.WorkflowStates.FindAsync(instance.CurrentWorkflowStateId);
        if (currentState is null)
            return LetterIssuanceCheck.Deny("The current workflow state could not be resolved.");

        if (allowedStates.Contains(currentState.StateName, StringComparer.OrdinalIgnoreCase))
            return LetterIssuanceCheck.Ok;

        var expected = string.Join(" or ", allowedStates);
        return LetterIssuanceCheck.Deny(
            $"This letter cannot be issued from the current workflow state '{currentState.StateName}'. " +
            $"The case must first reach {expected}. Complete the prerequisite control points before issuing this letter.");
    }

    private async Task<WorkflowInstance> MoveToStateAsync(
        WorkflowInstance instance,
        FileMaster fileMaster,
        WorkflowState currentState,
        WorkflowState target,
        Guid? userId,
        string? notes)
    {
        // Load acting user + their ASP.NET Identity roles for guards that perform
        // role-based checks (e.g. CpPrePublicReviewGuard requires RegionalManager+).
        // Identity is configured with Guid keys, so UserRoles/Roles join on Guid.
        var (user, userRoles) = await LoadUserContextAsync(userId);

        // Evaluate guards in order; first denial blocks the transition.
        var guardCtx = new GuardContext(fileMaster, currentState, target, user, userRoles);
        foreach (var guard in _guards)
        {
            var result = await guard.CheckAsync(guardCtx);
            if (!result.Allowed)
                throw new InvalidOperationException(result.Reason ?? "Transition blocked by a guard.");
        }

        var currentStep = await _context.WorkflowStepRecords
            .Where(s => s.WorkflowInstanceId == instance.WorkflowInstanceId && s.StepStatus == "InProgress")
            .OrderByDescending(s => s.StartedDate)
            .FirstOrDefaultAsync();

        if (currentStep != null)
        {
            currentStep.StepStatus = "Completed";
            currentStep.CompletedDate = DateTime.UtcNow;
            currentStep.CompletedById = userId;
            currentStep.Notes = notes;
        }

        instance.CurrentWorkflowStateId = target.WorkflowStateId;
        if (target.IsTerminal)
        {
            instance.Status = "Completed";
            instance.CompletedDate = DateTime.UtcNow;
        }

        _context.WorkflowStepRecords.Add(new WorkflowStepRecord
        {
            WorkflowStepRecordId = Guid.NewGuid(),
            WorkflowInstanceId = instance.WorkflowInstanceId,
            WorkflowStateId = target.WorkflowStateId,
            StepStatus = target.IsTerminal ? "Completed" : "InProgress",
            StartedDate = DateTime.UtcNow,
            CompletedDate = target.IsTerminal ? DateTime.UtcNow : null,
            CompletedById = target.IsTerminal ? userId : null,
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Another concurrent request committed a transition for this WorkflowInstance
            // before ours. The rowversion on our tracked instance is now stale. Translate
            // to a domain exception so controllers/callers can give the operator a clear,
            // actionable message without exposing EF internals.
            throw new dwa_ver_val.Services.Workflow.WorkflowConcurrencyException(
                "This case was advanced by another user. Refresh and retry.", ex);
        }

        await _audit.LogAsync(new AuditEvent(
            EntityType: nameof(FileMaster),
            EntityId: instance.FileMasterId.ToString(),
            Action: target.IsTerminal ? "WorkflowCompleted" : "WorkflowAdvanced",
            UserId: userId,
            FromValue: currentState.StateName,
            ToValue: target.StateName,
            Reason: notes));

        return instance;
    }

    private async Task<WorkflowInstance> LoadInstanceAsync(Guid fileMasterId)
    {
        return await _context.WorkflowInstances
            .FirstOrDefaultAsync(w => w.FileMasterId == fileMasterId)
            ?? throw new InvalidOperationException($"No workflow instance for FileMaster {fileMasterId}.");
    }

    private async Task<(ApplicationUser? User, IReadOnlyList<string> Roles)> LoadUserContextAsync(Guid? userId)
    {
        if (!userId.HasValue)
            return (null, Array.Empty<string>());

        var user = await _context.Users.FindAsync(userId.Value);
        var roles = await _context.UserRoles
            .Where(ur => ur.UserId == userId.Value)
            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!)
            .ToListAsync();

        return (user, roles);
    }
}
