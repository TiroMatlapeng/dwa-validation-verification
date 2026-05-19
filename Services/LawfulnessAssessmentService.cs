using dwa_ver_val.Services.Calculator;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Orchestrates the two-tier ELU lawfulness assessment for a FileMaster case.
/// Loads property, FieldAndCrop (qualifying period), DamCalculation, and GWCA rules,
/// delegates to the pure <see cref="LawfulnessCalculator"/>, and persists:
///   * a <see cref="LawfulnessAssessmentResult"/> row (upsert per FileMaster), and
///   * an <see cref="Entitlement"/> row whose volume is the lawful irrigation outcome
///     (creating one and linking it on FileMaster.EntitlementId if absent).
/// </summary>
public class LawfulnessAssessmentService : ILawfulnessAssessmentService
{
    private readonly ApplicationDBContext _db;

    public LawfulnessAssessmentService(ApplicationDBContext db) => _db = db;

    public async Task<LawfulnessAssessmentResult> AssessAsync(Guid fileMasterId)
    {
        // Load FileMaster + Property (including GWCA with active rules)
        var fm = await _db.FileMasters
            .Include(f => f.Property)
                .ThenInclude(p => p!.GovernmentWaterControlArea)
                    .ThenInclude(g => g!.ProclamationRules)
            .FirstOrDefaultAsync(f => f.FileMasterId == fileMasterId)
            ?? throw new InvalidOperationException($"FileMaster {fileMasterId} not found.");

        var property = fm.Property
            ?? throw new InvalidOperationException("FileMaster has no linked Property.");

        // Qualifying period IDs (PeriodName contains "Qualifying")
        var qualifyingPeriodIds = await _db.Periods
            .Where(p => p.PeriodName.Contains("Qualifying"))
            .Select(p => p.PeriodId)
            .ToListAsync();

        // FieldAndCrop records for this property's qualifying period
        var fieldAndCrops = await _db.FieldAndCrops
            .Where(fc => fc.PropertyId == property.PropertyId
                      && qualifyingPeriodIds.Contains(fc.PeriodId))
            .ToListAsync();

        // DamCalculation records for this property
        var damCalcs = await _db.DamCalculations
            .Where(d => d.PropertyId == property.PropertyId)
            .ToListAsync();

        // Aggregate inputs
        var totalIrrigatedArea = fieldAndCrops.Sum(fc => fc.CropArea);
        var totalDemand = fieldAndCrops
            .Where(fc => fc.SAPWATCalculationResult > 0)
            .Sum(fc => SapwatCalculator.ComputeVolume(fc.CropArea, fc.SAPWATCalculationResult));
        var totalDamCapacity = damCalcs.Sum(dc => dc.DamCapacity);

        // Determine GWCA membership and load active rules
        var isInGwca = property.WaterControlAreaId.HasValue;
        var gwcaRules = isInGwca && property.GovernmentWaterControlArea?.ProclamationRules is { } ruleSet
            ? (IReadOnlyList<GwcaProclamationRule>)ruleSet.Where(r => r.IsActive).ToList()
            : Array.Empty<GwcaProclamationRule>();

        var input = new LawfulnessInput(
            TotalIrrigatedAreaHa: totalIrrigatedArea,
            TotalIrrigationDemandM3: totalDemand,
            TotalDamCapacityM3: totalDamCapacity,
            PropertyIrrigableAreaHa: property.IrrigableAreaHa,
            IsInGwca: isInGwca,
            GwcaRules: gwcaRules);

        var output = LawfulnessCalculator.Compute(input);

        // Upsert LawfulnessAssessmentResult
        var existing = await _db.LawfulnessAssessmentResults
            .FirstOrDefaultAsync(r => r.FileMasterId == fileMasterId);

        if (existing is null)
        {
            existing = new LawfulnessAssessmentResult
            {
                LawfulnessAssessmentResultId = Guid.NewGuid(),
                FileMasterId = fileMasterId,
                LegalFramework = output.LegalFramework
            };
            _db.LawfulnessAssessmentResults.Add(existing);
        }

        existing.LegalFramework = output.LegalFramework;
        existing.GwcaId = property.WaterControlAreaId;
        existing.TotalIrrigatedAreaHa = totalIrrigatedArea;
        existing.TotalIrrigationDemandM3 = totalDemand;
        existing.LawfulIrrigationM3 = output.LawfulIrrigationM3;
        existing.UnlawfulIrrigationM3 = output.UnlawfulIrrigationM3;
        existing.IrrigationLimitApplied = output.IrrigationLimitApplied;
        existing.TotalDamCapacityM3 = totalDamCapacity;
        existing.LawfulStorageM3 = output.LawfulStorageM3;
        existing.UnlawfulStorageM3 = output.UnlawfulStorageM3;
        existing.StorageLimitApplied = output.StorageLimitApplied;
        existing.AssessedAt = DateTime.UtcNow;

        // Create or update Entitlement (lawful irrigation volume = the ELU outcome)
        var eluType = await _db.EntitlementTypes
            .FirstOrDefaultAsync(t => t.EntitlementName == "ELU_Irrigation")
            ?? throw new InvalidOperationException(
                "EntitlementType 'ELU_Irrigation' not found. Run seed data (SeedEntitlementTypesAsync) first.");

        if (fm.EntitlementId.HasValue)
        {
            var entitlement = await _db.Entitlements.FindAsync(fm.EntitlementId.Value);
            if (entitlement is not null)
                entitlement.Volume = output.LawfulIrrigationM3;
        }
        else
        {
            var newEntitlement = new Entitlement
            {
                EntitlementId = Guid.NewGuid(),
                Name = $"ELU — {fm.RegistrationNumber}",
                Volume = output.LawfulIrrigationM3,
                EntitlementTypeId = eluType.EntitlementTypeId
            };
            _db.Entitlements.Add(newEntitlement);
            fm.EntitlementId = newEntitlement.EntitlementId;
        }

        await _db.SaveChangesAsync();
        return existing;
    }
}
