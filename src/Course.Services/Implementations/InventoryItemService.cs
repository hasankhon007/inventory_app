using Course.DataAccess.UnitOfWork;
using Course.Domain.Entities;
using Course.Domain.Enums;
using Course.Domain.Exceptions;
using Course.Services.DTOs;
using Course.Services.Interfaces;
using Course.Services.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Course.Services.Implementations;

public class InventoryItemService : IInventoryItemService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InventoryItemService(IUnitOfWork unitOfWork, IHttpContextAccessor httpContextAccessor)
    {
        _unitOfWork = unitOfWork;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<List<ItemListDto>> GetItemsAsync(Guid inventoryId, string? currentUserId)
    {
        var inventory = await _unitOfWork.Repository<Inventory>().Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == inventoryId);

        if (inventory == null)
        {
            throw new NotFoundAppException("Inventory not found.");
        }

        var isAdmin = IsCurrentUserAdmin();
        var isOwner = !string.IsNullOrWhiteSpace(currentUserId) && inventory.OwnerId == currentUserId;
        var hasAccess = false;

        if (!string.IsNullOrWhiteSpace(currentUserId))
        {
            hasAccess = await _unitOfWork.Repository<InventoryAccess>().Query()
                .AnyAsync(x => x.InventoryId == inventoryId && x.UserId == currentUserId);
        }

        if (!inventory.IsPublic && !isAdmin && !isOwner && !hasAccess)
        {
            if (currentUserId == null)
            {
                throw new UnauthorizedAppException("Access denied.");
            }
            throw new ForbiddenAppException("Access denied.");
        }

        var items = await _unitOfWork.Repository<Item>().Query()
            .Include(x => x.CreatedBy)
            .Include(x => x.UpdatedBy)
            .Include(x => x.Likes)
            .Where(x => x.InventoryId == inventoryId)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync();

        var definitions = await _unitOfWork.Repository<InventoryFieldDefinition>().Query()
            .AsNoTracking()
            .Where(x => x.InventoryId == inventoryId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        var dtos = new List<ItemListDto>();
        foreach (var item in items)
        {
            var fieldsDto = new List<ItemFieldInputDto>();
            foreach (var field in definitions.Where(x => x.ShowInTable))
            {
                fieldsDto.Add(new ItemFieldInputDto
                {
                    FieldDefinitionId = field.Id,
                    FieldType = field.FieldType.ToString(),
                    SlotIndex = field.SlotIndex,
                    Title = field.Title,
                    Description = field.Description,
                    Value = GetSlotValue(item, field.FieldType, field.SlotIndex)
                });
            }

            dtos.Add(new ItemListDto
            {
                Id = item.Id,
                CustomId = item.CustomId,
                Name = item.Name,
                SequenceNumber = item.SequenceNumber,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                CreatedByName = item.CreatedBy?.UserName ?? string.Empty,
                UpdatedByName = item.UpdatedBy?.UserName,
                Fields = fieldsDto,
                LikesCount = item.Likes.Count,
                IsLikedByCurrentUser = !string.IsNullOrWhiteSpace(currentUserId) && item.Likes.Any(l => l.UserId == currentUserId),
                Price = item.NumberValue1
            });
        }

        return dtos;
    }

    public async Task<List<ItemFieldInputDto>> GetFieldInputsAsync(Guid inventoryId, Guid? itemId = null)
    {
        var definitions = await _unitOfWork.Repository<InventoryFieldDefinition>().Query()
            .AsNoTracking()
            .Where(x => x.InventoryId == inventoryId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        Item? item = null;
        if (itemId.HasValue)
        {
            item = await _unitOfWork.Repository<Item>().Query()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == itemId.Value);
        }

        var inputs = new List<ItemFieldInputDto>();
        foreach (var field in definitions)
        {
            var value = item != null ? GetSlotValue(item, field.FieldType, field.SlotIndex) : null;
            inputs.Add(new ItemFieldInputDto
            {
                FieldDefinitionId = field.Id,
                FieldType = field.FieldType.ToString(),
                SlotIndex = field.SlotIndex,
                Title = field.Title,
                Description = field.Description,
                Value = value
            });
        }
        return inputs;
    }

    public async Task CreateItemAsync(Guid inventoryId, ItemCreateDto model, string currentUserId)
    {
        var inventory = await _unitOfWork.Repository<Inventory>().Query()
            .Include(x => x.IdElements)
            .FirstOrDefaultAsync(x => x.Id == inventoryId);

        if (inventory == null)
        {
            throw new NotFoundAppException("Inventory not found.");
        }

        var isAdmin = IsCurrentUserAdmin();
        var isOwner = inventory.OwnerId == currentUserId;
        var hasWriteAccess = await _unitOfWork.Repository<InventoryAccess>().Query()
            .AnyAsync(x => x.InventoryId == inventoryId && x.UserId == currentUserId && x.CanWrite);
        var canAddItems = isAdmin || isOwner || hasWriteAccess || (inventory.IsPublic && !string.IsNullOrWhiteSpace(currentUserId));

        if (!canAddItems)
        {
            throw new ForbiddenAppException("Access denied.");
        }

        var idElements = inventory.IdElements?.OrderBy(element => element.SortOrder).ToList() ?? new List<InventoryIdElement>();
        var requiresSequence = idElements.Any(element => element.ElementType == InventoryIdElementType.Sequence);
        int? sequenceNumber = null;

        if (requiresSequence)
        {
            var lastSequence = await _unitOfWork.Repository<Item>()
                .Query()
                .AsNoTracking()
                .Where(item => item.InventoryId == inventoryId)
                .MaxAsync(item => (int?)item.SequenceNumber) ?? 0;
            sequenceNumber = lastSequence + 1;
        }

        var now = DateTimeOffset.UtcNow;
        string customId;
        if (idElements.Count == 0)
        {
            // Fallback to GUID when inventory has no ID format configured.
            customId = Guid.NewGuid().ToString("N");
        }
        else if (!InventoryIdFormatService.TryBuildCustomId(idElements, sequenceNumber, now, out var builtId, out var formatError))
        {
            throw new ValidationAppException(formatError ?? "Unable to build item ID.");
        }
        else
        {
            customId = builtId;
        }

        var item = new Item
        {
            Id = Guid.NewGuid(),
            InventoryId = inventoryId,
            CustomId = customId,
            SequenceNumber = sequenceNumber,
            Name = model.Name,
            NumberValue1 = model.Price,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedById = currentUserId ?? string.Empty,
            UpdatedById = currentUserId ?? string.Empty
        };

        // Apply fields defensively (allow null or empty)
        ApplyItemFieldInputs(item, model.Fields ?? Enumerable.Empty<Course.Services.DTOs.ItemFieldInputDto>());
        _unitOfWork.Repository<Item>().Add(item);
        inventory.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ToggleLikeAsync(Guid itemId, string userId)
    {
        var item = await _unitOfWork.Repository<Item>().Query()
            .Include(x => x.Likes)
            .FirstOrDefaultAsync(x => x.Id == itemId);

        if (item == null)
        {
            throw new NotFoundAppException("Item not found.");
        }

        var existingLike = item.Likes.FirstOrDefault(l => l.UserId == userId);
        if (existingLike != null)
        {
            _unitOfWork.Repository<ItemLike>().RemoveRange(new[] { existingLike });
        }
        else
        {
            var like = new ItemLike
            {
                ItemId = itemId,
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _unitOfWork.Repository<ItemLike>().Add(like);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private static string? GetSlotValue(Item item, InventoryFieldType type, int slotIndex)
    {
        return type switch
        {
            InventoryFieldType.SingleLineText => slotIndex switch
            {
                1 => item.TextValue1,
                2 => item.TextValue2,
                3 => item.TextValue3,
                _ => null
            },
            InventoryFieldType.MultiLineText => slotIndex switch
            {
                1 => item.MultiTextValue1,
                2 => item.MultiTextValue2,
                3 => item.MultiTextValue3,
                _ => null
            },
            InventoryFieldType.Link => slotIndex switch
            {
                1 => item.LinkValue1,
                2 => item.LinkValue2,
                3 => item.LinkValue3,
                _ => null
            },
            InventoryFieldType.Boolean => slotIndex switch
            {
                1 => item.BoolValue1?.ToString(),
                2 => item.BoolValue2?.ToString(),
                3 => item.BoolValue3?.ToString(),
                _ => null
            },
            InventoryFieldType.Number => slotIndex switch
            {
                1 => item.NumberValue1?.ToString(),
                2 => item.NumberValue2?.ToString(),
                3 => item.NumberValue3?.ToString(),
                _ => null
            },
            _ => null
        };
    }

    private static void ApplyItemFieldInputs(Item item, IEnumerable<ItemFieldInputDto> fields)
    {
        foreach (var field in fields)
        {
            if (Enum.TryParse<InventoryFieldType>(field.FieldType, out var fieldType))
            {
                switch (fieldType)
                {
                    case InventoryFieldType.SingleLineText:
                        SetTextValue(item, field.SlotIndex, field.Value);
                        break;
                    case InventoryFieldType.MultiLineText:
                        SetMultiTextValue(item, field.SlotIndex, field.Value);
                        break;
                    case InventoryFieldType.Link:
                        SetLinkValue(item, field.SlotIndex, field.Value);
                        break;
                    case InventoryFieldType.Boolean:
                        if (bool.TryParse(field.Value, out var boolVal))
                        {
                            SetBoolValue(item, field.SlotIndex, boolVal);
                        }
                        else if (field.Value == "true" || field.Value == "True" || field.Value == "1")
                        {
                            SetBoolValue(item, field.SlotIndex, true);
                        }
                        else
                        {
                            SetBoolValue(item, field.SlotIndex, false);
                        }
                        break;
                    case InventoryFieldType.Number:
                        if (decimal.TryParse(field.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var numberValue))
                        {
                            SetNumberValue(item, field.SlotIndex, numberValue);
                        }
                        break;
                }
            }
        }
    }

    private static void SetTextValue(Item item, int slotIndex, string? value)
    {
        switch (slotIndex)
        {
            case 1: item.TextValue1 = value; break;
            case 2: item.TextValue2 = value; break;
            case 3: item.TextValue3 = value; break;
        }
    }

    private static void SetMultiTextValue(Item item, int slotIndex, string? value)
    {
        switch (slotIndex)
        {
            case 1: item.MultiTextValue1 = value; break;
            case 2: item.MultiTextValue2 = value; break;
            case 3: item.MultiTextValue3 = value; break;
        }
    }

    private static void SetLinkValue(Item item, int slotIndex, string? value)
    {
        switch (slotIndex)
        {
            case 1: item.LinkValue1 = value; break;
            case 2: item.LinkValue2 = value; break;
            case 3: item.LinkValue3 = value; break;
        }
    }

    private static void SetBoolValue(Item item, int slotIndex, bool value)
    {
        switch (slotIndex)
        {
            case 1: item.BoolValue1 = value; break;
            case 2: item.BoolValue2 = value; break;
            case 3: item.BoolValue3 = value; break;
        }
    }

    private static void SetNumberValue(Item item, int slotIndex, decimal value)
    {
        switch (slotIndex)
        {
            case 1: item.NumberValue1 = value; break;
            case 2: item.NumberValue2 = value; break;
            case 3: item.NumberValue3 = value; break;
        }
    }

    private bool IsCurrentUserAdmin()
    {
        return _httpContextAccessor.HttpContext?.User?.IsInRole("Admin") ?? false;
    }
}
