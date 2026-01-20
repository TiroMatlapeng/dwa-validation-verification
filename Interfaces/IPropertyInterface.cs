public interface IPropertyInterface : IRepository<Property>
{
    void TransferOwnership(ICollection<PropertyOwner> propertyOwners);
    ICollection<Property> ListPropertyByOwner(PropertyOwner propertyOwner);
    ICollection<Property> ListPropertyByProvince(string provinceName);
}