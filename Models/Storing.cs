using System.ComponentModel.DataAnnotations.Schema;

public class Storing
{
    public Guid StoringId { get; set; }
    public required Property Property { get; set; }
    public Guid PropertyId  { get; set; }
    public int NumberOfDams { get; set; }
    [Column(TypeName = "decimal(18, 2)")]
    public decimal Volume { get; set; }
    public River? RiverOrStream { get; set; }
    public VerifactionScenario VerifactionScenario { get; set; }
    public required Period Period { get; set; }

}