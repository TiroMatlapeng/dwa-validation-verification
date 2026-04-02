using Microsoft.EntityFrameworkCore;

public class FileMasterRepository : IFileMaster
{
    private readonly ApplicationDBContext _context;

    public FileMasterRepository(ApplicationDBContext context)
    {
        _context = context;
    }

    public async Task<FileMaster> AddAsync(FileMaster fileMaster)
    {
        await _context.FileMasters.AddAsync(fileMaster);
        await _context.SaveChangesAsync();
        return fileMaster;
    }

    public async Task<FileMaster?> GetByIdAsync(Guid id)
    {
        return await _context.FileMasters
            .Include(fm => fm.Property)
            .Include(fm => fm.OrgUnit)
            .Include(fm => fm.Validator)
            .Include(fm => fm.CapturePerson)
            .Include(fm => fm.Entitlement)
            .FirstOrDefaultAsync(fm => fm.FileMasterId == id);
    }

    public async Task<ICollection<FileMaster>> ListAllAsync()
    {
        return await _context.FileMasters
            .Include(fm => fm.Property)
            .Include(fm => fm.OrgUnit)
            .Include(fm => fm.Validator)
            .Include(fm => fm.CapturePerson)
            .Include(fm => fm.Entitlement)
            .ToListAsync();
    }

    public async Task<ICollection<FileMaster>> ListByPropertyIdAsync(Guid propertyId)
    {
        return await _context.FileMasters
            .Where(fm => fm.PropertyId == propertyId)
            .ToListAsync();
    }

    public async Task<FileMaster> UpdateAsync(FileMaster fileMaster)
    {
        _context.FileMasters.Update(fileMaster);
        await _context.SaveChangesAsync();
        return fileMaster;
    }

    public async Task<FileMaster?> DeleteAsync(Guid id)
    {
        var fileMaster = await _context.FileMasters
            .FirstOrDefaultAsync(fm => fm.FileMasterId == id);

        if (fileMaster == null)
        {
            return null;
        }

        _context.FileMasters.Remove(fileMaster);
        await _context.SaveChangesAsync();
        return fileMaster;
    }
}
