public class PublicUserProperty
{
    public Guid Id { get; set; }
    public Guid PublicUserId { get; set; }
    public PublicUser? PublicUser { get; set; }
    public Guid PropertyId { get; set; }
    public Property? Property { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public ApplicationUser? ApprovedByUser { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public required string Status { get; set; } // Pending, Approved, Rejected
}
