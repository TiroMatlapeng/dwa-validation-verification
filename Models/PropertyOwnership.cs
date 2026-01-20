public class PropertyOwnership
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public Property? Property { get; set; }
    public Guid PropertyOwnerId { get; set; }
    public PropertyOwner? PropertyOwner { get; set; }
    public string? TitleDeedNumber { get; set; }
    public DateOnly? TitleDeedDate { get; set; }
}