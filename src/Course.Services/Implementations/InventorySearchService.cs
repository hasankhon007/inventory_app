using Course.Domain.Abstractions;
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

        var itemResults = await _unitOfWork.Repository<Item>()
            .Query()
            .AsNoTracking()
            .Where(item => accessibleInventories.Select(inventory => inventory.Id).Contains(item.InventoryId))
            .Where(item => EF.Functions.ILike(item.CustomId, pattern)
                || EF.Functions.ILike(item.Name, pattern)
                || (item.TextValue1 != null && EF.Functions.ILike(item.TextValue1, pattern))
                || (item.TextValue2 != null && EF.Functions.ILike(item.TextValue2, pattern))
                || (item.TextValue3 != null && EF.Functions.ILike(item.TextValue3, pattern))
                || (item.MultiTextValue1 != null && EF.Functions.ILike(item.MultiTextValue1, pattern))
                || (item.MultiTextValue2 != null && EF.Functions.ILike(item.MultiTextValue2, pattern))
                || (item.MultiTextValue3 != null && EF.Functions.ILike(item.MultiTextValue3, pattern))
                || (item.Inventory != null && EF.Functions.ILike(item.Inventory.Title, pattern)))
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
