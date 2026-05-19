using dwa_ver_val.Services.Calculator;

namespace dwa_ver_val.Tests.Services.Calculator;

public class DamVolumeCalculatorTests
{
    // Method 1: Slope = R1/C1, Depth = Fetch/Slope, Capacity = WallLength × Fetch × Depth × Factor / 2
    [Fact]
    public void Method1_ComputesCorrectCapacity()
    {
        // R1=100m, C1=10m → Slope=10; Fetch=50m → Depth=5m
        // Capacity = 200 × 50 × 5 × 0.4 / 2 = 10,000 m³
        var result = DamVolumeCalculator.ComputeMethod1(
            wallLength: 200m,
            fetch: 50m,
            riverDistance: 100m,
            contourDifference: 10m,
            shapeFactor: 0.40m);
        Assert.Equal(10_000m, result);
    }

    [Fact]
    public void Method1_RavineShapeFactor_UsesPointThreeThree()
    {
        // R1=100m, C1=10m → Slope=10; Fetch=50m → Depth=5m
        // Capacity = 200 × 50 × 5 × 0.33 / 2 = 8,250 m³
        var result = DamVolumeCalculator.ComputeMethod1(200m, 50m, 100m, 10m, 0.33m);
        Assert.Equal(8_250m, result);
    }

    [Fact]
    public void Method1_ThrowsWhenContourDifferenceIsZero()
    {
        Assert.Throws<ArgumentException>(() =>
            DamVolumeCalculator.ComputeMethod1(200m, 50m, 100m, 0m, 0.4m));
    }

    // Method 2: Capacity = Area_ha × Depth_m × Factor × 1000
    [Fact]
    public void Method2_ComputesCorrectCapacity()
    {
        // 2 ha × 3 m × 0.5 × 1000 = 3,000 m³
        var result = DamVolumeCalculator.ComputeMethod2(
            areaHa: 2m,
            depthM: 3m,
            shapeFactor: 0.5m);
        Assert.Equal(3_000m, result);
    }

    [Fact]
    public void Method2_ZeroDepth_ReturnsZero()
    {
        var result = DamVolumeCalculator.ComputeMethod2(2m, 0m, 0.4m);
        Assert.Equal(0m, result);
    }
}
