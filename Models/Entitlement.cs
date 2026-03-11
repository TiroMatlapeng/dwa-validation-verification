using System;
using System.ComponentModel.DataAnnotations.Schema;

public class Entitlement
{
    public Guid EntitlementId { get; set; }
    public required string Name { get; set;}
    [Column(TypeName = "decimal(18, 2)")]
    public required decimal Volume { get; set;}
    public Guid EntitlementTypeId {get;set;}
    public EntitlementType EntitlementType {get; set;}
}