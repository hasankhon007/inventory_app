using Course.Services.DTOs;

namespace Course.Services.Interfaces;

public interface ICustomFieldService
{
    Task<List<CustomFieldDto>> GetFieldsAsync(Guid inventoryId, string currentUserId);
    Task<CustomFieldDto> AddFieldAsync(Guid inventoryId, CustomFieldCreateRequest request, string currentUserId);
    Task UpdateFieldAsync(Guid inventoryId, CustomFieldUpdateRequest request, string currentUserId);
    Task DeleteFieldAsync(Guid inventoryId, Guid fieldId, string currentUserId);
    Task ReorderFieldsAsync(Guid inventoryId, ReorderFieldsRequest request, string currentUserId);
}
