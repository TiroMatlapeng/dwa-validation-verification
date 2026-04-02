using Microsoft.EntityFrameworkCore;

public class PropertyRepository : IPropertyInterface
{
    private readonly ApplicationDBContext _context;

    public PropertyRepository(ApplicationDBContext context)
    {
        _context = context;
    }

    public async Task<Property> AddAsync(Property property)
    {
        await _context.Properties.AddAsync(property);
        await _context.SaveChangesAsync();
        return property;
    }

    public async Task<Property?> GetByIdAsync(Guid id)
    {
        return await _context.Properties
            .Include(p => p.Address)
            .Include(p => p.WaterManagementArea)
            .Include(p => p.FileMasters)
            .FirstOrDefaultAsync(p => p.PropertyId == id);
    }

    public async Task<ICollection<Property>> ListAllAsync()
    {
        return await _context.Properties
            .Include(p => p.Address)
            .Include(p => p.WaterManagementArea)
            .ToListAsync();
    }

    public async Task<ICollection<Property>> ListByProvinceAsync(string provinceName)
    {
        return await _context.Properties
            .Include(p => p.Address)
            .Include(p => p.WaterManagementArea)
            .Where(p => p.Address != null && p.Address.Province == provinceName)
            .ToListAsync();
    }

    public async Task<Property> UpdateAsync(Property property)
    {
        _context.Properties.Update(property);
        await _context.SaveChangesAsync();
        return property;
    }

    public async Task<Property?> DeleteAsync(Guid id)
    {
        var property = await _context.Properties
            .FirstOrDefaultAsync(p => p.PropertyId == id);

        if (property == null)
        {
            return null;
        }

        _context.Properties.Remove(property);
        await _context.SaveChangesAsync();
        return property;
    }
}
