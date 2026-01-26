public interface IForestation
{
    public Task<Forestation> RegisterForestation(Forestation forestation);
    public ICollection<Forestation> ListAll();

    public Task<Forestation> UpdateForestation(Forestation forestation);
    
}