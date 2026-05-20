using Course.Services.DTOs;

namespace Course.Services.Interfaces;

public interface IInventoryFieldService
{
    Task<List<InventoryFieldDto>> GetFieldsAsync(Guid inventoryId, string currentUserId);
    Task AddFieldAsync(Guid inventoryId, InventoryFieldDto fieldDto, string currentUserId);
    Task UpdateFieldAsync(Guid inventoryId, InventoryFieldDto fieldDto, string currentUserId);
    Task RemoveFieldAsync(Guid inventoryId, Guid fieldId, string currentUserId);
}
