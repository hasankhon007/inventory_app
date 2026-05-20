using Course.Domain.Abstractions;
using Course.Domain.Entities;
using Course.Domain.Exceptions;
using Course.Services.DTOs;
using Course.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Course.Services.Implementations;

public class InventoryTagService : IInventoryTagService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InventoryTagService(IUnitOfWork unitOfWork, IHttpContextAccessor httpContextAccessor)
    {
        _unitOfWork = unitOfWork;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<List<InventoryTagDto>> GetTagsAsync(Guid inventoryId, string currentUserId)
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

        return await _unitOfWork.Repository<InventoryTag>().Query()
            .AsNoTracking()
            .Include(x => x.Tag)
            .Where(x => x.InventoryId == inventoryId)
            .OrderBy(x => x.Tag != null ? x.Tag.Name : string.Empty)
            .Select(x => new InventoryTagDto
            {
                InventoryId = x.InventoryId,
                TagId = x.TagId,
                TagName = x.Tag != null ? x.Tag.Name : string.Empty
            })
            .ToListAsync();
    }

    public async Task AddTagAsync(Guid inventoryId, string tagName, string currentUserId)
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

        var cleanTagName = tagName?.Trim();
        if (string.IsNullOrWhiteSpace(cleanTagName))
        {
            throw new ValidationAppException("Tag name cannot be empty.");
        }

        if (cleanTagName.Length > 60)
        {
            throw new ValidationAppException("Tag name is too long.");
        }

        var tag = await _unitOfWork.Repository<Tag>().Query()
            .FirstOrDefaultAsync(x => x.Name.ToLower() == cleanTagName.ToLower());

        if (tag == null)
        {
            tag = new Tag { Name = cleanTagName };
            _unitOfWork.Repository<Tag>().Add(tag);
            await _unitOfWork.SaveChangesAsync();
        }

        var exists = await _unitOfWork.Repository<InventoryTag>().Query()
            .AnyAsync(x => x.InventoryId == inventoryId && x.TagId == tag.Id);

        if (!exists)
        {
            var inventoryTag = new InventoryTag
            {
                InventoryId = inventoryId,
                TagId = tag.Id
            };
            _unitOfWork.Repository<InventoryTag>().Add(inventoryTag);
            inventory.UpdatedAt = DateTimeOffset.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task UpdateTagAsync(Guid inventoryId, int tagId, string tagName, string currentUserId)
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

        var cleanTagName = tagName?.Trim();
        if (string.IsNullOrWhiteSpace(cleanTagName))
        {
            throw new ValidationAppException("Tag name cannot be empty.");
        }

        var tag = await _unitOfWork.Repository<Tag>().Query()
            .FirstOrDefaultAsync(x => x.Id == tagId);

        if (tag == null)
        {
            throw new NotFoundAppException("Tag not found.");
        }

        tag.Name = cleanTagName;
        inventory.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task RemoveTagAsync(Guid inventoryId, int tagId, string currentUserId)
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

        var association = await _unitOfWork.Repository<InventoryTag>().Query()
            .FirstOrDefaultAsync(x => x.InventoryId == inventoryId && x.TagId == tagId);

        if (association != null)
        {
            _unitOfWork.Repository<InventoryTag>().RemoveRange(new[] { association });
            inventory.UpdatedAt = DateTimeOffset.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }
    }

    private bool IsCurrentUserAdmin()
    {
        return _httpContextAccessor.HttpContext?.User?.IsInRole("Admin") ?? false;
    }
}
