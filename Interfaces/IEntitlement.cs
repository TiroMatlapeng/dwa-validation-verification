using System.Collections;

public interface Entitlement 
{
    public Entitlement AddEntitlement(Entitlement entitlement);
    public Entitlement UpdateEntitlement(Entitlement entitlement);

    public ICollection<Entitlement> ListEntitlements();
}