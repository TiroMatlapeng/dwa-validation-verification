public class LetterType
{
    public Guid LetterTypeId { get; set; }
    public required string LetterName { get; set; }
    public required string LetterDescription { get; set; }
    public string? NWASection { get; set; }
}
