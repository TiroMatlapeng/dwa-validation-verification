using System.ComponentModel.DataAnnotations.Schema;

public class Property
{   
    public Guid PropertyId { get; set; }
    public string? PropertyNumber { get; set; }
    public Address? PropertyAddress { get; set;}  
    public int PropertySize { get; set;}
    public DateOnly ProclamationDate { get; set; }
    public DateOnly RegistrationDate { get; set; }
    public string? SGCode { get; set; }
    public string? QuatenaryDrainage { get; set; }
    public string? WaterManagementArea { get; set; }
    [Column(TypeName = "decimal(9, 6)")]
    public decimal? Longitude   { get; set; }
    [Column(TypeName = "decimal(9, 6)")]
    public decimal? Latitude { get; set; }
    
    public ICollection<PropertyOwnership> PropertyOwnerships { get; set; } = new List<PropertyOwnership>();
}