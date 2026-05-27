using Course.DataAccess.UnitOfWork;
using Course.Domain.Entities;
using Course.Domain.Enums;
using Course.Domain.Exceptions;
using Course.Services.DTOs;
using Course.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Course.Services.Implementations;

public class CustomFieldService : ICustomFieldService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CustomFieldService(IUnitOfWork unitOfWork, IHttpContextAccessor httpContextAccessor)
    {
        _unitOfWork = unitOfWork;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<List<CustomFieldDto>> GetFieldsAsync(Guid inventoryId, string currentUserId)
    {
        var inventory = await _unitOfWork.Repository<Inventory>().Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == inventoryId);

        if (inventory == null)
        {
            throw new NotFoundAppException("Inventory not found.");
        }

        var isAdmin = IsCurrentUserAdmin();
        var isOwner = inventory.OwnerId == currentUserId;
        var hasAccess = await _unitOfWork.Repository<InventoryAccess>().Query()
            .AnyAsync(x => x.InventoryId == inventoryId && x.UserId == currentUserId);

        if (!inventory.IsPublic && !isAdmin && !isOwner && !hasAccess)
        {
            throw new ForbiddenAppException("Access denied.");
        }

        return await _unitOfWork.Repository<CustomField>().Query()
            .AsNoTracking()
            .Where(x => x.InventoryId == inventoryId)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new CustomFieldDto
            {
                Id = x.Id,
                InventoryId = x.InventoryId,
                Name = x.Name,
                FieldType = x.FieldType.ToString(),
                IsRequired = x.IsRequired,
                DisplayOrder = x.DisplayOrder,
                SettingsJson = x.SettingsJson,
                CreatedAt = x.CreatedAt,
                IsDeleted = x.IsDeleted
            })
            .ToListAsync();
    }

    public async Task<CustomFieldDto> AddFieldAsync(Guid inventoryId, CustomFieldCreateRequest request, string currentUserId)
    {
        var inventory = await _unitOfWork.Repository<Inventory>().Query()
            .FirstOrDefaultAsync(x => x.Id == inventoryId);

        if (inventory == null)
        {
            throw new NotFoundAppException("Inventory not found.");
        }

        EnsureWriteAccess(inventory, currentUserId);

        if (!Enum.TryParse<InventoryFieldType>(request.FieldType, out var fieldType))
        {
            throw new ValidationAppException("Invalid field type.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationAppException("Field name is required.");
        }

        var existingFields = await _unitOfWork.Repository<CustomField>().Query()
            .AsNoTracking()
            .Where(x => x.InventoryId == inventoryId)
            .ToListAsync();

        var maxOrder = existingFields.Count > 0 ? existingFields.Max(x => x.DisplayOrder) : 0;

        var field = new CustomField
        {
            Id = Guid.NewGuid(),
            InventoryId = inventoryId,
            Name = request.Name.Trim(),
            FieldType = fieldType,
            IsRequired = request.IsRequired,
            DisplayOrder = request.DisplayOrder > 0 ? request.DisplayOrder : maxOrder + 1,
            SettingsJson = request.SettingsJson,
            CreatedAt = DateTimeOffset.UtcNow,
            IsDeleted = false
        };

        _unitOfWork.Repository<CustomField>().Add(field);
        inventory.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        return new CustomFieldDto
        {
            Id = field.Id,
            InventoryId = field.InventoryId,
            Name = field.Name,
            FieldType = field.FieldType.ToString(),
            IsRequired = field.IsRequired,
            DisplayOrder = field.DisplayOrder,
            SettingsJson = field.SettingsJson,
            CreatedAt = field.CreatedAt,
            IsDeleted = field.IsDeleted
        };
    }

    public async Task UpdateFieldAsync(Guid inventoryId, CustomFieldUpdateRequest request, string currentUserId)
    {
        var field = await _unitOfWork.Repository<CustomField>().Query()
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.InventoryId == inventoryId);

        if (field == null)
        {
            throw new NotFoundAppException("Field definition not found.");
        }

        var inventory = await _unitOfWork.Repository<Inventory>().Query()
            .FirstOrDefaultAsync(x => x.Id == inventoryId);

        if (inventory == null)
        {
            throw new NotFoundAppException("Inventory not found.");
        }

        EnsureWriteAccess(inventory, currentUserId);

        if (!Enum.TryParse<InventoryFieldType>(request.FieldType, out var fieldType))
        {
            throw new ValidationAppException("Invalid field type.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationAppException("Field name is required.");
        }

        field.Name = request.Name.Trim();
        field.FieldType = fieldType;
        field.IsRequired = request.IsRequired;
        field.DisplayOrder = request.DisplayOrder;
        field.SettingsJson = request.SettingsJson;

        inventory.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteFieldAsync(Guid inventoryId, Guid fieldId, string currentUserId)
    {
        // Bypass the global query filter by accessing the context directly
        var field = await _unitOfWork.Repository<CustomField>().Query()
            .FirstOrDefaultAsync(x => x.Id == fieldId && x.InventoryId == inventoryId);

        if (field == null)
        {
            throw new NotFoundAppException("Field definition not found.");
        }

        var inventory = await _unitOfWork.Repository<Inventory>().Query()
            .FirstOrDefaultAsync(x => x.Id == inventoryId);

        if (inventory == null)
        {
            throw new NotFoundAppException("Inventory not found.");
        }

        EnsureWriteAccess(inventory, currentUserId);

        // Soft delete — mark as deleted, values remain in database
        field.IsDeleted = true;

        inventory.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ReorderFieldsAsync(Guid inventoryId, ReorderFieldsRequest request, string currentUserId)
    {
        var inventory = await _unitOfWork.Repository<Inventory>().Query()
            .FirstOrDefaultAsync(x => x.Id == inventoryId);

        if (inventory == null)
        {
            throw new NotFoundAppException("Inventory not found.");
        }

        EnsureWriteAccess(inventory, currentUserId);

        var fields = await _unitOfWork.Repository<CustomField>().Query()
            .Where(x => x.InventoryId == inventoryId)
            .ToListAsync();

        var fieldMap = fields.ToDictionary(f => f.Id);

        for (var i = 0; i < request.FieldIds.Count; i++)
        {
            if (fieldMap.TryGetValue(request.FieldIds[i], out var field))
            {
                field.DisplayOrder = i + 1;
            }
        }

        inventory.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync();
    }

    private void EnsureWriteAccess(Inventory inventory, string currentUserId)
    {
        var isAdmin = IsCurrentUserAdmin();
        var isOwner = inventory.OwnerId == currentUserId;
        var hasWriteAccess = _unitOfWork.Repository<InventoryAccess>().Query()
            .Any(x => x.InventoryId == inventory.Id && x.UserId == currentUserId && x.CanWrite);

        if (!isAdmin && !isOwner && !hasWriteAccess)
        {
            throw new ForbiddenAppException("Access denied.");
        }
    }

    private bool IsCurrentUserAdmin()
    {
        return _httpContextAccessor.HttpContext?.User?.IsInRole("Admin") ?? false;
    }
}
