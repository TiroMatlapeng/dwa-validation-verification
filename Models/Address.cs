using System.ComponentModel.DataAnnotations.Schema;

public class Address
{
    public Guid AddressId { get; set; } 
    public required string StreetAddress { get; set; }
    public required string SuburbName   { get; set; }
    public required string CityName  { get; set; }
    public required string Province {get; set; }
    [Column(TypeName = "decimal(9, 6)")]
    public decimal? Longitude { get; set; }
    [Column(TypeName = "decimal(9, 6)")]
    public decimal? Latitude { get; set; }
}