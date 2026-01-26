using System.Collections.Generic;
using System.Threading.Tasks;

public interface IFieldAndCrop
{
   public Task<FieldAndCrop> AddFieldAndCrop(FieldAndCrop fieldAndCrop);
   public Task<FieldAndCrop> UpdateFieldAndCrop(FieldAndCrop fieldAndCrop);
   public Task<ICollection<FieldAndCrop>> ListAll();
}