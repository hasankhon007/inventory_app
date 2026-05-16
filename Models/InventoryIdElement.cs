using System.ComponentModel.DataAnnotations;

namespace Course.Models;

public class InventoryIdElement
{
    public Guid Id { get; set; }

    public Guid InventoryId { get; set; }
    public Inventory? Inventory { get; set; }

    public InventoryIdElementType ElementType { get; set; }

    [MaxLength(200)]
    public string? FixedText { get; set; }

    [MaxLength(100)]
    public string? Format { get; set; }

    public int SortOrder { get; set; }
}
