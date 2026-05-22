using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Address
{
    public Guid AddressId { get; set; }

    [Display(Name = "Street Address")]
    public required string StreetAddress { get; set; }

    [Display(Name = "Suburb")]
    public required string SuburbName   { get; set; }

    [Display(Name = "City")]
    public required string CityName  { get; set; }

    [Display(Name = "Province")]
    public required string Province {get; set; }

    [Display(Name = "Longitude")]
    [Column(TypeName = "decimal(9, 6)")]
    public decimal? Longitude { get; set; }

    [Display(Name = "Latitude")]
    [Column(TypeName = "decimal(9, 6)")]
    public decimal? Latitude { get; set; }
}