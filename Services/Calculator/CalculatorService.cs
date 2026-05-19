using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Services.Calculator;

/// <summary>
/// DI-friendly orchestrator over the pure calculators in this namespace.
/// Loads the entity, looks up the appropriate reference rate, invokes the pure
/// calculator, and persists the result.
/// </summary>
public class CalculatorService : ICalculatorService
{
    private readonly ApplicationDBContext _db;

    public CalculatorService(ApplicationDBContext db) => _db = db;

    public async Task<decimal> ComputeSapwatAsync(Guid fieldAndCropId)
    {
        var fc = await _db.FieldAndCrops
            .Include(f => f.Crop)
            .Include(f => f.IrrigationSystem)
            .FirstOrDefaultAsync(f => f.FieldAndCropId == fieldAndCropId);

        if (fc is null)
            throw new InvalidOperationException($"FieldAndCrop {fieldAndCropId} not found.");
        if (fc.Crop is null)
            throw new InvalidOperationException("FieldAndCrop has no Crop linked — select a crop before calculating.");

        var irrigationSystemId = fc.IrrigationSystem?.IrrigationSystemId;

        // Prefer a system-specific rate; fall back to the crop-wide rate (IrrigationSystemId == null).
        var rate = await _db.CropWaterRates
            .Where(r => r.CropId == fc.Crop.CropId &&
                        (r.IrrigationSystemId == null ||
                         r.IrrigationSystemId == irrigationSystemId))
            .OrderByDescending(r => r.IrrigationSystemId != null)
            .Select(r => (decimal?)r.RatePerHaPerAnnum)
            .FirstOrDefaultAsync();

        if (rate is null)
            throw new InvalidOperationException(
                $"No CropWaterRate found for crop '{fc.Crop.CropName}'. Seed reference data first.");

        var effectiveRate = SapwatCalculator.ComputeRate(rate.Value, fc.RotationFactor);
        fc.SAPWATCalculationResult = effectiveRate;
        fc.LastCalculatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return effectiveRate;
    }

    public async Task<decimal> ComputeDamVolumeAsync(Guid damCalculationId)
    {
        var dam = await _db.DamCalculations.FirstOrDefaultAsync(d => d.DamCalculationId == damCalculationId)
            ?? throw new InvalidOperationException($"DamCalculation {damCalculationId} not found.");

        decimal capacity;
        try
        {
            capacity = dam.CalculationMethod switch
            {
                "Method1" => DamVolumeCalculator.ComputeMethod1(
                    dam.WallLength ?? throw new InvalidOperationException("WallLength required for Method 1."),
                    dam.Fetch ?? throw new InvalidOperationException("Fetch required for Method 1."),
                    dam.RiverDistance ?? throw new InvalidOperationException("RiverDistance required for Method 1."),
                    dam.ContourDifference ?? throw new InvalidOperationException("ContourDifference required for Method 1."),
                    dam.ShapeFactor ?? throw new InvalidOperationException("ShapeFactor required for Method 1.")),
                "Method2" => DamVolumeCalculator.ComputeMethod2(
                    dam.DamArea ?? throw new InvalidOperationException("DamArea required for Method 2."),
                    dam.DamDepth ?? throw new InvalidOperationException("DamDepth required for Method 2."),
                    dam.ShapeFactor ?? throw new InvalidOperationException("ShapeFactor required for Method 2.")),
                _ => throw new InvalidOperationException(
                    $"Unknown CalculationMethod '{dam.CalculationMethod}'. Use 'Method1' or 'Method2'.")
            };
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }

        dam.DamCapacity = capacity;
        dam.LastCalculatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return capacity;
    }

    public async Task<SfraResult> ComputeSfraAsync(Guid forestationId)
    {
        var f = await _db.Forestations.FirstOrDefaultAsync(x => x.ForestationId == forestationId)
            ?? throw new InvalidOperationException($"Forestation {forestationId} not found.");

        if (string.IsNullOrWhiteSpace(f.Specie))
            throw new InvalidOperationException("Forestation record has no species set. Enter a species name before calculating.");

        var speciesName = f.Specie.Trim();
        // Use ToLower for case-insensitive parity between SQL Server and the in-memory EF provider used in tests.
        var speciesRate = await _db.SfraSpeciesRates
            .Where(r => r.SpeciesName.ToLower() == speciesName.ToLower())
            .Select(r => (decimal?)r.RateM3PerHaPerAnnum)
            .FirstOrDefaultAsync();

        if (speciesRate is null)
            throw new InvalidOperationException(
                $"No SfraSpeciesRate found for species '{speciesName}'. Add it to the reference data first.");

        var result = SfraCalculator.Compute(
            f.Pre1972Hectares,
            f.SFRAPermitHectares,
            f.QualifyPeriodSFRAHectares ?? 0m,
            speciesRate.Value);

        f.ELUHectares = result.EluHa;
        f.ELUVolume = result.EluVolume;
        f.LawfulHectares = result.LawfulHa;
        f.LawfulVolume = result.LawfulVolume;
        f.UnlawfulHectares = result.UnlawfulHa;
        f.UnlawfulVolume = result.UnlawfulVolume;
        f.LastCalculatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return result;
    }
}
