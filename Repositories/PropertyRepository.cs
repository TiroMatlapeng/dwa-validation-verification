using Microsoft.EntityFrameworkCore;

public class PropertyRepository : IPropertyInterface
{
    private readonly ApplicationDBContext _context;

    public PropertyRepository(ApplicationDBContext context)
    {
        _context = context;
    }

    public void TransferOwnership(ICollection<PropertyOwner> propertyOwners)
    {
        throw new NotImplementedException();
    }

    public ICollection<Property> ListPropertyByOwner(PropertyOwner propertyOwner)
    {
        throw new NotImplementedException();
    }

    public async Task<Property?> DeleteProperty(Guid PropertyId)
    {
        var property = await _context.Properties
                       .FirstOrDefaultAsync(Property => Property.PropertyId == PropertyId);
        if(property == null){
            return null;
        }
         _context.Properties.Remove(property);
         await _context.SaveChangesAsync();
         return property;
    }

    public Property UpdateProperty(Property Property)
    {
         
         _context.Properties.Update(Property);
         _context.SaveChanges();
         return Property;
    }

    public Property AddProperty(Property Property)
    {
          _context.Properties.Add(Property);
          _context.SaveChanges();
          return Property;
    }


    public ICollection<Property> ListPropertyByProvince(string provinceName)
    {
        return _context.Properties
            .Where(p => p.PropertyAddress != null && p.PropertyAddress.Province == provinceName)
            .ToList();
    }

    public ICollection<Property> ListAll()
    {
        return _context.Properties
            .ToList();
    }
}