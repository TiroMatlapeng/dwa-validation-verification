namespace dwa_ver_val.Services.Calculator;

/// <summary>
/// Pure Appendix D dam volume calculator. No DI, no DB access.
/// Formula source: DWS V&amp;V Requirements Ed.3 Appendix D.
/// </summary>
public static class DamVolumeCalculator
{
    /// <summary>
    /// Method 1 — Wall Length method.
    /// Slope = RiverDistance / ContourDifference
    /// Depth = Fetch / Slope
    /// Capacity (m³) = WallLength × Fetch × Depth × ShapeFactor / 2
    /// </summary>
    public static decimal ComputeMethod1(
        decimal wallLength,
        decimal fetch,
        decimal riverDistance,
        decimal contourDifference,
        decimal shapeFactor)
    {
        if (contourDifference == 0)
            throw new ArgumentException("ContourDifference must be non-zero to compute slope.", nameof(contourDifference));
        if (riverDistance == 0)
            throw new ArgumentException("RiverDistance must be non-zero.", nameof(riverDistance));

        var slope = riverDistance / contourDifference;
        var depth = fetch / slope;
        return wallLength * fetch * depth * shapeFactor / 2m;
    }

    /// <summary>
    /// Method 2 — Area method.
    /// Capacity (m³) = Area (ha) × Depth (m) × ShapeFactor × 1000
    /// </summary>
    public static decimal ComputeMethod2(decimal areaHa, decimal depthM, decimal shapeFactor)
        => areaHa * depthM * shapeFactor * 1000m;
}
