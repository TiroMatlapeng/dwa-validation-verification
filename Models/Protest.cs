public class Protest
{
    public Guid ProtestId { get; set; }
    public Guid FileMasterId { get; set; }
    public FileMaster? FileMaster { get; set; }
    public Guid PublicUserId { get; set; }
    public PublicUser? PublicUser { get; set; }
    public DateTime LodgedDate { get; set; }
    public required string Status { get; set; } // Lodged, UnderReview, Resolved, Dismissed
    public string? ResolutionNotes { get; set; }
    public ICollection<ProtestDocument> ProtestDocuments { get; set; } = new List<ProtestDocument>();
}
