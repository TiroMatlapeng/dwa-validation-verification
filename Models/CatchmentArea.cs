public class CatchmentArea
{
    public Guid CatchmentAreaId { get; set; }
    public required string CatchmentCode { get; set; }   // Quaternary code, e.g. "A21A", "B31A"
    public required string CatchmentName { get; set; }
    public Guid WmaId { get; set; }
    public WaterManagementArea? WaterManagementArea { get; set; }

    public ICollection<Property> Properties { get; set; } = new List<Property>();
    public ICollection<FileMaster> FileMasters { get; set; } = new List<FileMaster>();
}
