namespace Course.Services.DTOs;

public class ProfileDto
{
    public List<InventoryListItemDto> OwnedInventories { get; set; } = new();
    public List<InventoryListItemDto> SharedInventories { get; set; } = new();
}
