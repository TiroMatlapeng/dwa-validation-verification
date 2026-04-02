using System.ComponentModel.DataAnnotations.Schema;

public class Property
{
    public Guid PropertyId { get; set; }
    public string? PropertyReferenceNumber { get; set; }
    public Guid? AddressId { get; set; }
    public Address? Address { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal PropertySize { get; set; }
    public DateOnly? ProclamationDate { get; set; }
    public DateOnly? RegistrationDate { get; set; }
    public string? SGCode { get; set; }
    public string? QuaternaryDrainage { get; set; } // Legacy — use CatchmentAreaId FK instead
    public Guid? CatchmentAreaId { get; set; }
    public CatchmentArea? CatchmentArea { get; set; }
    public Guid? WmaId { get; set; }
    public WaterManagementArea? WaterManagementArea { get; set; }
    [Column(TypeName = "decimal(9, 6)")]
    public decimal? Longitude { get; set; }
    [Column(TypeName = "decimal(9, 6)")]
    public decimal? Latitude { get; set; }

    // Property status for subdivision/consolidation tracking
    public string? PropertyStatus { get; set; } // Active, Consolidated, Subdivided
    public Guid? ParentPropertyId { get; set; }
    public Property? ParentProperty { get; set; }
    public ICollection<Property> ChildProperties { get; set; } = new List<Property>();

    public ICollection<PropertyOwnership> PropertyOwnerships { get; set; } = new List<PropertyOwnership>();
    public ICollection<FileMaster> FileMasters { get; set; } = new List<FileMaster>();
    public ICollection<Irrigation> Irrigations { get; set; } = new List<Irrigation>();
    public ICollection<Storing> Storings { get; set; } = new List<Storing>();
    public ICollection<Forestation> Forestations { get; set; } = new List<Forestation>();
    public ICollection<FieldAndCrop> FieldAndCrops { get; set; } = new List<FieldAndCrop>();
    public ICollection<DamCalculation> DamCalculations { get; set; } = new List<DamCalculation>();
    public ICollection<SateliteImage> SateliteImages { get; set; } = new List<SateliteImage>();
    public ICollection<Mapbook> Mapbooks { get; set; } = new List<Mapbook>();
}
