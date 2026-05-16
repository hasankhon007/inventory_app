namespace Course.Models;

public class ItemLike
{
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
