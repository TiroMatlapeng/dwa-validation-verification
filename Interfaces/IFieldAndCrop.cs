using System.Collections.Generic;
using System.Threading.Tasks;

public interface IFieldAndCrop
{
    Task<FieldAndCrop> AddFieldAndCrop(FieldAndCrop fieldAndCrop);
    Task<FieldAndCrop> UpdateFieldAndCrop(FieldAndCrop fieldAndCrop);
    Task<ICollection<FieldAndCrop>> ListAll();
    Task<ICollection<FieldAndCrop>> GetByPropertyIdAsync(Guid propertyId);
    Task<FieldAndCrop?> GetByIdAsync(Guid id);
    Task DeleteAsync(Guid id);
}