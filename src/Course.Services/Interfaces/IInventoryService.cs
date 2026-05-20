using Course.Services.DTOs;

namespace Course.Services.Interfaces;

public interface IInventoryService
{
    Task<List<InventoryListItemDto>> GetInventoryListAsync();
    Task<InventoryDetailsDto> GetDetailsAsync(Guid id);
    Task<InventoryEditDto> GetEditModelAsync(Guid id);
    Task<Guid> CreateInventoryAsync(InventoryCreateDto model, string ownerUserId);
    Task UpdateInventoryAsync(InventoryEditDto model, string currentUserId);
    Task DeleteInventoryAsync(Guid id, string currentUserId);
    Task<InventoryStatsDto> GetStatsAsync(Guid id);
    Task AddCommentAsync(Guid id, string body, string userId);
    Task UpdateIdFormatAsync(Guid id, string idFormat, string currentUserId);
    Task<AccessSnapshotDto> GetAccessSnapshotAsync(Guid id, string? currentUserId);
}
