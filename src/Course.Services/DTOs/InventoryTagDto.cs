namespace Course.Services.DTOs;

public class InventoryTagDto
{
    public Guid InventoryId { get; set; }
    public int TagId { get; set; }
    public string TagName { get; set; } = string.Empty;
}
