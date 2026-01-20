public interface IProperty : IRepository<Property>
{
    void TransferOwnership(ICollection<PropertyOwner> propertyOwners);
    Property UpdateProperty(Property);
    Property DeleteProperty(Guid PropertyId);
    ICollection<Property> ListPropertyByOwner(PropertyOwner propertyOwner);
    ICollection<Property> ListPropertyByProvince(string provinceName);
    ICollection<Property> ListAll();
}