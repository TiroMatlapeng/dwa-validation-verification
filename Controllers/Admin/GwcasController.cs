using dwa_ver_val.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Controllers.Admin;

[Authorize(Policy = DwsPolicies.CanAdminister)]
[Route("Admin/[controller]/[action]")]
public class GwcasController : Controller
{
    private readonly ApplicationDBContext _db;

    public GwcasController(ApplicationDBContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var items = await _db.GovernmentWaterControlAreas
            .OrderBy(g => g.GovernmentWaterControlAreaName)
            .Select(g => new GwcaListItemViewModel
            {
                Id = g.WaterControlAreaId,
                Name = g.GovernmentWaterControlAreaName,
                GazetteReference = g.GovernmentGazetteReference,
                ProclamationDate = g.ProclamationDate,
                ActiveRuleCount = g.ProclamationRules.Count(r => r.IsActive)
            })
            .ToListAsync(ct);

        return View(items);
    }

    [HttpGet]
    public IActionResult Create() => View(new GwcaFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GwcaFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var gwca = new GovernmentWaterControlArea
        {
            WaterControlAreaId = Guid.NewGuid(),
            GovernmentWaterControlAreaName = model.Name.Trim(),
            GovernmentGazetteReference = model.GazetteReference,
            ProclamationDate = model.ProclamationDate,
            WaterControlPhoneNumber = model.PhoneNumber
        };
        _db.GovernmentWaterControlAreas.Add(gwca);
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = $"GWCA \"{gwca.GovernmentWaterControlAreaName}\" created.";
        return RedirectToAction(nameof(Edit), new { id = gwca.WaterControlAreaId });
    }

    /// <summary>GWCA edit + inline proclamation-rule management (detail page).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var model = await BuildDetail(id, ct);
        if (model is null) return NotFound();
        return View(model);
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, GwcaDetailViewModel model, CancellationToken ct)
    {
        if (id != model.Gwca.Id) return BadRequest();

        // Only the GWCA fields are validated on this post — rule sub-forms post to their own actions.
        ModelState.Remove("NewRule.RuleCode");
        ModelState.Remove("NewRule.RuleDescription");

        if (!ModelState.IsValid)
        {
            var rebuilt = await BuildDetail(id, ct);
            if (rebuilt is null) return NotFound();
            rebuilt.Gwca = model.Gwca;
            return View(rebuilt);
        }

        var gwca = await _db.GovernmentWaterControlAreas.FirstOrDefaultAsync(g => g.WaterControlAreaId == id, ct);
        if (gwca is null) return NotFound();

        gwca.GovernmentWaterControlAreaName = model.Gwca.Name.Trim();
        gwca.GovernmentGazetteReference = model.Gwca.GazetteReference;
        gwca.ProclamationDate = model.Gwca.ProclamationDate;
        gwca.WaterControlPhoneNumber = model.Gwca.PhoneNumber;
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = "GWCA details updated.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var gwca = await _db.GovernmentWaterControlAreas.FirstOrDefaultAsync(g => g.WaterControlAreaId == id, ct);
        if (gwca is null) { TempData["Error"] = "GWCA not found."; return RedirectToAction(nameof(Index)); }

        // Properties and ELU results reference a GWCA (SetNull). Block delete so an assessment's
        // legal basis is never silently severed; the rules cascade-delete only when the GWCA is.
        if (await _db.Properties.AnyAsync(p => p.WaterControlAreaId == id, ct))
        { TempData["Error"] = $"Cannot delete \"{gwca.GovernmentWaterControlAreaName}\": properties fall within this GWCA."; return RedirectToAction(nameof(Index)); }

        if (await _db.LawfulnessAssessmentResults.AnyAsync(r => r.GwcaId == id, ct))
        { TempData["Error"] = $"Cannot delete \"{gwca.GovernmentWaterControlAreaName}\": ELU assessments reference this GWCA."; return RedirectToAction(nameof(Index)); }

        _db.GovernmentWaterControlAreas.Remove(gwca);
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = $"GWCA \"{gwca.GovernmentWaterControlAreaName}\" deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── Proclamation rule sub-actions ──

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRule(Guid id, GwcaRuleFormViewModel rule, CancellationToken ct)
    {
        var exists = await _db.GovernmentWaterControlAreas.AnyAsync(g => g.WaterControlAreaId == id, ct);
        if (!exists) return NotFound();

        if (string.IsNullOrWhiteSpace(rule.RuleCode) || string.IsNullOrWhiteSpace(rule.RuleDescription))
        {
            TempData["Error"] = "Rule code and description are required.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        _db.GwcaProclamationRules.Add(new GwcaProclamationRule
        {
            RuleId = Guid.NewGuid(),
            WaterControlAreaId = id,
            RuleCode = rule.RuleCode.Trim(),
            RuleDescription = rule.RuleDescription.Trim(),
            NumericLimit = rule.NumericLimit,
            Unit = rule.Unit,
            GovernmentGazetteReference = rule.GazetteReference,
            EffectiveFrom = rule.EffectiveFrom,
            EffectiveTo = rule.EffectiveTo,
            IsActive = rule.IsActive
        });
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = $"Rule \"{rule.RuleCode}\" added.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpGet("{ruleId:guid}")]
    public async Task<IActionResult> EditRule(Guid ruleId, CancellationToken ct)
    {
        var rule = await _db.GwcaProclamationRules.FirstOrDefaultAsync(r => r.RuleId == ruleId, ct);
        if (rule is null) return NotFound();

        return View(new GwcaRuleFormViewModel
        {
            RuleId = rule.RuleId,
            WaterControlAreaId = rule.WaterControlAreaId,
            RuleCode = rule.RuleCode,
            RuleDescription = rule.RuleDescription,
            NumericLimit = rule.NumericLimit,
            Unit = rule.Unit,
            GazetteReference = rule.GovernmentGazetteReference,
            EffectiveFrom = rule.EffectiveFrom,
            EffectiveTo = rule.EffectiveTo,
            IsActive = rule.IsActive
        });
    }

    [HttpPost("{ruleId:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRule(Guid ruleId, GwcaRuleFormViewModel rule, CancellationToken ct)
    {
        var existing = await _db.GwcaProclamationRules.FirstOrDefaultAsync(r => r.RuleId == ruleId, ct);
        if (existing is null) { TempData["Error"] = "Rule not found."; return RedirectToAction(nameof(Index)); }

        if (string.IsNullOrWhiteSpace(rule.RuleCode) || string.IsNullOrWhiteSpace(rule.RuleDescription))
        {
            TempData["Error"] = "Rule code and description are required.";
            return RedirectToAction(nameof(Edit), new { id = existing.WaterControlAreaId });
        }

        existing.RuleCode = rule.RuleCode.Trim();
        existing.RuleDescription = rule.RuleDescription.Trim();
        existing.NumericLimit = rule.NumericLimit;
        existing.Unit = rule.Unit;
        existing.GovernmentGazetteReference = rule.GazetteReference;
        existing.EffectiveFrom = rule.EffectiveFrom;
        existing.EffectiveTo = rule.EffectiveTo;
        existing.IsActive = rule.IsActive;
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = $"Rule \"{existing.RuleCode}\" updated.";
        return RedirectToAction(nameof(Edit), new { id = existing.WaterControlAreaId });
    }

    /// <summary>Soft toggle — flips a rule's IsActive flag (preferred over hard delete for config).</summary>
    [HttpPost("{ruleId:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleRule(Guid ruleId, CancellationToken ct)
    {
        var rule = await _db.GwcaProclamationRules.FirstOrDefaultAsync(r => r.RuleId == ruleId, ct);
        if (rule is null) { TempData["Error"] = "Rule not found."; return RedirectToAction(nameof(Index)); }

        rule.IsActive = !rule.IsActive;
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = $"Rule \"{rule.RuleCode}\" {(rule.IsActive ? "activated" : "deactivated")}.";
        return RedirectToAction(nameof(Edit), new { id = rule.WaterControlAreaId });
    }

    [HttpPost("{ruleId:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRule(Guid ruleId, CancellationToken ct)
    {
        var rule = await _db.GwcaProclamationRules.FirstOrDefaultAsync(r => r.RuleId == ruleId, ct);
        if (rule is null) { TempData["Error"] = "Rule not found."; return RedirectToAction(nameof(Index)); }

        var gwcaId = rule.WaterControlAreaId;
        _db.GwcaProclamationRules.Remove(rule);
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = $"Rule \"{rule.RuleCode}\" deleted.";
        return RedirectToAction(nameof(Edit), new { id = gwcaId });
    }

    private async Task<GwcaDetailViewModel?> BuildDetail(Guid id, CancellationToken ct)
    {
        var gwca = await _db.GovernmentWaterControlAreas
            .Include(g => g.ProclamationRules)
            .FirstOrDefaultAsync(g => g.WaterControlAreaId == id, ct);
        if (gwca is null) return null;

        return new GwcaDetailViewModel
        {
            Gwca = new GwcaFormViewModel
            {
                Id = gwca.WaterControlAreaId,
                Name = gwca.GovernmentWaterControlAreaName,
                GazetteReference = gwca.GovernmentGazetteReference,
                ProclamationDate = gwca.ProclamationDate,
                PhoneNumber = gwca.WaterControlPhoneNumber
            },
            Rules = gwca.ProclamationRules.OrderBy(r => r.RuleCode).ToList(),
            NewRule = new GwcaRuleFormViewModel { WaterControlAreaId = id }
        };
    }
}
