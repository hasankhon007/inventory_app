using Course.Services.DTOs;

namespace Course.Services.Interfaces;

public interface IInventoryItemService
{
    Task<List<ItemListDto>> GetItemsAsync(Guid inventoryId, string? currentUserId);
    Task<List<ItemFieldInputDto>> GetFieldInputsAsync(Guid inventoryId, Guid? itemId = null);
    Task CreateItemAsync(Guid inventoryId, ItemCreateDto model, string currentUserId);
    Task ToggleLikeAsync(Guid itemId, string userId);
}
