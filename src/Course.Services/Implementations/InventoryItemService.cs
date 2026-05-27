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
        await EnsureViewAccess(inventoryId, currentUserId);

        var items = await _unitOfWork.Repository<Item>().Query()
            .Include(x => x.CreatedBy)
            .Include(x => x.UpdatedBy)
            .Include(x => x.Likes)
            .Include(x => x.FieldValues)
            .Where(x => x.InventoryId == inventoryId)
            .OrderByDescending(x => x.UpdatedAt)
            .AsNoTracking()
            .ToListAsync();

        return items.Select(item => MapToDto(item, currentUserId)).ToList();
    }

    public async Task<ItemListDto> GetItemAsync(Guid itemId, string? currentUserId)
    {
        var item = await _unitOfWork.Repository<Item>().Query()
            .Include(x => x.CreatedBy)
            .Include(x => x.UpdatedBy)
            .Include(x => x.Likes)
            .Include(x => x.FieldValues)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == itemId);

        if (item == null)
        {
            throw new NotFoundAppException("Item not found.");
        }

        await EnsureViewAccess(item.InventoryId, currentUserId);

        return MapToDto(item, currentUserId);
    }

    public async Task<List<ItemFieldInputDto>> GetFieldInputsAsync(Guid inventoryId, Guid? itemId = null)
    {
        var customFields = await _unitOfWork.Repository<CustomField>().Query()
            .AsNoTracking()
            .Where(x => x.InventoryId == inventoryId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();

        Dictionary<Guid, string?> existingValues = new();
        if (itemId.HasValue)
        {
            existingValues = await _unitOfWork.Repository<ItemFieldValue>().Query()
                .AsNoTracking()
                .Where(x => x.ItemId == itemId.Value)
                .ToDictionaryAsync(x => x.CustomFieldId, x => x.Value);
        }

        return customFields.Select(field =>
        {
            existingValues.TryGetValue(field.Id, out var value);
            return new ItemFieldInputDto
            {
                CustomFieldId = field.Id,
                FieldType = field.FieldType.ToString(),
                Name = field.Name,
                Description = null,
                IsRequired = field.IsRequired,
                SettingsJson = field.SettingsJson,
                Value = value,
                SelectOptions = field.FieldType == InventoryFieldType.OneFromList
                    ? CustomFieldValidator.GetSelectOptions(field.SettingsJson)
                    : null
            };
        }).ToList();
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

        // Validate fixed fields
        ValidateFixedFields(model.Name, model.Description, model.Price);

        // Validate custom field values
        var customFields = await _unitOfWork.Repository<CustomField>().Query()
            .AsNoTracking()
            .Where(x => x.InventoryId == inventoryId)
            .ToListAsync();

        ValidateFieldValues(customFields, model.FieldValues);

        // Generate custom ID
        var idElements = inventory.IdElements?.OrderBy(e => e.SortOrder).ToList() ?? new List<InventoryIdElement>();
        var requiresSequence = idElements.Any(e => e.ElementType == InventoryIdElementType.Sequence);
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
            Description = model.Description,
            Price = model.Price,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedById = currentUserId,
            UpdatedById = currentUserId
        };

        _unitOfWork.Repository<Item>().Add(item);

        // Create field values
        foreach (var (fieldId, value) in model.FieldValues)
        {
            if (value == null) continue;
            _unitOfWork.Repository<ItemFieldValue>().Add(new ItemFieldValue
            {
                Id = Guid.NewGuid(),
                ItemId = item.Id,
                CustomFieldId = fieldId,
                Value = value
            });
        }

        inventory.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task UpdateItemAsync(Guid inventoryId, ItemEditDto model, string currentUserId)
    {
        var item = await _unitOfWork.Repository<Item>().Query()
            .Include(x => x.FieldValues)
            .FirstOrDefaultAsync(x => x.Id == model.Id && x.InventoryId == inventoryId);

        if (item == null)
        {
            throw new NotFoundAppException("Item not found.");
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

        // Validate fixed fields
        ValidateFixedFields(model.Name, model.Description, model.Price);

        // Validate custom field values
        var customFields = await _unitOfWork.Repository<CustomField>().Query()
            .AsNoTracking()
            .Where(x => x.InventoryId == inventoryId)
            .ToListAsync();

        ValidateFieldValues(customFields, model.FieldValues);

        // Update fixed fields
        item.Name = model.Name;
        item.Description = model.Description;
        item.Price = model.Price;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.UpdatedById = currentUserId;

        // Upsert field values
        var existingValues = item.FieldValues.ToDictionary(v => v.CustomFieldId);

        foreach (var (fieldId, value) in model.FieldValues)
        {
            if (existingValues.TryGetValue(fieldId, out var existing))
            {
                existing.Value = value;
            }
            else if (value != null)
            {
                _unitOfWork.Repository<ItemFieldValue>().Add(new ItemFieldValue
                {
                    Id = Guid.NewGuid(),
                    ItemId = item.Id,
                    CustomFieldId = fieldId,
                    Value = value
                });
            }
        }

        // Remove values for fields no longer submitted
        var removedValues = existingValues
            .Where(kv => !model.FieldValues.ContainsKey(kv.Key))
            .Select(kv => kv.Value)
            .ToList();

        if (removedValues.Count > 0)
        {
            _unitOfWork.Repository<ItemFieldValue>().RemoveRange(removedValues);
        }

        inventory.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteItemAsync(Guid itemId, string currentUserId)
    {
        var item = await _unitOfWork.Repository<Item>().Query()
            .Include(x => x.FieldValues)
            .FirstOrDefaultAsync(x => x.Id == itemId);

        if (item == null)
        {
            throw new NotFoundAppException("Item not found.");
        }

        var inventory = await _unitOfWork.Repository<Inventory>().Query()
            .FirstOrDefaultAsync(x => x.Id == item.InventoryId);

        if (inventory == null)
        {
            throw new NotFoundAppException("Inventory not found.");
        }

        var isAdmin = IsCurrentUserAdmin();
        var isOwner = inventory.OwnerId == currentUserId;
        var hasWriteAccess = await _unitOfWork.Repository<InventoryAccess>().Query()
            .AnyAsync(x => x.InventoryId == item.InventoryId && x.UserId == currentUserId && x.CanWrite);

        if (!isAdmin && !isOwner && !hasWriteAccess)
        {
            throw new ForbiddenAppException("Access denied.");
        }

        // Field values will cascade-delete via FK
        _unitOfWork.Repository<Item>().RemoveRange(new[] { item });
        inventory.UpdatedAt = DateTimeOffset.UtcNow;

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
            _unitOfWork.Repository<ItemLike>().Add(new ItemLike
            {
                ItemId = itemId,
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<List<ItemListDto>> SearchItemsAsync(Guid inventoryId, string? query, string? currentUserId)
    {
        await EnsureViewAccess(inventoryId, currentUserId);

        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return await GetItemsAsync(inventoryId, currentUserId);
        }

        var pattern = $"%{normalizedQuery}%";

        // Find item IDs matching via field values
        var itemIdsFromFieldValues = await _unitOfWork.Repository<ItemFieldValue>().Query()
            .AsNoTracking()
            .Where(fv => fv.Item != null && fv.Item.InventoryId == inventoryId
                         && fv.Value != null && EF.Functions.ILike(fv.Value, pattern))
            .Select(fv => fv.ItemId)
            .Distinct()
            .ToListAsync();

        var items = await _unitOfWork.Repository<Item>().Query()
            .Include(x => x.CreatedBy)
            .Include(x => x.UpdatedBy)
            .Include(x => x.Likes)
            .Include(x => x.FieldValues)
            .Where(x => x.InventoryId == inventoryId &&
                         (EF.Functions.ILike(x.Name, pattern)
                          || EF.Functions.ILike(x.Description, pattern)
                          || itemIdsFromFieldValues.Contains(x.Id)))
            .OrderByDescending(x => x.UpdatedAt)
            .AsNoTracking()
            .ToListAsync();

        return items.Select(item => MapToDto(item, currentUserId)).ToList();
    }

    public async Task<List<ItemListDto>> GetFilteredItemsAsync(Guid inventoryId, ItemFilterDto filter, string? currentUserId)
    {
        await EnsureViewAccess(inventoryId, currentUserId);

        var query = _unitOfWork.Repository<Item>().Query()
            .Include(x => x.CreatedBy)
            .Include(x => x.UpdatedBy)
            .Include(x => x.Likes)
            .Include(x => x.FieldValues)
            .Where(x => x.InventoryId == inventoryId);

        // Apply fixed field filters
        if (!string.IsNullOrWhiteSpace(filter.NameFilter))
        {
            var namePattern = $"%{filter.NameFilter.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Name, namePattern));
        }

        if (!string.IsNullOrWhiteSpace(filter.DescriptionFilter))
        {
            var descPattern = $"%{filter.DescriptionFilter.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Description, descPattern));
        }

        if (filter.MinPrice.HasValue)
        {
            query = query.Where(x => x.Price >= filter.MinPrice.Value);
        }

        if (filter.MaxPrice.HasValue)
        {
            query = query.Where(x => x.Price <= filter.MaxPrice.Value);
        }

        // Apply custom field filters
        foreach (var (fieldId, filterValue) in filter.FieldFilters)
        {
            if (string.IsNullOrWhiteSpace(filterValue)) continue;

            var fieldPattern = $"%{filterValue.Trim()}%";
            var capturedFieldId = fieldId;
            query = query.Where(x => x.FieldValues.Any(
                fv => fv.CustomFieldId == capturedFieldId
                      && fv.Value != null
                      && EF.Functions.ILike(fv.Value, fieldPattern)));
        }

        // Apply sorting
        query = (filter.SortBy?.ToLowerInvariant()) switch
        {
            "name" => filter.SortDirection?.ToLowerInvariant() == "asc"
                ? query.OrderBy(x => x.Name)
                : query.OrderByDescending(x => x.Name),
            "price" => filter.SortDirection?.ToLowerInvariant() == "asc"
                ? query.OrderBy(x => x.Price)
                : query.OrderByDescending(x => x.Price),
            "createdat" => filter.SortDirection?.ToLowerInvariant() == "asc"
                ? query.OrderBy(x => x.CreatedAt)
                : query.OrderByDescending(x => x.CreatedAt),
            _ => query.OrderByDescending(x => x.UpdatedAt)
        };

        var items = await query.AsNoTracking().ToListAsync();

        return items.Select(item => MapToDto(item, currentUserId)).ToList();
    }

    private static ItemListDto MapToDto(Item item, string? currentUserId)
    {
        return new ItemListDto
        {
            Id = item.Id,
            CustomId = item.CustomId,
            Name = item.Name,
            Description = item.Description,
            Price = item.Price,
            SequenceNumber = item.SequenceNumber,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            CreatedByName = item.CreatedBy?.UserName ?? string.Empty,
            UpdatedByName = item.UpdatedBy?.UserName,
            FieldValues = item.FieldValues.ToDictionary(fv => fv.CustomFieldId, fv => fv.Value),
            LikesCount = item.Likes.Count,
            IsLikedByCurrentUser = !string.IsNullOrWhiteSpace(currentUserId) && item.Likes.Any(l => l.UserId == currentUserId)
        };
    }

    private static void ValidateFixedFields(string name, string description, decimal price)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
            errors.Add("Name is required.");
        else if (name.Length > 256)
            errors.Add("Name must be at most 256 characters.");

        if (string.IsNullOrWhiteSpace(description))
            errors.Add("Description is required.");
        else if (description.Length > 2000)
            errors.Add("Description must be at most 2000 characters.");

        if (price < 0)
            errors.Add("Price must be a non-negative value.");

        if (errors.Count > 0)
        {
            throw new ValidationAppException(string.Join(" ", errors));
        }
    }

    private static void ValidateFieldValues(List<CustomField> customFields, Dictionary<Guid, string?> fieldValues)
    {
        var allErrors = new List<string>();
        var fieldMap = customFields.ToDictionary(f => f.Id);

        foreach (var field in customFields)
        {
            fieldValues.TryGetValue(field.Id, out var value);
            var errors = CustomFieldValidator.Validate(field, value);
            allErrors.AddRange(errors);
        }

        if (allErrors.Count > 0)
        {
            throw new ValidationAppException(string.Join(" ", allErrors));
        }
    }

    private async Task EnsureViewAccess(Guid inventoryId, string? currentUserId)
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
    }

    private bool IsCurrentUserAdmin()
    {
        return _httpContextAccessor.HttpContext?.User?.IsInRole("Admin") ?? false;
    }
}
