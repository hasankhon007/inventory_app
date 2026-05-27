using System.ComponentModel.DataAnnotations;
using Course.Domain.Enums;

namespace Course.Domain.Entities;

public class CustomField
{
    public Guid Id { get; set; }

    public Guid InventoryId { get; set; }
    public Inventory? Inventory { get; set; }

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public InventoryFieldType FieldType { get; set; }

    public bool IsRequired { get; set; }

    public int DisplayOrder { get; set; }

    [MaxLength(4000)]
    public string? SettingsJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<ItemFieldValue> FieldValues { get; set; } = new List<ItemFieldValue>();
}
