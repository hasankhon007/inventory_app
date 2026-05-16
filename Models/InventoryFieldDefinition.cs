using System.ComponentModel.DataAnnotations;

namespace Course.Models;

public class InventoryFieldDefinition
{
    public Guid Id { get; set; }

    public Guid InventoryId { get; set; }
    public Inventory? Inventory { get; set; }

    public InventoryFieldType FieldType { get; set; }

    [Range(1, 3)]
    public int SlotIndex { get; set; }

    [MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? Description { get; set; }

    public bool ShowInTable { get; set; }

    public int SortOrder { get; set; }
}
