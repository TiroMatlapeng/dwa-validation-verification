using System.Security.Policy;

public class Validation
{
    public Guid Id { get; set; }
    public ValidationStatus ValidationStatus { get; set;}
    public DateOnly ValidationStartDate { get; set;}
    public Property Property {get; set;}
    public Period Period {get; set;}
    public Entitlement Entitlement { get; set;}
    
}