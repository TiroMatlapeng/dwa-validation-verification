public interface IDamCalculation
{
    Task<DamCalculation> AddCalculationAsync(DamCalculation damCalculation);
    Task<DamCalculation> UpdateCalculationAsync(DamCalculation damCalculation);
    Task<ICollection<DamCalculation>> GetByPropertyIdAsync(Guid propertyId);
    Task<DamCalculation?> GetByIdAsync(Guid id);
    Task DeleteAsync(Guid id);
}