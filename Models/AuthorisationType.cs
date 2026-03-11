public class AuthorisationType
{
    public Guid AuthorisationTypeId { get; set; }
    public required string AuthorisationTypeName { get; set; }
    public string? Description { get; set; }
}
