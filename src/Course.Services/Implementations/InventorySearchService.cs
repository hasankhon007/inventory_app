using Course.DataAccess.UnitOfWork;
using Course.Domain.Entities;
using Course.Services.DTOs;
using Course.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Course.Services.Implementations;

public class InventorySearchService : IInventorySearchService
{
    private readonly IUnitOfWork _unitOfWork;

    public InventorySearchService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<SearchResultDto>> SearchAsync(string? query, string? currentUserId)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return new List<SearchResultDto>();
        }

        var pattern = $"%{normalizedQuery}%";
        var accessibleInventories = _unitOfWork.Repository<Inventory>()
            .Query()
            .AsNoTracking()
            .Where(inventory => inventory.IsPublic
                || (!string.IsNullOrWhiteSpace(currentUserId) && (inventory.OwnerId == currentUserId
                    || inventory.AccessList.Any(access => access.UserId == currentUserId))));

        var inventoryResults = await accessibleInventories
            .Where(inventory => EF.Functions.ILike(inventory.Title, pattern)
                || (inventory.Description != null && EF.Functions.ILike(inventory.Description, pattern))
                || (inventory.Category != null && EF.Functions.ILike(inventory.Category.Name, pattern))
                || (inventory.Owner != null && inventory.Owner.UserName != null
                    && EF.Functions.ILike(inventory.Owner.UserName, pattern)))
            .Select(inventory => new SearchResultDto
            {
                Type = "inventory",
                Title = inventory.Title,
                OwnerName = inventory.Owner != null
                    ? (inventory.Owner.UserName ?? string.Empty)
                    : string.Empty,
                UpdatedAt = inventory.UpdatedAt,
                InventoryId = inventory.Id,
                IsItem = false
            })
            .Take(50)
            .ToListAsync();

        // Search items by Name, Description, CustomId, and custom field values
        var accessibleInventoryIds = accessibleInventories.Select(i => i.Id);

        // Find item IDs matching via field values
        var itemIdsFromFieldValues = await _unitOfWork.Repository<ItemFieldValue>()
            .Query()
            .AsNoTracking()
            .Where(fv => accessibleInventoryIds.Contains(fv.Item!.InventoryId)
                         && fv.Value != null
                         && EF.Functions.ILike(fv.Value, pattern))
            .Select(fv => fv.ItemId)
            .Distinct()
            .Take(100)
            .ToListAsync();

        var itemResults = await _unitOfWork.Repository<Item>()
            .Query()
            .AsNoTracking()
            .Where(item => accessibleInventoryIds.Contains(item.InventoryId))
            .Where(item => EF.Functions.ILike(item.CustomId, pattern)
                || EF.Functions.ILike(item.Name, pattern)
                || EF.Functions.ILike(item.Description, pattern)
                || itemIdsFromFieldValues.Contains(item.Id))
            .Select(item => new SearchResultDto
            {
                Type = "item",
                Title = string.IsNullOrWhiteSpace(item.Name)
                    ? item.CustomId
                    : $"{item.Name} ({item.CustomId})",
                OwnerName = item.Inventory != null && item.Inventory.Owner != null
                    ? (item.Inventory.Owner.UserName ?? string.Empty)
                    : string.Empty,
                UpdatedAt = item.UpdatedAt,
                InventoryId = item.InventoryId,
                IsItem = true,
                CustomId = item.CustomId
            })
            .Take(50)
            .ToListAsync();

        return inventoryResults
            .Concat(itemResults)
            .OrderByDescending(result => result.UpdatedAt)
            .Take(100)
            .ToList();
    }
}
