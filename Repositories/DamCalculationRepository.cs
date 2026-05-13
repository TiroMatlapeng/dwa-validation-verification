using Microsoft.EntityFrameworkCore;

public class DamCalculationRepository : IDamCalculation
{
    private readonly ApplicationDBContext _db;

    public DamCalculationRepository(ApplicationDBContext db) => _db = db;

    public async Task<DamCalculation> AddCalculationAsync(DamCalculation damCalculation)
    {
        var entry = await _db.DamCalculations.AddAsync(damCalculation);
        await _db.SaveChangesAsync();
        return entry.Entity;
    }

    public async Task<DamCalculation> UpdateCalculationAsync(DamCalculation damCalculation)
    {
        var entry = _db.DamCalculations.Update(damCalculation);
        await _db.SaveChangesAsync();
        return entry.Entity;
    }

    public async Task<ICollection<DamCalculation>> GetByPropertyIdAsync(Guid propertyId) =>
        await _db.DamCalculations
            .Include(d => d.River)
            .Where(d => d.PropertyId == propertyId)
            .OrderByDescending(d => d.CalculationDate)
            .ToListAsync();

    public async Task<DamCalculation?> GetByIdAsync(Guid id) =>
        await _db.DamCalculations
            .Include(d => d.River)
            .Include(d => d.Property)
            .FirstOrDefaultAsync(d => d.DamCalculationId == id);

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _db.DamCalculations.FindAsync(id);
        if (entity != null)
        {
            _db.DamCalculations.Remove(entity);
            await _db.SaveChangesAsync();
        }
    }
}
