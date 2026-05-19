namespace dwa_ver_val.Services.Calculator;

/// <summary>
/// Input to the two-tier ELU lawfulness calculation.
/// Built by LawfulnessAssessmentService from DB data.
/// </summary>
public record LawfulnessInput(
    decimal TotalIrrigatedAreaHa,
    decimal TotalIrrigationDemandM3,
    decimal TotalDamCapacityM3,
    decimal? PropertyIrrigableAreaHa,
    bool IsInGwca,
    IReadOnlyList<GwcaProclamationRule> GwcaRules);

/// <summary>
/// Output of the two-tier ELU lawfulness calculation.
/// </summary>
public record LawfulnessOutput(
    string LegalFramework,
    decimal LawfulIrrigationM3,
    decimal UnlawfulIrrigationM3,
    string IrrigationLimitApplied,
    decimal LawfulStorageM3,
    decimal UnlawfulStorageM3,
    string StorageLimitApplied);

/// <summary>
/// Pure two-tier ELU lawfulness calculator.
/// No DI, no DB — operates on data supplied by the caller.
/// Tier 1: GWCA proclamation rules (property-specific, from GwcaProclamationRule).
/// Tier 2: General S9B statutory limits (hardcoded from National Water Act).
/// </summary>
public static class LawfulnessCalculator
{
    // S9B statutory limits — hardcoded from statute (not configurable)
    public const decimal S9bStorageLimitM3 = 250_000m;
    public const decimal S9bAbstractionLimitM3PerYear = 3_468_960m; // 110 l/s × 31,536,000 s ÷ 1,000

    public static LawfulnessOutput Compute(LawfulnessInput input) =>
        input.IsInGwca ? ComputeGwca(input) : ComputeGeneral(input);

    public static LawfulnessOutput ComputeGeneral(LawfulnessInput input)
    {
        var lawfulIrrigation = Math.Min(input.TotalIrrigationDemandM3, S9bAbstractionLimitM3PerYear);
        var unlawfulIrrigation = input.TotalIrrigationDemandM3 - lawfulIrrigation;

        var lawfulStorage = Math.Min(input.TotalDamCapacityM3, S9bStorageLimitM3);
        var unlawfulStorage = input.TotalDamCapacityM3 - lawfulStorage;

        return new LawfulnessOutput(
            LegalFramework: "General",
            LawfulIrrigationM3: lawfulIrrigation,
            UnlawfulIrrigationM3: unlawfulIrrigation,
            IrrigationLimitApplied: $"S9B statutory limit: {S9bAbstractionLimitM3PerYear:N0} m³/year (110 l/s)",
            LawfulStorageM3: lawfulStorage,
            UnlawfulStorageM3: unlawfulStorage,
            StorageLimitApplied: $"S9B statutory limit: {S9bStorageLimitM3:N0} m³ without permit");
    }

    public static LawfulnessOutput ComputeGwca(LawfulnessInput input)
    {
        var rules = input.GwcaRules;
        var irrigableArea = input.PropertyIrrigableAreaHa ?? input.TotalIrrigatedAreaHa;

        // MAX_HECTARES: base entitlement — whichever is greater: rule limit OR 40% of irrigable
        var maxHaLimit = GetLimit(rules, "MAX_HECTARES") ?? 30m;
        var effectiveMaxHa = Math.Max(maxHaLimit, 0.40m * irrigableArea);

        // MAX_IRRIGABLE_PCT: absolute cap as percentage of irrigable area
        var maxPct = GetLimit(rules, "MAX_IRRIGABLE_PCT") ?? 53m;
        var pctCapHa = (maxPct / 100m) * irrigableArea;

        var allowedIrrigatedHa = Math.Min(input.TotalIrrigatedAreaHa,
                                          Math.Min(effectiveMaxHa, pctCapHa));

        // MAX_VOLUME_PER_HA: abstraction limit
        var maxVolPerHa = GetLimit(rules, "MAX_VOLUME_PER_HA") ?? 9_900m;
        var irrigationLimit = maxVolPerHa * allowedIrrigatedHa;
        var lawfulIrrigation = Math.Min(input.TotalIrrigationDemandM3, irrigationLimit);
        var unlawfulIrrigation = input.TotalIrrigationDemandM3 - lawfulIrrigation;

        // Storage: lesser of per-ha and per-property limits
        var maxStoragePerHa = GetLimit(rules, "MAX_STORAGE_PER_HA") ?? 5_000m;
        var maxStoragePerProp = GetLimit(rules, "MAX_STORAGE_PER_PROPERTY") ?? 50_000m;
        var storageLimit = Math.Min(maxStoragePerHa * allowedIrrigatedHa, maxStoragePerProp);
        var lawfulStorage = Math.Min(input.TotalDamCapacityM3, storageLimit);
        var unlawfulStorage = input.TotalDamCapacityM3 - lawfulStorage;

        return new LawfulnessOutput(
            LegalFramework: "GWCA",
            LawfulIrrigationM3: lawfulIrrigation,
            UnlawfulIrrigationM3: unlawfulIrrigation,
            IrrigationLimitApplied: $"GWCA MAX_VOLUME_PER_HA: {maxVolPerHa:N0} m³/ha × {allowedIrrigatedHa:N1} ha = {irrigationLimit:N0} m³",
            LawfulStorageM3: lawfulStorage,
            UnlawfulStorageM3: unlawfulStorage,
            StorageLimitApplied: $"GWCA: min({maxStoragePerHa:N0} m³/ha × {allowedIrrigatedHa:N1} ha, {maxStoragePerProp:N0} m³ per property) = {storageLimit:N0} m³");
    }

    private static decimal? GetLimit(IReadOnlyList<GwcaProclamationRule> rules, string code)
        => rules.FirstOrDefault(r => r.RuleCode == code && r.IsActive)?.NumericLimit;
}
