using System;

public class Entitlement
{
    public Guid EntitlementId { get; set; }
    public required string Name { get; set;}
    public required decimal Volume { get; set;}
    public Guid EntitlementTypeId {get;set;}
    public EntitlementType EntitlementType {get; set;}
}