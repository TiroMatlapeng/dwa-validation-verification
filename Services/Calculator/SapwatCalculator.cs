namespace dwa_ver_val.Services.Calculator;

/// <summary>
/// Pure SAPWAT-style crop water demand calculator.
/// No DI, no DB — operates on seeded lookup values supplied by the caller.
/// Reference: DWS V&amp;V Requirements (Edition 3, July 2024) — Appendix C (SAPWAT modelling).
/// </summary>
public static class SapwatCalculator
{
    /// <summary>
    /// Applies the rotation factor to the seeded crop water demand rate.
    /// </summary>
    /// <param name="lookupRatePerHaPerAnnum">Reference rate in mm/ha/a from CropWaterRate seed data.</param>
    /// <param name="rotationFactor">FieldAndCrop.RotationFactor (0–1 for partial season crops, 1 for full season).</param>
    /// <returns>Effective demand rate in mm/ha/a to store in FieldAndCrop.SAPWATCalculationResult.</returns>
    public static decimal ComputeRate(decimal lookupRatePerHaPerAnnum, decimal rotationFactor)
        => lookupRatePerHaPerAnnum * rotationFactor;

    /// <summary>
    /// Converts an area × rate combination into an annual volume.
    /// 1 mm depth over 1 ha = 10 m³.
    /// </summary>
    /// <param name="cropAreaHa">FieldAndCrop.CropArea in hectares.</param>
    /// <param name="ratePerHaPerAnnum">Effective demand in mm/ha/a (output of ComputeRate).</param>
    /// <returns>Annual water volume in m³.</returns>
    public static decimal ComputeVolume(decimal cropAreaHa, decimal ratePerHaPerAnnum)
        => cropAreaHa * ratePerHaPerAnnum * 10m;
}
