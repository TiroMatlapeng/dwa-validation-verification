public class PropertyAddress
{
    
    public Guid Id { get; set; }
    public required string PropertyReference { get; set; }
    public required string Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? Address3 { get; set; }
    public string? Address4 { get; set; }
    public required string PostalAddress1 { get; set; }
    public string? PostalAddress2 { get; set; }
    public string? PostalAddress3 { get; set; }  
}