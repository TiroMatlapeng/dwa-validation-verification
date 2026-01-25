using System;
using System.Threading.Tasks;

public class AddressRepository : IAddress
{
    private readonly ApplicationDBContext _dbContext;

    public AddressRepository(ApplicationDBContext dBContext){
        _dbContext = dBContext;
    } 
    public async Task<Address> AddAddress(Address Address){
      _dbContext.Addresses
                .Add(Address);
        await _dbContext.SaveChangesAsync();
        return Address;
    }
    public async Task<Address> UpdateAddress(Address AddressToUpdate){

        _dbContext.Addresses
                  .Update(AddressToUpdate);
        await _dbContext.SaveChangesAsync();          
        return AddressToUpdate;

    }
}