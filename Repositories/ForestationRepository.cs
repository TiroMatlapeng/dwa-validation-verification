using System.Threading.Tasks;

public class ForestationRepository : IForestation 
{
    private readonly ApplicationDBContext _dbContext;
    public ForestationRepository(ApplicationDBContext dBContext){
        _dbContext = dBContext;
    }
     public async Task<Forestation> RegisterForestation(Forestation Forestation){
       var entry = await _dbContext.Forestations.AddAsync(Forestation);
         await _dbContext.SaveChangesAsync();
        return entry.Entity;
     }
    public ICollection<Forestation> ListAll()
    {
        return _dbContext.Forestations.ToList();
    }
    //public ICollection<Forestation> ListByWaterUser(PropertyOwner propertyOwner){
//        return _dbContext.Forestations.Where( f => f.)
  //  }

    public async Task<Forestation> UpdateForestation(Forestation forestation)
    {
        var entry =  _dbContext.Forestations.Update(forestation);
        await _dbContext.SaveChangesAsync();
        return entry.Entity;
    }
}