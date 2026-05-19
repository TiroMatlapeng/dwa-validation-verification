namespace dwa_ver_val.Services.Calculator;

/// <summary>
/// Orchestrates calculator pipelines against the database.
/// Loads the relevant entity, looks up reference rates, invokes the matching pure
/// calculator, and persists the result back to the entity.
/// </summary>
public interface ICalculatorService
{
    /// <summary>Compute SAPWAT effective rate for one FieldAndCrop; saves to SAPWATCalculationResult.</summary>
    Task<decimal> ComputeSapwatAsync(Guid fieldAndCropId);

    /// <summary>Compute dam capacity for one DamCalculation; saves to DamCapacity.</summary>
    Task<decimal> ComputeDamVolumeAsync(Guid damCalculationId);

    /// <summary>Compute SFRA ELU for one Forestation; saves ELU/Lawful/Unlawful fields.</summary>
    Task<SfraResult> ComputeSfraAsync(Guid forestationId);
}
