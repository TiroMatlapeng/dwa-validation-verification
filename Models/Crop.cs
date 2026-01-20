public class Crop
{
    public Guid CropId { get; set; }
    public required string CropName { get; set; }
    public CropType? CropType{ get; set; }
}