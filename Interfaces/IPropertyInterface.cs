using System.Threading.Tasks;

public interface IPropertyInterface
{
    Task<Property> AddAsync(Property property);
    Task<Property?> GetByIdAsync(Guid id);
    Task<ICollection<Property>> ListAllAsync();
    Task<ICollection<Property>> ListByProvinceAsync(string provinceName);
    Task<Property> UpdateAsync(Property property);
    Task<Property?> DeleteAsync(Guid id);
}
