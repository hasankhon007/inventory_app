using System.ComponentModel.DataAnnotations;

namespace Course.Domain.Entities;

public class Inventory
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public int? CategoryId { get; set; }
    public InventoryCategory? Category { get; set; }

    [MaxLength(2048)]
    public string? ImageUrl { get; set; }

    public bool IsPublic { get; set; }

    [MaxLength(450)]
    public string OwnerId { get; set; } = string.Empty;
    public ApplicationUser? Owner { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<InventoryFieldDefinition> FieldDefinitions { get; set; } = new List<InventoryFieldDefinition>();
    public ICollection<InventoryIdElement> IdElements { get; set; } = new List<InventoryIdElement>();
    public ICollection<InventoryAccess> AccessList { get; set; } = new List<InventoryAccess>();
    public ICollection<InventoryTag> Tags { get; set; } = new List<InventoryTag>();
    public ICollection<Item> Items { get; set; } = new List<Item>();
    public ICollection<DiscussionPost> DiscussionPosts { get; set; } = new List<DiscussionPost>();
}
