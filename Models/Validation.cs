public class Validation
{
    public Guid ValidationId { get; set; }
    public Guid FileMasterId { get; set; }
    public FileMaster? FileMaster { get; set; }
    public Guid? PropertyId { get; set; }
    public Property? Property { get; set; }
    public Guid? PeriodId { get; set; }
    public Period? Period { get; set; }
    public string? ValidationStatusName { get; set; }
    public DateOnly? ValidationStartDate { get; set; }
    public string? ValidationDescription { get; set; }
    public Guid? AssignedToId { get; set; }
    public ApplicationUser? AssignedTo { get; set; }
    public Guid? EntitlementId { get; set; }
    public Entitlement? Entitlement { get; set; }
}
