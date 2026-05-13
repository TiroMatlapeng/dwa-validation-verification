using Microsoft.EntityFrameworkCore;

public class ForestationRepository : IForestation
{
    private readonly ApplicationDBContext _dbContext;

    public ForestationRepository(ApplicationDBContext dBContext) => _dbContext = dBContext;

    public async Task<Forestation> RegisterForestation(Forestation forestation)
    {
        var entry = await _dbContext.Forestations.AddAsync(forestation);
        await _dbContext.SaveChangesAsync();
        return entry.Entity;
    }

    public async Task<ICollection<Forestation>> ListAll() =>
        await _dbContext.Forestations.Include(f => f.Property).ToListAsync();

    public async Task<ICollection<Forestation>> GetByPropertyIdAsync(Guid propertyId) =>
        await _dbContext.Forestations
            .Include(f => f.Period)
            .Where(f => f.PropertyId == propertyId)
            .ToListAsync();

    public async Task<Forestation?> GetByIdAsync(Guid id) =>
        await _dbContext.Forestations
            .Include(f => f.Period)
            .FirstOrDefaultAsync(f => f.ForestationId == id);

    public async Task<Forestation> UpdateForestation(Forestation forestation)
    {
        var entry = _dbContext.Forestations.Update(forestation);
        await _dbContext.SaveChangesAsync();
        return entry.Entity;
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _dbContext.Forestations.FindAsync(id);
        if (entity != null)
        {
            _dbContext.Forestations.Remove(entity);
            await _dbContext.SaveChangesAsync();
        }
    }
}