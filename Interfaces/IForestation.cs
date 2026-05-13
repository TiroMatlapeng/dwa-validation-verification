public interface IForestation
{
    Task<Forestation> RegisterForestation(Forestation forestation);
    Task<ICollection<Forestation>> ListAll();
    Task<ICollection<Forestation>> GetByPropertyIdAsync(Guid propertyId);
    Task<Forestation?> GetByIdAsync(Guid id);
    Task<Forestation> UpdateForestation(Forestation forestation);
    Task DeleteAsync(Guid id);
}