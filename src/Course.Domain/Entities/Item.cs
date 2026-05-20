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

    public int? SequenceNumber { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    [MaxLength(450)]
    public string CreatedById { get; set; } = string.Empty;
    public ApplicationUser? CreatedBy { get; set; }

    [MaxLength(450)]
    public string? UpdatedById { get; set; }
    public ApplicationUser? UpdatedBy { get; set; }

    [MaxLength(256)]
    public string? TextValue1 { get; set; }

    [MaxLength(256)]
    public string? TextValue2 { get; set; }

    [MaxLength(256)]
    public string? TextValue3 { get; set; }

    [MaxLength(2000)]
    public string? MultiTextValue1 { get; set; }

    [MaxLength(2000)]
    public string? MultiTextValue2 { get; set; }

    [MaxLength(2000)]
    public string? MultiTextValue3 { get; set; }

    public decimal? NumberValue1 { get; set; }
    public decimal? NumberValue2 { get; set; }
    public decimal? NumberValue3 { get; set; }

    [MaxLength(2048)]
    public string? LinkValue1 { get; set; }

    [MaxLength(2048)]
    public string? LinkValue2 { get; set; }

    [MaxLength(2048)]
    public string? LinkValue3 { get; set; }

    public bool? BoolValue1 { get; set; }
    public bool? BoolValue2 { get; set; }
    public bool? BoolValue3 { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<ItemLike> Likes { get; set; } = new List<ItemLike>();
}
