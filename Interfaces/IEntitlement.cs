public interface IEntitlement
{
    Task<Entitlement> AddEntitlement(Entitlement entitlement);
    Task<Entitlement> UpdateEntitlement(Entitlement entitlement);
    Task<ICollection<Entitlement>> ListEntitlements();
}
