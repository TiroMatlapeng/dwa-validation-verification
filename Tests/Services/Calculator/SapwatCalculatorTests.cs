using dwa_ver_val.Services.Calculator;

namespace dwa_ver_val.Tests.Services.Calculator;

public class SapwatCalculatorTests
{
    [Fact]
    public void ComputeRate_MultipliesLookupRateByRotationFactor()
    {
        var result = SapwatCalculator.ComputeRate(lookupRatePerHaPerAnnum: 600m, rotationFactor: 0.75m);
        Assert.Equal(450m, result);
    }

    [Fact]
    public void ComputeRate_RotationFactorOne_ReturnsFullRate()
    {
        var result = SapwatCalculator.ComputeRate(800m, 1.0m);
        Assert.Equal(800m, result);
    }

    [Fact]
    public void ComputeVolume_ConvertsMmPerHaToM3()
    {
        // 500 mm/ha/a × 2 ha × 10 conversion = 10,000 m³/year
        var result = SapwatCalculator.ComputeVolume(cropAreaHa: 2m, ratePerHaPerAnnum: 500m);
        Assert.Equal(10_000m, result);
    }

    [Fact]
    public void ComputeVolume_ZeroArea_ReturnsZero()
    {
        var result = SapwatCalculator.ComputeVolume(0m, 600m);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void ComputeRate_ZeroRotationFactor_ReturnsZero()
    {
        var result = SapwatCalculator.ComputeRate(600m, 0m);
        Assert.Equal(0m, result);
    }
}
