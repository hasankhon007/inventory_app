using Course.Services.DTOs;

namespace Course.Services.Interfaces;

public interface IInventoryTagService
{
    Task<List<InventoryTagDto>> GetTagsAsync(Guid inventoryId, string currentUserId);
    Task AddTagAsync(Guid inventoryId, string tagName, string currentUserId);
    Task UpdateTagAsync(Guid inventoryId, int tagId, string tagName, string currentUserId);
    Task RemoveTagAsync(Guid inventoryId, int tagId, string currentUserId);
}
