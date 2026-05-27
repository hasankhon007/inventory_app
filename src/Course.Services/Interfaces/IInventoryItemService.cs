using Course.Services.DTOs;

namespace Course.Services.Interfaces;

public interface IInventoryItemService
{
    Task<List<ItemListDto>> GetItemsAsync(Guid inventoryId, string? currentUserId);
    Task<ItemListDto> GetItemAsync(Guid itemId, string? currentUserId);
    Task<List<ItemFieldInputDto>> GetFieldInputsAsync(Guid inventoryId, Guid? itemId = null);
    Task CreateItemAsync(Guid inventoryId, ItemCreateDto model, string currentUserId);
    Task UpdateItemAsync(Guid inventoryId, ItemEditDto model, string currentUserId);
    Task DeleteItemAsync(Guid itemId, string currentUserId);
    Task ToggleLikeAsync(Guid itemId, string userId);
    Task<List<ItemListDto>> SearchItemsAsync(Guid inventoryId, string? query, string? currentUserId);
    Task<List<ItemListDto>> GetFilteredItemsAsync(Guid inventoryId, ItemFilterDto filter, string? currentUserId);
}
