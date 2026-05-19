namespace dwa_ver_val.Services.Calculator;

public record SfraResult
{
    public decimal EluHa { get; init; }
    public decimal EluVolume { get; init; }
    public decimal LawfulHa { get; init; }
    public decimal LawfulVolume { get; init; }
    public decimal UnlawfulHa { get; init; }
    public decimal UnlawfulVolume { get; init; }
}

/// <summary>
/// Pure SFRA (Stream Flow Reduction Activity) ELU calculator.
/// Rules: Pre-1972 planting = fully lawful ELU; Post-1984 SFRA permit = lawful to extent of permit.
/// Any qualifying-period area beyond the authorised extent is unlawful.
/// </summary>
public static class SfraCalculator
{
    public static SfraResult Compute(
        decimal pre1972Ha,
        decimal sfraPermitHa,
        decimal qualifyingHa,
        decimal speciesRateM3PerHaPerAnnum)
    {
        if (qualifyingHa <= 0) return new SfraResult();

        var authorisedHa = Math.Min(pre1972Ha + sfraPermitHa, qualifyingHa);
        var unlawfulHa = qualifyingHa - authorisedHa;

        var eluVolume = authorisedHa * speciesRateM3PerHaPerAnnum;
        var unlawfulVolume = unlawfulHa * speciesRateM3PerHaPerAnnum;

        return new SfraResult
        {
            EluHa = authorisedHa,
            EluVolume = eluVolume,
            LawfulHa = authorisedHa,
            LawfulVolume = eluVolume,
            UnlawfulHa = unlawfulHa,
            UnlawfulVolume = unlawfulVolume
        };
    }
}
