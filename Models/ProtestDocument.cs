public class ObjectionDocument
{
    public Guid Id { get; set; }
    public Guid ObjectionId { get; set; }
    public Objection? Objection { get; set; }
    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }
}
