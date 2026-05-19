using dwa_ver_val.Services.Calculator;

namespace dwa_ver_val.Tests.Services.Calculator;

public class SfraCalculatorTests
{
    [Fact]
    public void Compute_Pre1972Only_FullyLawful()
    {
        var result = SfraCalculator.Compute(
            pre1972Ha: 10m,
            sfraPermitHa: 0m,
            qualifyingHa: 10m,
            speciesRateM3PerHaPerAnnum: 6500m);

        Assert.Equal(10m, result.EluHa);
        Assert.Equal(10m, result.LawfulHa);
        Assert.Equal(0m, result.UnlawfulHa);
        Assert.Equal(65_000m, result.EluVolume);
        Assert.Equal(65_000m, result.LawfulVolume);
        Assert.Equal(0m, result.UnlawfulVolume);
    }

    [Fact]
    public void Compute_PermitOnly_CapToPermitExtent()
    {
        var result = SfraCalculator.Compute(0m, 8m, 12m, 6500m);

        Assert.Equal(8m, result.EluHa);
        Assert.Equal(8m, result.LawfulHa);
        Assert.Equal(4m, result.UnlawfulHa);
        Assert.Equal(52_000m, result.EluVolume);
        Assert.Equal(52_000m, result.LawfulVolume);
        Assert.Equal(26_000m, result.UnlawfulVolume);
    }

    [Fact]
    public void Compute_Pre1972AndPermitCombined_SumsToQualifyingCap()
    {
        // 5 ha pre-1972 + 8 ha permit = 13 ha → capped to 10 ha qualifying
        var result = SfraCalculator.Compute(5m, 8m, 10m, 6500m);

        Assert.Equal(10m, result.EluHa);
        Assert.Equal(10m, result.LawfulHa);
        Assert.Equal(0m, result.UnlawfulHa);
    }

    [Fact]
    public void Compute_ZeroQualifying_AllZero()
    {
        var result = SfraCalculator.Compute(5m, 3m, 0m, 6500m);
        Assert.Equal(0m, result.EluHa);
        Assert.Equal(0m, result.UnlawfulHa);
    }
}
