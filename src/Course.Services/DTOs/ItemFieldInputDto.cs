namespace Course.Services.DTOs;

public class ItemFieldInputDto
{
    public Guid FieldDefinitionId { get; set; }
    public string FieldType { get; set; } = string.Empty;
    public int SlotIndex { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Value { get; set; }
}
