using System.ComponentModel.DataAnnotations;

namespace Course.Models;

public class DiscussionPost
{
    public Guid Id { get; set; }

    public Guid InventoryId { get; set; }
    public Inventory? Inventory { get; set; }

    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required]
    [MaxLength(4000)]
    public string Body { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
