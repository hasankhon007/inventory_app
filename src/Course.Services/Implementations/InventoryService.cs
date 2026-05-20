using Course.Domain.Abstractions;
using Course.Domain.Entities;
using Course.Domain.Exceptions;
using Course.Services.DTOs;
using Course.Services.Interfaces;
using Course.Services.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Course.Services.Implementations;

public class InventoryService : IInventoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IInventoryCreateValidator _validator;

    public InventoryService(
        IUnitOfWork unitOfWork,
        IHttpContextAccessor httpContextAccessor,
        IInventoryCreateValidator validator)
    {
        _unitOfWork = unitOfWork;
        _httpContextAccessor = httpContextAccessor;
        _validator = validator;
    }

    public async Task<List<InventoryListItemDto>> GetInventoryListAsync()
    {
        var currentUserId = GetCurrentUserId();
        var isAdmin = IsCurrentUserAdmin();

        var query = _unitOfWork.Repository<Inventory>().Query();

        if (!isAdmin)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                query = query.Where(inventory => inventory.IsPublic);
            }
            else
            {
                query = query.Where(inventory => inventory.IsPublic 
                    || inventory.OwnerId == currentUserId 
                    || inventory.AccessList.Any(access => access.UserId == currentUserId));
            }
        }

        return await query
            .AsNoTracking()
            .OrderByDescending(inventory => inventory.UpdatedAt)
            .Select(inventory => new InventoryListItemDto
            {
                Id = inventory.Id,
                Title = inventory.Title,
                CategoryName = inventory.Category != null ? inventory.Category.Name : null,
                OwnerName = inventory.Owner != null ? (inventory.Owner.UserName ?? string.Empty) : string.Empty,
                ItemsCount = inventory.Items.Count,
                UpdatedAt = inventory.UpdatedAt,
                CanWrite = false
            })
            .ToListAsync();
    }

    public async Task<InventoryDetailsDto> GetDetailsAsync(Guid id)
    {
        var inventory = await _unitOfWork.Repository<Inventory>()
            .Query()
            .AsNoTracking()
            .Include(current => current.Category)
            .Include(current => current.Owner)
            .Include(current => current.IdElements)
            .FirstOrDefaultAsync(current => current.Id == id);

        if (inventory == null)
        {
            throw new NotFoundAppException("Inventory not found.");
        }

        var currentUserId = GetCurrentUserId();
        var access = await GetAccessSnapshotAsync(id, currentUserId);
        if (!access.CanView)
        {
            if (currentUserId == null)
            {
                throw new UnauthorizedAppException("Access denied.");
            }
            throw new ForbiddenAppException("Access denied.");
        }

        var comments = await _unitOfWork.Repository<DiscussionPost>()
            .Query()
            .AsNoTracking()
            .Where(post => post.InventoryId == id)
            .OrderByDescending(post => post.CreatedAt)
            .Select(post => new DiscussionPostDto
            {
                Id = post.Id,
                InventoryId = post.InventoryId,
                UserId = post.UserId,
                UserPreferredName = post.User != null ? (post.User.UserName ?? string.Empty) : string.Empty,
                Body = post.Body,
                CreatedAt = post.CreatedAt
            })
            .ToListAsync();

        return new InventoryDetailsDto
        {
            Id = inventory.Id,
            Title = inventory.Title,
            Description = inventory.Description,
            CategoryId = inventory.CategoryId,
            CategoryName = inventory.Category?.Name,
            ImageUrl = inventory.ImageUrl,
            IsPublic = inventory.IsPublic,
            OwnerId = inventory.OwnerId,
            OwnerName = inventory.Owner != null ? (inventory.Owner.UserName ?? string.Empty) : string.Empty,
            CreatedAt = inventory.CreatedAt,
            UpdatedAt = inventory.UpdatedAt,
            IdFormat = InventoryIdFormatService.ToFormatString(inventory.IdElements),
            DiscussionPosts = comments
        };
    }

    public async Task<InventoryEditDto> GetEditModelAsync(Guid id)
    {
        var inventory = await _unitOfWork.Repository<Inventory>()
            .Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(current => current.Id == id);

        if (inventory == null)
        {
            throw new NotFoundAppException("Inventory not found.");
        }

        var currentUserId = GetCurrentUserId();
        var access = await GetAccessSnapshotAsync(id, currentUserId);
        if (!access.CanWrite)
        {
            throw new ForbiddenAppException("Access denied.");
        }

        return new InventoryEditDto
        {
            Id = inventory.Id,
            Title = inventory.Title,
            Description = inventory.Description,
            CategoryId = inventory.CategoryId,
            ImageUrl = inventory.ImageUrl,
            IsPublic = inventory.IsPublic,
            OwnerId = inventory.OwnerId
        };
    }

    public async Task<Guid> CreateInventoryAsync(InventoryCreateDto model, string ownerUserId)
    {
        var validationResult = await _validator.ValidateAsync(model);
        if (!validationResult.IsValid)
        {
            throw new ValidationAppException(string.Join(" ", validationResult.Errors));
        }

        var now = DateTimeOffset.UtcNow;
        var inventory = new Inventory
        {
            Id = Guid.NewGuid(),
            Title = model.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            CategoryId = model.CategoryId,
            ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl) ? null : model.ImageUrl.Trim(),
            IsPublic = model.IsPublic,
            OwnerId = ownerUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _unitOfWork.Repository<Inventory>().Add(inventory);

        foreach (var element in validationResult.ParsedIdElements)
        {
            element.Id = Guid.NewGuid();
            element.InventoryId = inventory.Id;
        }

        _unitOfWork.Repository<InventoryIdElement>().AddRange(validationResult.ParsedIdElements);
        _unitOfWork.Repository<InventoryAccess>().Add(new InventoryAccess
        {
            InventoryId = inventory.Id,
            UserId = ownerUserId,
            CanWrite = true,
            AddedAt = now
        });

        if (!model.IsPublic && validationResult.SharedUsers.Count > 0)
        {
            foreach (var user in validationResult.SharedUsers.Where(user => user.Id != ownerUserId).DistinctBy(user => user.Id))
            {
                _unitOfWork.Repository<InventoryAccess>().Add(new InventoryAccess
                {
                    InventoryId = inventory.Id,
                    UserId = user.Id,
                    CanWrite = model.SharedCanWrite,
                    AddedAt = now
                });
            }
        }

        await _unitOfWork.SaveChangesAsync();
        return inventory.Id;
    }

    public async Task UpdateInventoryAsync(InventoryEditDto model, string currentUserId)
    {
        var inventory = await _unitOfWork.Repository<Inventory>()
            .Query()
            .Include(current => current.IdElements)
            .FirstOrDefaultAsync(current => current.Id == model.Id);

        if (inventory == null)
        {
            throw new NotFoundAppException("Inventory not found.");
        }

        var access = await GetAccessSnapshotAsync(model.Id, currentUserId);
        if (!access.CanWrite)
        {
            throw new ForbiddenAppException("Access denied.");
        }

        var normalizedSharedUserIds = model.SharedUserIds
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .Select(userId => userId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<ApplicationUser> sharedUsers = new();
        if (!model.IsPublic)
        {
            if (normalizedSharedUserIds.Count > 0)
            {
                sharedUsers = await _unitOfWork.Repository<ApplicationUser>()
                    .Query()
                    .Where(user => normalizedSharedUserIds.Contains(user.Id))
                    .ToListAsync();

                var missingUserIds = normalizedSharedUserIds
                    .Where(userId => !sharedUsers.Any(user => user.Id == userId))
                    .ToList();

                if (missingUserIds.Count > 0)
                {
                    throw new ValidationAppException($"These users were not found: {string.Join(", ", missingUserIds)}.");
                }
            }

            await SyncSharedAccessAsync(inventory.Id, inventory.OwnerId, sharedUsers, model.SharedCanWrite);
        }

        inventory.Title = model.Title.Trim();
        inventory.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        inventory.CategoryId = model.CategoryId;
        inventory.ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl) ? inventory.ImageUrl : model.ImageUrl.Trim();
        inventory.IsPublic = model.IsPublic;
        inventory.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteInventoryAsync(Guid id, string currentUserId)
    {
        var inventory = await _unitOfWork.Repository<Inventory>()
            .Query()
            .FirstOrDefaultAsync(current => current.Id == id);

        if (inventory == null)
        {
            throw new NotFoundAppException("Inventory not found.");
        }

        var access = await GetAccessSnapshotAsync(id, currentUserId);
        if (!access.IsOwner && !access.IsOwner) // only owner (or admin) can delete
        {
            throw new ForbiddenAppException("Access denied.");
        }

        // Clean related elements
        var idElements = await _unitOfWork.Repository<InventoryIdElement>().Query().Where(x => x.InventoryId == id).ToListAsync();
        _unitOfWork.Repository<InventoryIdElement>().RemoveRange(idElements);

        var accesses = await _unitOfWork.Repository<InventoryAccess>().Query().Where(x => x.InventoryId == id).ToListAsync();
        _unitOfWork.Repository<InventoryAccess>().RemoveRange(accesses);

        var items = await _unitOfWork.Repository<Item>().Query().Where(x => x.InventoryId == id).ToListAsync();
        _unitOfWork.Repository<Item>().RemoveRange(items);

        var discussionPosts = await _unitOfWork.Repository<DiscussionPost>().Query().Where(x => x.InventoryId == id).ToListAsync();
        _unitOfWork.Repository<DiscussionPost>().RemoveRange(discussionPosts);

        var tags = await _unitOfWork.Repository<InventoryTag>().Query().Where(x => x.InventoryId == id).ToListAsync();
        _unitOfWork.Repository<InventoryTag>().RemoveRange(tags);

        _unitOfWork.Repository<Inventory>().RemoveRange(new[] { inventory });
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<InventoryStatsDto> GetStatsAsync(Guid id)
    {
        var inventory = await _unitOfWork.Repository<Inventory>()
            .Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(current => current.Id == id);

        if (inventory == null)
        {
            throw new NotFoundAppException("Inventory not found.");
        }

        var currentUserId = GetCurrentUserId();
        var access = await GetAccessSnapshotAsync(id, currentUserId);
        if (!access.CanView)
        {
            throw new ForbiddenAppException("Access denied.");
        }

        var stats = await _unitOfWork.Repository<Item>()
            .Query()
            .AsNoTracking()
            .Where(item => item.InventoryId == id)
            .GroupBy(_ => 1)
            .Select(group => new InventoryStatsDto
            {
                ItemsCount = group.Count(),
                AvgPrice = group.Average(item => item.NumberValue1),
                MinPrice = group.Min(item => item.NumberValue1),
                MaxPrice = group.Max(item => item.NumberValue1),
                LastItemUpdatedAt = group.Max(item => item.UpdatedAt)
            })
            .FirstOrDefaultAsync();

        return stats ?? new InventoryStatsDto { ItemsCount = 0 };
    }

    public async Task AddCommentAsync(Guid id, string body, string userId)
    {
        var inventory = await _unitOfWork.Repository<Inventory>()
            .Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(current => current.Id == id);

        if (inventory == null)
        {
            throw new NotFoundAppException("Inventory not found.");
        }

        var access = await GetAccessSnapshotAsync(id, userId);
        var canComment = access.IsOwner || access.CanWrite || access.CanView; // any access can comment or owner/admin
        if (!canComment)
        {
            throw new ForbiddenAppException("Access denied.");
        }

        var trimmedBody = body?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedBody))
        {
            throw new ValidationAppException("Comment cannot be empty.");
        }

        if (trimmedBody.Length > 4000)
        {
            throw new ValidationAppException("Comment is too long.");
        }

        var post = new DiscussionPost
        {
            Id = Guid.NewGuid(),
            InventoryId = id,
            UserId = userId,
            Body = trimmedBody,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _unitOfWork.Repository<DiscussionPost>().Add(post);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task UpdateIdFormatAsync(Guid id, string idFormat, string currentUserId)
    {
        var inventory = await _unitOfWork.Repository<Inventory>()
            .Query()
            .Include(current => current.IdElements)
            .FirstOrDefaultAsync(current => current.Id == id);

        if (inventory == null)
        {
            throw new NotFoundAppException("Inventory not found.");
        }

        var access = await GetAccessSnapshotAsync(id, currentUserId);
        if (!access.CanWrite)
        {
            throw new ForbiddenAppException("Access denied.");
        }

        if (!InventoryIdFormatService.TryParse(idFormat, out var idElements, out var formatError))
        {
            throw new ValidationAppException(formatError ?? "Invalid ID format.");
        }

        _unitOfWork.Repository<InventoryIdElement>().RemoveRange(inventory.IdElements);
        foreach (var element in idElements)
        {
            element.Id = Guid.NewGuid();
            element.InventoryId = inventory.Id;
        }

        _unitOfWork.Repository<InventoryIdElement>().AddRange(idElements);
        inventory.UpdatedAt = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<AccessSnapshotDto> GetAccessSnapshotAsync(Guid id, string? currentUserId)
    {
        var inventory = await _unitOfWork.Repository<Inventory>()
            .Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (inventory == null)
        {
            return new AccessSnapshotDto();
        }

        var isAdmin = IsCurrentUserAdmin();
        var isOwner = !string.IsNullOrWhiteSpace(currentUserId) && inventory.OwnerId == currentUserId;
        var hasAccess = false;
        var hasWriteAccess = false;

        if (!string.IsNullOrWhiteSpace(currentUserId))
        {
            var access = await _unitOfWork.Repository<InventoryAccess>()
                .Query()
                .AsNoTracking()
                .Where(entry => entry.InventoryId == id && entry.UserId == currentUserId)
                .Select(entry => new { entry.CanWrite })
                .FirstOrDefaultAsync();

            if (access != null)
            {
                hasAccess = true;
                hasWriteAccess = access.CanWrite;
            }
        }

        var canView = inventory.IsPublic || isAdmin || isOwner || hasAccess;
        var canEdit = isAdmin || isOwner || hasWriteAccess || inventory.IsPublic;
        var canAddItems = canEdit || inventory.IsPublic;

        return new AccessSnapshotDto
        {
            IsOwner = isOwner,
            CanView = canView,
            CanWrite = canEdit, // CanWrite maps to canEdit in original code
            CanManage = isOwner || isAdmin,
            CanAddItems = canAddItems
        };
    }

    private string? GetCurrentUserId()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private bool IsCurrentUserAdmin()
    {
        return _httpContextAccessor.HttpContext?.User?.IsInRole("Admin") ?? false;
    }

    private async Task SyncSharedAccessAsync(Guid inventoryId, string ownerUserId, IReadOnlyCollection<ApplicationUser> sharedUsers, bool canWrite)
    {
        var normalizedSharedUserIds = sharedUsers
            .Where(user => !string.Equals(user.Id, ownerUserId, StringComparison.OrdinalIgnoreCase))
            .Select(user => user.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingAccessEntries = await _unitOfWork.Repository<InventoryAccess>()
            .Query()
            .Where(entry => entry.InventoryId == inventoryId)
            .ToListAsync();

        var existingByUserId = existingAccessEntries.ToDictionary(entry => entry.UserId, StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;

        foreach (var accessEntry in existingAccessEntries.Where(entry => entry.UserId != ownerUserId && !normalizedSharedUserIds.Contains(entry.UserId)).ToList())
        {
            _unitOfWork.Repository<InventoryAccess>().RemoveRange(new[] { accessEntry });
        }

        if (existingByUserId.TryGetValue(ownerUserId, out var ownerAccess))
        {
            ownerAccess.CanWrite = true;
            ownerAccess.AddedAt = now;
        }
        else
        {
            _unitOfWork.Repository<InventoryAccess>().Add(new InventoryAccess
            {
                InventoryId = inventoryId,
                UserId = ownerUserId,
                CanWrite = true,
                AddedAt = now
            });
        }

        foreach (var user in sharedUsers.Where(user => user.Id != ownerUserId).DistinctBy(user => user.Id))
        {
            if (existingByUserId.TryGetValue(user.Id, out var existingAccess))
            {
                existingAccess.CanWrite = canWrite;
                existingAccess.AddedAt = now;
                continue;
            }

            _unitOfWork.Repository<InventoryAccess>().Add(new InventoryAccess
            {
                InventoryId = inventoryId,
                UserId = user.Id,
                CanWrite = canWrite,
                AddedAt = now
            });
        }
    }
}
