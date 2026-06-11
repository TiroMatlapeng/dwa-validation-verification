using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.ViewModels.Admin;

/// <summary>Aggregate model for the reference-data landing page (all six lookup tables).</summary>
public class ReferenceDataIndexViewModel
{
    public List<River> Rivers { get; set; } = new();
    public List<CatchmentArea> CatchmentAreas { get; set; } = new();
    public List<IrrigationBoard> IrrigationBoards { get; set; } = new();
    public List<Crop> Crops { get; set; } = new();
    public List<WaterSource> WaterSources { get; set; } = new();
    public List<IrrigationSystem> IrrigationSystems { get; set; } = new();
}

public class RiverFormViewModel
{
    public Guid Id { get; set; }
    [Required, Display(Name = "River Name")]
    public string RiverName { get; set; } = string.Empty;
}

public class CatchmentAreaFormViewModel
{
    public Guid Id { get; set; }

    [Required, Display(Name = "Catchment Code")]
    public string CatchmentCode { get; set; } = string.Empty;

    [Required, Display(Name = "Catchment Name")]
    public string CatchmentName { get; set; } = string.Empty;

    [Required, Display(Name = "Water Management Area")]
    public Guid WmaId { get; set; }

    public IEnumerable<LookupOption> AvailableWmas { get; set; } = Array.Empty<LookupOption>();
}

public class IrrigationBoardFormViewModel
{
    public Guid Id { get; set; }

    [Required, Display(Name = "Board Name")]
    public string IrrigationBoardName { get; set; } = string.Empty;

    [Display(Name = "P-Number")]
    public string? IrrigationBoardPNumber { get; set; }

    [Display(Name = "Email Address"), EmailAddress]
    public string? EmailAddress { get; set; }
}

public class CropFormViewModel
{
    public Guid Id { get; set; }

    [Required, Display(Name = "Crop Name")]
    public string CropName { get; set; } = string.Empty;

    [Display(Name = "Crop Type")]
    public Guid? CropTypeId { get; set; }

    public IEnumerable<LookupOption> AvailableCropTypes { get; set; } = Array.Empty<LookupOption>();
}

public class WaterSourceFormViewModel
{
    public Guid Id { get; set; }

    [Required, Display(Name = "Water Source Name")]
    public string WaterSourceName { get; set; } = string.Empty;

    [Required, Display(Name = "Water Source Type")]
    public WaterSourceType WaterSourceType { get; set; }
}

public class IrrigationSystemFormViewModel
{
    public Guid Id { get; set; }

    [Required, Display(Name = "System Name")]
    public string IrrigationSystemName { get; set; } = string.Empty;

    [Display(Name = "Description")]
    public string? IrrigationSystemDescription { get; set; }

    [Display(Name = "Model")]
    public string? IrrigationSystemModel { get; set; }
}
