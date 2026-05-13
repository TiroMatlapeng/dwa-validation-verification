using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

[Authorize(Policy = DwsPolicies.CanTransitionWorkflow)]
public class ForestationController : Controller
{
    private readonly IForestation _repo;
    private readonly ApplicationDBContext _context;

    public ForestationController(IForestation repo, ApplicationDBContext context)
    {
        _repo = repo;
        _context = context;
    }

    // GET: Forestation/Index?propertyId=...
    [HttpGet]
    [Authorize(Policy = DwsPolicies.CanRead)]
    public async Task<IActionResult> Index(Guid propertyId)
    {
        var property = await _context.Properties.FindAsync(propertyId);
        if (property == null) return NotFound();

        var records = await _repo.GetByPropertyIdAsync(propertyId);
        var vms = records.Select(f => new ForestationViewModel
        {
            ForestationId = f.ForestationId,
            PropertyId = f.PropertyId,
            PeriodId = f.PeriodId,
            WithinGWCA = f.WithinGWCA,
            Specie = f.Specie,
            WaterResource = f.WaterResource,
            QualifyPeriodSFRAHectares = f.QualifyPeriodSFRAHectares,
            CurrentPeriodSFRAHectares = f.CurrentPeriodSFRAHectares,
            QualifyPeriodVolume = f.QualifyPeriodVolume,
            RegisteredHectares = f.RegisteredHectares,
            RegisteredVolume = f.RegisteredVolume,
            ELUHectares = f.ELUHectares,
            ELUVolume = f.ELUVolume,
            LawfulHectares = f.LawfulHectares,
            LawfulVolume = f.LawfulVolume,
            UnlawfulHectares = f.UnlawfulHectares,
            UnlawfulVolume = f.UnlawfulVolume,
            Pre1972Hectares = f.Pre1972Hectares,
            Pre1972Volume = f.Pre1972Volume,
            SFRAPermitNumber = f.SFRAPermitNumber,
            PeriodName = f.Period?.PeriodName
        }).ToList();

        ViewBag.PropertyId = propertyId;
        ViewBag.PropertyRef = property.PropertyReferenceNumber;
        return View(vms);
    }

    // GET: Forestation/Create?propertyId=...
    [HttpGet]
    public async Task<IActionResult> Create(Guid propertyId)
    {
        var property = await _context.Properties.FindAsync(propertyId);
        if (property == null) return NotFound();

        await PopulateDropdownsAsync();
        var vm = new ForestationViewModel { PropertyId = propertyId };
        ViewBag.PropertyRef = property.PropertyReferenceNumber;
        return View(vm);
    }

    // POST: Forestation/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ForestationViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            ViewBag.PropertyRef = (await _context.Properties.FindAsync(vm.PropertyId))?.PropertyReferenceNumber;
            return View(vm);
        }

        var property = await _context.Properties.FindAsync(vm.PropertyId);
        if (property == null) return NotFound();

        var entity = new Forestation
        {
            ForestationId = Guid.NewGuid(),
            Property = property,
            PropertyId = vm.PropertyId,
            PeriodId = vm.PeriodId,
            WithinGWCA = vm.WithinGWCA,
            Specie = vm.Specie,
            WaterResource = vm.WaterResource,
            QualifyPeriodSFRAHectares = vm.QualifyPeriodSFRAHectares,
            CurrentPeriodSFRAHectares = vm.CurrentPeriodSFRAHectares,
            QualifyPeriodVolume = vm.QualifyPeriodVolume,
            RegisteredHectares = vm.RegisteredHectares,
            RegisteredVolume = vm.RegisteredVolume,
            Pre1972Hectares = vm.Pre1972Hectares,
            Pre1972Volume = vm.Pre1972Volume,
            SFRAPermitNumber = vm.SFRAPermitNumber,
            SFRAPermitHectares = vm.SFRAPermitHectares,
            ELUHectares = vm.ELUHectares,
            ELUVolume = vm.ELUVolume,
            LawfulHectares = vm.LawfulHectares,
            LawfulVolume = vm.LawfulVolume,
            UnlawfulHectares = vm.UnlawfulHectares,
            UnlawfulVolume = vm.UnlawfulVolume,
            UnitForVolumeCalculation = vm.UnitForVolumeCalculation,
            UserFeedbackEntitlementType = vm.UserFeedbackEntitlementType,
            UserFeedbackEntitlementReference = vm.UserFeedbackEntitlementReference,
            UserFeedbackEntitlementHectares = vm.UserFeedbackEntitlementHectares,
            CommentOnFeedback = vm.CommentOnFeedback,
            CommentsOnData = vm.CommentsOnData
        };

        await _repo.RegisterForestation(entity);
        TempData["Success"] = "Forestation / SFRA record added successfully.";
        return RedirectToAction(nameof(Index), new { propertyId = vm.PropertyId });
    }

    // GET: Forestation/Edit/{id}
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return NotFound();

        await PopulateDropdownsAsync(entity.PeriodId);
        var vm = ToViewModel(entity);
        ViewBag.PropertyRef = entity.Property?.PropertyReferenceNumber
            ?? (await _context.Properties.FindAsync(entity.PropertyId))?.PropertyReferenceNumber;
        return View(vm);
    }

    // POST: Forestation/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ForestationViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(vm.PeriodId);
            return View(vm);
        }

        var existing = await _repo.GetByIdAsync(vm.ForestationId);
        if (existing == null) return NotFound();

        existing.PeriodId = vm.PeriodId;
        existing.WithinGWCA = vm.WithinGWCA;
        existing.Specie = vm.Specie;
        existing.WaterResource = vm.WaterResource;
        existing.QualifyPeriodSFRAHectares = vm.QualifyPeriodSFRAHectares;
        existing.CurrentPeriodSFRAHectares = vm.CurrentPeriodSFRAHectares;
        existing.QualifyPeriodVolume = vm.QualifyPeriodVolume;
        existing.RegisteredHectares = vm.RegisteredHectares;
        existing.RegisteredVolume = vm.RegisteredVolume;
        existing.Pre1972Hectares = vm.Pre1972Hectares;
        existing.Pre1972Volume = vm.Pre1972Volume;
        existing.SFRAPermitNumber = vm.SFRAPermitNumber;
        existing.SFRAPermitHectares = vm.SFRAPermitHectares;
        existing.ELUHectares = vm.ELUHectares;
        existing.ELUVolume = vm.ELUVolume;
        existing.LawfulHectares = vm.LawfulHectares;
        existing.LawfulVolume = vm.LawfulVolume;
        existing.UnlawfulHectares = vm.UnlawfulHectares;
        existing.UnlawfulVolume = vm.UnlawfulVolume;
        existing.UnitForVolumeCalculation = vm.UnitForVolumeCalculation;
        existing.UserFeedbackEntitlementType = vm.UserFeedbackEntitlementType;
        existing.UserFeedbackEntitlementReference = vm.UserFeedbackEntitlementReference;
        existing.UserFeedbackEntitlementHectares = vm.UserFeedbackEntitlementHectares;
        existing.CommentOnFeedback = vm.CommentOnFeedback;
        existing.CommentsOnData = vm.CommentsOnData;

        await _repo.UpdateForestation(existing);
        TempData["Success"] = "Forestation / SFRA record updated successfully.";
        return RedirectToAction(nameof(Index), new { propertyId = existing.PropertyId });
    }

    // POST: Forestation/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, Guid propertyId)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null)
        {
            TempData["Error"] = "Forestation record not found — it may have already been deleted.";
            return RedirectToAction(nameof(Index), new { propertyId });
        }
        var entityPropertyId = entity.PropertyId;
        await _repo.DeleteAsync(id);
        TempData["Success"] = "Forestation / SFRA record deleted.";
        return RedirectToAction(nameof(Index), new { propertyId = entityPropertyId });
    }

    private async Task PopulateDropdownsAsync(Guid? selectedPeriodId = null)
    {
        ViewBag.Periods = new SelectList(
            await _context.Periods.OrderBy(p => p.PeriodName).ToListAsync(),
            "PeriodId", "PeriodName", selectedPeriodId);
    }

    private static ForestationViewModel ToViewModel(Forestation f) => new()
    {
        ForestationId = f.ForestationId,
        PropertyId = f.PropertyId,
        PeriodId = f.PeriodId,
        WithinGWCA = f.WithinGWCA,
        Specie = f.Specie,
        WaterResource = f.WaterResource,
        QualifyPeriodSFRAHectares = f.QualifyPeriodSFRAHectares,
        CurrentPeriodSFRAHectares = f.CurrentPeriodSFRAHectares,
        QualifyPeriodVolume = f.QualifyPeriodVolume,
        RegisteredHectares = f.RegisteredHectares,
        RegisteredVolume = f.RegisteredVolume,
        Pre1972Hectares = f.Pre1972Hectares,
        Pre1972Volume = f.Pre1972Volume,
        SFRAPermitNumber = f.SFRAPermitNumber,
        SFRAPermitHectares = f.SFRAPermitHectares,
        ELUHectares = f.ELUHectares,
        ELUVolume = f.ELUVolume,
        LawfulHectares = f.LawfulHectares,
        LawfulVolume = f.LawfulVolume,
        UnlawfulHectares = f.UnlawfulHectares,
        UnlawfulVolume = f.UnlawfulVolume,
        UnitForVolumeCalculation = f.UnitForVolumeCalculation,
        UserFeedbackEntitlementType = f.UserFeedbackEntitlementType,
        UserFeedbackEntitlementReference = f.UserFeedbackEntitlementReference,
        UserFeedbackEntitlementHectares = f.UserFeedbackEntitlementHectares,
        CommentOnFeedback = f.CommentOnFeedback,
        CommentsOnData = f.CommentsOnData,
        PeriodName = f.Period?.PeriodName
    };
}
