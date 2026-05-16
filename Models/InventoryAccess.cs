namespace Course.Models;

public class InventoryAccess
{
    public Guid InventoryId { get; set; }
    public Inventory? Inventory { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public bool CanWrite { get; set; } = true;
    public DateTimeOffset AddedAt { get; set; }
}
