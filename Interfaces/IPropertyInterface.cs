using System.Threading.Tasks;

public interface IPropertyInterface
{
    void TransferOwnership(ICollection<PropertyOwner> propertyOwners);
    public Property UpdateProperty(Property property);
    public Task<Property?> DeleteProperty(Guid PropertyId);
    public ICollection<Property> ListPropertyByOwner(PropertyOwner propertyOwner);
    public ICollection<Property> ListPropertyByProvince(string provinceName);
    public ICollection<Property> ListAll();
    public Property AddProperty(Property Property);
}