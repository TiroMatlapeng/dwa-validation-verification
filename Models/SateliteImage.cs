public class SateliteImage
{
    public Guid ImageId { get; set; }
    public required string ImageName { get; set; }
    public required string FarmNumber { get; set; }
    public DateOnly? MapCompilation { get; set; }
    public DateOnly? ImageDate { get; set; }
    public string? ImageNumber { get; set; }

}