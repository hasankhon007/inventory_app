using System.ComponentModel.DataAnnotations;

namespace Course.Domain.Entities;

public class ItemLike
{
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
