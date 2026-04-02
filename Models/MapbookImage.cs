public class MapbookImage
{
    public Guid MapbookImageId { get; set; }
    public Guid MapbookId { get; set; }
    public Mapbook? Mapbook { get; set; }
    public Guid SateliteImageId { get; set; }
    public SateliteImage? SateliteImage { get; set; }
    public int LayerOrder { get; set; }
    public string? Notes { get; set; }
}
