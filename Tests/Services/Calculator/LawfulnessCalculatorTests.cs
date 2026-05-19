using dwa_ver_val.Services.Calculator;
using Xunit;

namespace dwa_ver_val.Tests.Services.Calculator;

public class LawfulnessCalculatorTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static LawfulnessInput GeneralInput(
        decimal irrigatedHa = 10m,
        decimal demandM3 = 80_000m,
        decimal damCapacityM3 = 50_000m,
        decimal? irrigableHa = null)
        => new(irrigatedHa, demandM3, damCapacityM3, irrigableHa, false, Array.Empty<GwcaProclamationRule>());

    private static LawfulnessInput GwcaInput(
        decimal irrigatedHa = 20m,
        decimal demandM3 = 150_000m,
        decimal damCapacityM3 = 40_000m,
        decimal? irrigableHa = 80m,
        decimal maxHa = 30m,
        decimal maxIrrigablePct = 53m,
        decimal maxVolPerHa = 9_900m,
        decimal maxStoragePerHa = 5_000m,
        decimal maxStoragePerProp = 50_000m)
    {
        var rules = new List<GwcaProclamationRule>
        {
            new() { RuleId = Guid.NewGuid(), WaterControlAreaId = Guid.NewGuid(), RuleCode = "MAX_HECTARES",             IsActive = true, NumericLimit = maxHa,           Unit = "ha",    RuleDescription = "Max ha" },
            new() { RuleId = Guid.NewGuid(), WaterControlAreaId = Guid.NewGuid(), RuleCode = "MAX_IRRIGABLE_PCT",        IsActive = true, NumericLimit = maxIrrigablePct,  Unit = "pct",   RuleDescription = "Max pct" },
            new() { RuleId = Guid.NewGuid(), WaterControlAreaId = Guid.NewGuid(), RuleCode = "MAX_VOLUME_PER_HA",        IsActive = true, NumericLimit = maxVolPerHa,      Unit = "m3/ha", RuleDescription = "Max vol/ha" },
            new() { RuleId = Guid.NewGuid(), WaterControlAreaId = Guid.NewGuid(), RuleCode = "MAX_STORAGE_PER_HA",       IsActive = true, NumericLimit = maxStoragePerHa,  Unit = "m3/ha", RuleDescription = "Max storage/ha" },
            new() { RuleId = Guid.NewGuid(), WaterControlAreaId = Guid.NewGuid(), RuleCode = "MAX_STORAGE_PER_PROPERTY", IsActive = true, NumericLimit = maxStoragePerProp, Unit = "m3",   RuleDescription = "Max storage prop" },
        };
        return new(irrigatedHa, demandM3, damCapacityM3, irrigableHa, true, rules);
    }

    // ── General path ─────────────────────────────────────────────────────────

    [Fact]
    public void ComputeGeneral_BelowBothLimits_AllLawful()
    {
        var result = LawfulnessCalculator.ComputeGeneral(GeneralInput(demandM3: 80_000m, damCapacityM3: 50_000m));
        Assert.Equal("General", result.LegalFramework);
        Assert.Equal(80_000m, result.LawfulIrrigationM3);
        Assert.Equal(0m, result.UnlawfulIrrigationM3);
        Assert.Equal(50_000m, result.LawfulStorageM3);
        Assert.Equal(0m, result.UnlawfulStorageM3);
    }

    [Fact]
    public void ComputeGeneral_DamExceedsS9bLimit_StorageCapped()
    {
        var result = LawfulnessCalculator.ComputeGeneral(GeneralInput(damCapacityM3: 300_000m));
        Assert.Equal(250_000m, result.LawfulStorageM3);
        Assert.Equal(50_000m, result.UnlawfulStorageM3);
    }

    [Fact]
    public void ComputeGeneral_DemandExceedsS9bAbstraction_IrrigationCapped()
    {
        var result = LawfulnessCalculator.ComputeGeneral(GeneralInput(demandM3: 4_000_000m));
        Assert.Equal(LawfulnessCalculator.S9bAbstractionLimitM3PerYear, result.LawfulIrrigationM3);
        Assert.True(result.UnlawfulIrrigationM3 > 0m);
    }

    [Fact]
    public void Compute_WhenNotInGwca_DelegatesToGeneral()
    {
        var input = GeneralInput(); // IsInGwca = false
        var result = LawfulnessCalculator.Compute(input);
        Assert.Equal("General", result.LegalFramework);
    }

    // ── GWCA path ─────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeGwca_WithinAllLimits_AllLawful()
    {
        // irrigated=20ha, irrigable=80ha
        // effectiveMaxHa = max(30, 0.40×80) = max(30, 32) = 32 ha
        // pctCap = 0.53×80 = 42.4 ha
        // allowedHa = min(20, min(32, 42.4)) = 20 ha
        // irrigationLimit = 9900 × 20 = 198,000 m³
        // demand=150,000 < 198,000 → all lawful
        // storageLimit = min(5000×20, 50000) = min(100000, 50000) = 50000
        // dam=40,000 < 50,000 → all lawful
        var result = LawfulnessCalculator.ComputeGwca(GwcaInput(irrigatedHa: 20m, demandM3: 150_000m, damCapacityM3: 40_000m, irrigableHa: 80m));
        Assert.Equal("GWCA", result.LegalFramework);
        Assert.Equal(150_000m, result.LawfulIrrigationM3);
        Assert.Equal(0m, result.UnlawfulIrrigationM3);
        Assert.Equal(40_000m, result.LawfulStorageM3);
        Assert.Equal(0m, result.UnlawfulStorageM3);
    }

    [Fact]
    public void ComputeGwca_DemandExceedsVolumePerHaLimit_IrrigationCapped()
    {
        // allowedHa = 20 (all within limits), irrigationLimit = 9900×20 = 198,000
        // demand=250,000 > 198,000 → unlawful = 52,000
        var result = LawfulnessCalculator.ComputeGwca(GwcaInput(irrigatedHa: 20m, demandM3: 250_000m, irrigableHa: 80m));
        Assert.Equal(198_000m, result.LawfulIrrigationM3);
        Assert.Equal(52_000m, result.UnlawfulIrrigationM3);
    }

    [Fact]
    public void ComputeGwca_DamExceedsPerPropertyLimit_StorageCapped()
    {
        // dam=60,000 > MAX_STORAGE_PER_PROPERTY=50,000
        var result = LawfulnessCalculator.ComputeGwca(GwcaInput(damCapacityM3: 60_000m));
        Assert.Equal(50_000m, result.LawfulStorageM3);
        Assert.Equal(10_000m, result.UnlawfulStorageM3);
    }

    [Fact]
    public void ComputeGwca_IrrigatedAreaCappedByMaxHa_ReducesVolumeLimit()
    {
        // irrigated=50ha, irrigable=60ha
        // effectiveMaxHa = max(30, 0.40×60) = max(30, 24) = 30 ha
        // pctCap = 0.53×60 = 31.8 ha
        // allowedHa = min(50, min(30, 31.8)) = 30 ha
        // irrigationLimit = 9900 × 30 = 297,000
        var result = LawfulnessCalculator.ComputeGwca(GwcaInput(irrigatedHa: 50m, demandM3: 300_000m, irrigableHa: 60m));
        Assert.Equal(297_000m, result.LawfulIrrigationM3);
        Assert.Equal(3_000m, result.UnlawfulIrrigationM3);
    }

    [Fact]
    public void Compute_WhenInGwca_DelegatesToGwca()
    {
        var input = GwcaInput();
        var result = LawfulnessCalculator.Compute(input);
        Assert.Equal("GWCA", result.LegalFramework);
    }
}
