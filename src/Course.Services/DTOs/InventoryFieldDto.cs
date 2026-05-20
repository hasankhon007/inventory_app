namespace Course.Services.DTOs;

public class InventoryFieldDto
{
    public Guid Id { get; set; }
    public Guid InventoryId { get; set; }
    public string FieldType { get; set; } = string.Empty;
    public int SlotIndex { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool ShowInTable { get; set; }
    public int SortOrder { get; set; }
}
