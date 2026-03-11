public class SateliteImage
{
    public Guid ImageId { get; set; }
    public required string ImageName { get; set; }
    public string? FarmNumber { get; set; }
    public Guid? PropertyId { get; set; }
    public Property? Property { get; set; }
    public Guid? PeriodId { get; set; }
    public Period? Period { get; set; }
    public DateOnly? MapCompilation { get; set; }
    public DateOnly? ImageDate { get; set; }
    public string? ImageNumber { get; set; }
    public string? ImageSource { get; set; }
}
