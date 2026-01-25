using System.Threading.Tasks;

public interface IAddress
{
    public Task<Address> AddAddress(Address Address);
    public Task<Address> UpdateAddress(Address Address);
}