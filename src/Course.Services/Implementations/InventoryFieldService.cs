using Course.Domain.Abstractions;
using Course.Domain.Entities;
using Course.Domain.Enums;
using Course.Domain.Exceptions;
using Course.Services.DTOs;
using Course.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Course.Services.Implementations;

public class InventoryFieldService : IInventoryFieldService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InventoryFieldService(IUnitOfWork unitOfWork, IHttpContextAccessor httpContextAccessor)
    {
        _unitOfWork = unitOfWork;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<List<InventoryFieldDto>> GetFieldsAsync(Guid inventoryId, string currentUserId)
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

        return await _unitOfWork.Repository<InventoryFieldDefinition>().Query()
            .AsNoTracking()
            .Where(x => x.InventoryId == inventoryId)
            .OrderBy(x => x.SortOrder)
            .Select(x => new InventoryFieldDto
            {
                Id = x.Id,
                InventoryId = x.InventoryId,
                FieldType = x.FieldType.ToString(),
                SlotIndex = x.SlotIndex,
                Title = x.Title,
                Description = x.Description,
                ShowInTable = x.ShowInTable,
                SortOrder = x.SortOrder
            })
            .ToListAsync();
    }

    public async Task AddFieldAsync(Guid inventoryId, InventoryFieldDto fieldDto, string currentUserId)
    {
        var inventory = await _unitOfWork.Repository<Inventory>().Query()
            .FirstOrDefaultAsync(x => x.Id == inventoryId);

        if (inventory == null)
        {
            throw new NotFoundAppException("Inventory not found.");
        }

        var isAdmin = IsCurrentUserAdmin();
        var isOwner = inventory.OwnerId == currentUserId;
        var hasWriteAccess = await _unitOfWork.Repository<InventoryAccess>().Query()
            .AnyAsync(x => x.InventoryId == inventoryId && x.UserId == currentUserId && x.CanWrite);

        if (!isAdmin && !isOwner && !hasWriteAccess)
        {
            throw new ForbiddenAppException("Access denied.");
        }

        if (!Enum.TryParse<InventoryFieldType>(fieldDto.FieldType, out var fieldType))
        {
            throw new ValidationAppException("Invalid field type.");
        }

        if (string.IsNullOrWhiteSpace(fieldDto.Title))
        {
            throw new ValidationAppException("Field title is required.");
        }

        var existingFields = await _unitOfWork.Repository<InventoryFieldDefinition>().Query()
            .AsNoTracking()
            .Where(x => x.InventoryId == inventoryId)
            .ToListAsync();

        var typeFields = existingFields.Where(x => x.FieldType == fieldType).ToList();
        if (typeFields.Count >= 3)
        {
            throw new ValidationAppException($"Maximum 3 fields of type '{fieldType}' allowed.");
        }

        var usedSlots = typeFields.Select(x => x.SlotIndex).ToHashSet();
        var slotIndex = 1;
        for (var i = 1; i <= 3; i++)
        {
            if (!usedSlots.Contains(i))
            {
                slotIndex = i;
                break;
            }
        }

        var maxSortOrder = existingFields.Any() ? existingFields.Max(x => x.SortOrder) : 0;

        var field = new InventoryFieldDefinition
        {
            Id = Guid.NewGuid(),
            InventoryId = inventoryId,
            FieldType = fieldType,
            SlotIndex = slotIndex,
            Title = fieldDto.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(fieldDto.Description) ? null : fieldDto.Description.Trim(),
            ShowInTable = fieldDto.ShowInTable,
            SortOrder = maxSortOrder + 1
        };

        _unitOfWork.Repository<InventoryFieldDefinition>().Add(field);
        inventory.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task UpdateFieldAsync(Guid inventoryId, InventoryFieldDto fieldDto, string currentUserId)
    {
        var field = await _unitOfWork.Repository<InventoryFieldDefinition>().Query()
            .FirstOrDefaultAsync(x => x.Id == fieldDto.Id && x.InventoryId == inventoryId);

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

        var isAdmin = IsCurrentUserAdmin();
        var isOwner = inventory.OwnerId == currentUserId;
        var hasWriteAccess = await _unitOfWork.Repository<InventoryAccess>().Query()
            .AnyAsync(x => x.InventoryId == inventoryId && x.UserId == currentUserId && x.CanWrite);

        if (!isAdmin && !isOwner && !hasWriteAccess)
        {
            throw new ForbiddenAppException("Access denied.");
        }

        if (!Enum.TryParse<InventoryFieldType>(fieldDto.FieldType, out var fieldType))
        {
            throw new ValidationAppException("Invalid field type.");
        }

        if (string.IsNullOrWhiteSpace(fieldDto.Title))
        {
            throw new ValidationAppException("Field title is required.");
        }

        field.FieldType = fieldType;
        field.Title = fieldDto.Title.Trim();
        field.Description = string.IsNullOrWhiteSpace(fieldDto.Description) ? null : fieldDto.Description.Trim();
        field.ShowInTable = fieldDto.ShowInTable;

        inventory.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RemoveFieldAsync(Guid inventoryId, Guid fieldId, string currentUserId)
    {
        var field = await _unitOfWork.Repository<InventoryFieldDefinition>().Query()
            .FirstOrDefaultAsync(x => x.Id == fieldId && x.InventoryId == inventoryId);

        if (field == null)
        {
            throw new NotFoundAppException("Field definition not found.");
        }

        var inventory = await _unitOfWork.Repository<Inventory>().Query()
            .FirstOrDefaultAsync(x => x.Id == field.InventoryId);

        if (inventory == null)
        {
            throw new NotFoundAppException("Inventory not found.");
        }

        var isAdmin = IsCurrentUserAdmin();
        var isOwner = inventory.OwnerId == currentUserId;
        var hasWriteAccess = await _unitOfWork.Repository<InventoryAccess>().Query()
            .AnyAsync(x => x.InventoryId == field.InventoryId && x.UserId == currentUserId && x.CanWrite);

        if (!isAdmin && !isOwner && !hasWriteAccess)
        {
            throw new ForbiddenAppException("Access denied.");
        }

        var items = await _unitOfWork.Repository<Item>().Query()
            .Where(x => x.InventoryId == field.InventoryId)
            .ToListAsync();

        foreach (var item in items)
        {
            ClearSlotValue(item, field.FieldType, field.SlotIndex);
        }

        _unitOfWork.Repository<InventoryFieldDefinition>().RemoveRange(new[] { field });
        inventory.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync();
    }

    private static void ClearSlotValue(Item item, InventoryFieldType type, int slotIndex)
    {
        switch (type)
        {
            case InventoryFieldType.SingleLineText:
                if (slotIndex == 1) item.TextValue1 = null;
                else if (slotIndex == 2) item.TextValue2 = null;
                else if (slotIndex == 3) item.TextValue3 = null;
                break;
            case InventoryFieldType.MultiLineText:
                if (slotIndex == 1) item.MultiTextValue1 = null;
                else if (slotIndex == 2) item.MultiTextValue2 = null;
                else if (slotIndex == 3) item.MultiTextValue3 = null;
                break;
            case InventoryFieldType.Link:
                if (slotIndex == 1) item.LinkValue1 = null;
                else if (slotIndex == 2) item.LinkValue2 = null;
                else if (slotIndex == 3) item.LinkValue3 = null;
                break;
            case InventoryFieldType.Boolean:
                if (slotIndex == 1) item.BoolValue1 = null;
                else if (slotIndex == 2) item.BoolValue2 = null;
                else if (slotIndex == 3) item.BoolValue3 = null;
                break;
            case InventoryFieldType.Number:
                if (slotIndex == 1) item.NumberValue1 = null;
                else if (slotIndex == 2) item.NumberValue2 = null;
                else if (slotIndex == 3) item.NumberValue3 = null;
                break;
        }
    }

    private bool IsCurrentUserAdmin()
    {
        return _httpContextAccessor.HttpContext?.User?.IsInRole("Admin") ?? false;
    }
}
