using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Property
{
    public Guid PropertyId { get; set; }

    [Display(Name = "Property Reference Number")]
    public string? PropertyReferenceNumber { get; set; }

    [Display(Name = "Address")]
    public Guid? AddressId { get; set; }
    public Address? Address { get; set; }

    [Display(Name = "Property Size (ha)")]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal PropertySize { get; set; }

    [Display(Name = "Proclamation Date")]
    public DateOnly? ProclamationDate { get; set; }

    [Display(Name = "Registration Date")]
    public DateOnly? RegistrationDate { get; set; }

    [Display(Name = "SG Code")]
    public string? SGCode { get; set; }

    [Display(Name = "Quaternary Drainage")]
    public string? QuaternaryDrainage { get; set; } // Legacy — use CatchmentAreaId FK instead

    [Display(Name = "Catchment Area")]
    public Guid? CatchmentAreaId { get; set; }
    public CatchmentArea? CatchmentArea { get; set; }

    [Display(Name = "Water Management Area")]
    public Guid? WmaId { get; set; }
    public WaterManagementArea? WaterManagementArea { get; set; }

    [Display(Name = "Longitude")]
    [Column(TypeName = "decimal(9, 6)")]
    public decimal? Longitude { get; set; }

    [Display(Name = "Latitude")]
    [Column(TypeName = "decimal(9, 6)")]
    public decimal? Latitude { get; set; }

    // Property status for subdivision/consolidation tracking
    [Display(Name = "Property Status")]
    public string? PropertyStatus { get; set; } // Active, Consolidated, Subdivided

    [Display(Name = "Parent Property")]
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
