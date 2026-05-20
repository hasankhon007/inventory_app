using Course.Services.DTOs;

namespace Course.Services.Interfaces;

public interface IInventoryAccessService
{
    Task<List<InventoryAccessDto>> GetAccessListAsync(Guid inventoryId, string currentUserId);
    Task GrantAccessAsync(Guid inventoryId, string targetUserId, bool canWrite, string currentUserId);
    Task RevokeAccessAsync(Guid inventoryId, string userId, string currentUserId);
}
