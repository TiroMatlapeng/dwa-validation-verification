using Microsoft.EntityFrameworkCore;

public class PropertyRepository : IPropertyInterface
{
    private readonly ApplicationDBContext _context;

    public PropertyRepository(ApplicationDBContext context) : base(context)
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

    public Property DeleteProperty(Guid PropertyId)
    {
         throw new NotImplementedException();
    }

    public Property UpdateProperty(Property Property)
    {
         throw new NotImplementedException();
    }

    public Property AddProperty(Property Property)
    {
         return new NotImplementedException();
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
//            .Where(p => p.PropertyAddress != null && p.PropertyAddress.Province == provinceName)
            .ToList();
    }
}