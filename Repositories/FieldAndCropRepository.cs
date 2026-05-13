using Microsoft.EntityFrameworkCore;

public class FieldAndCropRepository : IFieldAndCrop
{
    private readonly ApplicationDBContext _db;

    public FieldAndCropRepository(ApplicationDBContext db) => _db = db;

    public async Task<FieldAndCrop> AddFieldAndCrop(FieldAndCrop fieldAndCrop)
    {
        var entry = await _db.FieldAndCrops.AddAsync(fieldAndCrop);
        await _db.SaveChangesAsync();
        return entry.Entity;
    }

    public async Task<FieldAndCrop> UpdateFieldAndCrop(FieldAndCrop fieldAndCrop)
    {
        var entry = _db.FieldAndCrops.Update(fieldAndCrop);
        await _db.SaveChangesAsync();
        return entry.Entity;
    }

    public async Task<ICollection<FieldAndCrop>> ListAll() =>
        await _db.FieldAndCrops.Include(f => f.Crop).Include(f => f.WaterSource)
            .Include(f => f.IrrigationSystem).Include(f => f.Period).ToListAsync();

    public async Task<ICollection<FieldAndCrop>> GetByPropertyIdAsync(Guid propertyId) =>
        await _db.FieldAndCrops
            .Include(f => f.Crop).Include(f => f.WaterSource)
            .Include(f => f.IrrigationSystem).Include(f => f.Period)
            .Where(f => f.PropertyId == propertyId)
            .ToListAsync();

    public async Task<FieldAndCrop?> GetByIdAsync(Guid id) =>
        await _db.FieldAndCrops
            .Include(f => f.Crop).Include(f => f.WaterSource)
            .Include(f => f.IrrigationSystem).Include(f => f.Period)
            .FirstOrDefaultAsync(f => f.FieldAndCropId == id);

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _db.FieldAndCrops.FindAsync(id);
        if (entity != null)
        {
            _db.FieldAndCrops.Remove(entity);
            await _db.SaveChangesAsync();
        }
    }
}
