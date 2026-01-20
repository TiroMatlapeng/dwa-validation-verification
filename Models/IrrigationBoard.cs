public class IrrigationBoard
{
  public Guid IrrigationBoardId { get; set; }
  public required string IrrigationBoardName { get; set; }
  public string? IrrigationBoardPNumber { get; set; } 
  public string? EmailAddress { get; set; }   
  public Address? IrrigationBoardAddress { get; set; }
}