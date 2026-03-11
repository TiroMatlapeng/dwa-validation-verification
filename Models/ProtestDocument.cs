public class ProtestDocument
{
    public Guid Id { get; set; }
    public Guid ProtestId { get; set; }
    public Protest? Protest { get; set; }
    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }
}
