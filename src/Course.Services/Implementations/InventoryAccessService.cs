using Course.Domain.Entities;
using Course.Domain.Exceptions;
using Course.Services.DTOs;
using Course.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Course.DataAccess.UnitOfWork;

namespace Course.Services.Implementations;

public class InventoryAccessService : IInventoryAccessService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InventoryAccessService(IUnitOfWork unitOfWork, IHttpContextAccessor httpContextAccessor)
    {
        _unitOfWork = unitOfWork;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<List<InventoryAccessDto>> GetAccessListAsync(Guid inventoryId, string currentUserId)
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

        if (!isAdmin && !isOwner)
        {
            throw new ForbiddenAppException("Access denied.");
        }

        return await _unitOfWork.Repository<InventoryAccess>().Query()
            .AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.InventoryId == inventoryId)
            .OrderByDescending(x => x.AddedAt)
            .Select(x => new InventoryAccessDto
            {
                InventoryId = x.InventoryId,
                UserId = x.UserId,
                Username = x.User != null ? (x.User.UserName ?? string.Empty) : string.Empty,
                Email = x.User != null ? (x.User.Email ?? string.Empty) : string.Empty,
                CanWrite = x.CanWrite,
                AddedAt = x.AddedAt
            })
            .ToListAsync();
    }

    public async Task GrantAccessAsync(Guid inventoryId, string targetUserId, bool canWrite, string currentUserId)
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

        if (!isAdmin && !isOwner)
        {
            throw new ForbiddenAppException("Access denied.");
        }

        var targetUser = await _unitOfWork.Repository<ApplicationUser>().Query()
            .FirstOrDefaultAsync(x => x.Id == targetUserId);

        if (targetUser == null)
        {
            throw new NotFoundAppException("User not found.");
        }

        if (targetUser.Id == inventory.OwnerId)
        {
            throw new ValidationAppException("User is the owner of the inventory.");
        }

        var existingAccess = await _unitOfWork.Repository<InventoryAccess>().Query()
            .FirstOrDefaultAsync(x => x.InventoryId == inventoryId && x.UserId == targetUser.Id);

        if (existingAccess != null)
        {
            existingAccess.CanWrite = canWrite;
            existingAccess.AddedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            var access = new InventoryAccess
            {
                InventoryId = inventoryId,
                UserId = targetUser.Id,
                CanWrite = canWrite,
                AddedAt = DateTimeOffset.UtcNow
            };
            _unitOfWork.Repository<InventoryAccess>().Add(access);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RevokeAccessAsync(Guid inventoryId, string userId, string currentUserId)
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

        if (!isAdmin && !isOwner)
        {
            throw new ForbiddenAppException("Access denied.");
        }

        var access = await _unitOfWork.Repository<InventoryAccess>().Query()
            .FirstOrDefaultAsync(x => x.InventoryId == inventoryId && x.UserId == userId);

        if (access != null)
        {
            _unitOfWork.Repository<InventoryAccess>().RemoveRange(new[] { access });
            await _unitOfWork.SaveChangesAsync();
        }
    }

    private bool IsCurrentUserAdmin()
    {
        return _httpContextAccessor.HttpContext?.User?.IsInRole("Admin") ?? false;
    }
}
