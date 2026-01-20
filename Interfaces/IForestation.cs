public interface IForestation
{
    IForestation RegisterForestation(Forestation forestation);
    ICollection<Forestation> ListAll();
    ICollection<Forestation> ListByWaterUser(PropertyOwner propertyOwner);

    Forestation UpdateForestation(Forestation forestation);
    
}