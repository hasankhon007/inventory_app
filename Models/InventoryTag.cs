namespace Course.Models;

public class InventoryTag
{
    public Guid InventoryId { get; set; }
    public Inventory? Inventory { get; set; }

    public int TagId { get; set; }
    public Tag? Tag { get; set; }
}
