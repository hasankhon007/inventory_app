using Course.DataAccess.UnitOfWork;
using Course.Domain.Entities;
using Course.Domain.Exceptions;
using Course.Services.DTOs;
using Course.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Course.Services.Implementations;

public class ProfileService : IProfileService
{
    private readonly IUnitOfWork _unitOfWork;

    public ProfileService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ProfileDto> GetProfileAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAppException("Authenticated user is required.");
        }

        var ownedInventories = await _unitOfWork.Repository<Inventory>()
            .Query()
            .AsNoTracking()
            .Where(inventory => inventory.OwnerId == userId)
            .OrderByDescending(inventory => inventory.UpdatedAt)
            .Select(inventory => new InventoryListItemDto
            {
                Id = inventory.Id,
                Title = inventory.Title,
                CategoryName = inventory.Category != null ? inventory.Category.Name : null,
                OwnerName = inventory.Owner != null ? inventory.Owner.UserName ?? string.Empty : string.Empty,
                ItemsCount = inventory.Items.Count,
                UpdatedAt = inventory.UpdatedAt,
                CanWrite = true
            })
            .ToListAsync();

        var sharedInventories = await _unitOfWork.Repository<InventoryAccess>()
            .Query()
            .AsNoTracking()
            .Where(access => access.UserId == userId && access.Inventory != null && access.Inventory.OwnerId != userId)
            .OrderByDescending(access => access.Inventory != null ? access.Inventory.UpdatedAt : DateTimeOffset.MinValue)
            .Select(access => new InventoryListItemDto
            {
                Id = access.InventoryId,
                Title = access.Inventory != null ? access.Inventory.Title : string.Empty,
                CategoryName = access.Inventory != null && access.Inventory.Category != null
                    ? access.Inventory.Category.Name
                    : null,
                OwnerName = access.Inventory != null && access.Inventory.Owner != null
                    ? access.Inventory.Owner.UserName ?? string.Empty
                    : string.Empty,
                ItemsCount = access.Inventory != null ? access.Inventory.Items.Count : 0,
                UpdatedAt = access.Inventory != null ? access.Inventory.UpdatedAt : access.AddedAt,
                CanWrite = access.CanWrite
            })
            .ToListAsync();

        return new ProfileDto
        {
            OwnedInventories = ownedInventories,
            SharedInventories = sharedInventories
        };
    }
}
