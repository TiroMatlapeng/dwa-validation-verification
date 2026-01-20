public class LetterIssuance
{
    public Guid Id { get; set; }
    public required PropertyOwner PropertyOwner { get; set; }
    public required LetterType LetterType { get; set; }
    public required DateOnly LetterDate { get; set; }
}