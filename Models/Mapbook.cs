public class Mapbook
{
    public Guid MapbookId { get; set; }
    public Guid FileMasterId { get; set; }
    public FileMaster? FileMaster { get; set; }

    public required string MapbookTitle { get; set; }
    public required string MapType { get; set; }          // "Qualifying", "Current", "Overview"
    public DateOnly? ProcessedDate { get; set; }
    public string? GisLayerReference { get; set; }
    public Guid? DocumentId { get; set; }
    public Document? Document { get; set; }
    public Guid? PeriodId { get; set; }
    public Period? Period { get; set; }

    public ICollection<MapbookImage> MapbookImages { get; set; } = new List<MapbookImage>();
}
