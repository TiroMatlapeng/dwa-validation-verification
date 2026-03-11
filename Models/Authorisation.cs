public class Authorisation
{
    public Guid AuthorisationId { get; set; }
    public Guid FileMasterId { get; set; }
    public FileMaster? FileMaster { get; set; }
    public Guid AuthorisationTypeId { get; set; }
    public AuthorisationType? AuthorisationType { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? Notes { get; set; }
}
