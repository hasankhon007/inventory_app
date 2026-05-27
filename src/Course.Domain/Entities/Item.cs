using System.ComponentModel.DataAnnotations;

namespace Course.Domain.Entities;

public class Item
{
    public Guid Id { get; set; }

    public Guid InventoryId { get; set; }
    public Inventory? Inventory { get; set; }

    [Required]
    [MaxLength(120)]
    public string CustomId { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int? SequenceNumber { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    [MaxLength(450)]
    public string CreatedById { get; set; } = string.Empty;
    public ApplicationUser? CreatedBy { get; set; }

    [MaxLength(450)]
    public string? UpdatedById { get; set; }
    public ApplicationUser? UpdatedBy { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<ItemLike> Likes { get; set; } = new List<ItemLike>();
    public ICollection<ItemFieldValue> FieldValues { get; set; } = new List<ItemFieldValue>();
}
