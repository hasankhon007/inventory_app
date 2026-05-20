using System.Security.Claims;
using Course.Domain.Entities;
using Course.Domain.Exceptions;
using Course.Services.DTOs;
using Course.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Course.Services.Implementations;

public class AdminService : IAdminService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AdminService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<List<UserListItemDto>> GetUsersListAsync()
    {
        var users = await _userManager.Users
            .OrderBy(u => u.Email)
            .ToListAsync();

        var list = new List<UserListItemDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            list.Add(new UserListItemDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                IsBlocked = user.IsBlocked,
                Roles = roles.ToList()
            });
        }

        return list;
    }

    public async Task<List<UserLookupDto>> GetUserLookupListAsync(IReadOnlyCollection<string>? excludedUserIds = null)
    {
        var excludedUserIdSet = excludedUserIds == null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : excludedUserIds
                .Where(userId => !string.IsNullOrWhiteSpace(userId))
                .Select(userId => userId.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var users = await _userManager.Users
            .OrderBy(u => u.Email)
            .ToListAsync();

        var adminUserIds = (await _userManager.GetUsersInRoleAsync("Admin"))
            .Select(user => user.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var usersLookup = new List<UserLookupDto>();

        foreach (var user in users)
        {
            if (excludedUserIdSet.Contains(user.Id))
            {
                continue;
            }

            if (adminUserIds.Contains(user.Id))
            {
                continue;
            }

            usersLookup.Add(new UserLookupDto
            {
                Id = user.Id,
                DisplayName = !string.IsNullOrWhiteSpace(user.FullName)
                    ? user.FullName
                    : (user.UserName ?? user.Email ?? string.Empty),
                Email = user.Email
            });
        }

        return usersLookup;
    }

    public async Task BlockUserAsync(string userId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == userId)
        {
            throw new ValidationAppException("You cannot block your own account.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundAppException("User not found.");
        }

        user.IsBlocked = true;
        await _userManager.UpdateSecurityStampAsync(user); // Force session invalidation
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new ValidationAppException("Failed to block user: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    public async Task UnblockUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundAppException("User not found.");
        }

        user.IsBlocked = false;
        await _userManager.UpdateSecurityStampAsync(user);
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new ValidationAppException("Failed to unblock user: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    public async Task DeleteUserAsync(string userId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == userId)
        {
            throw new ValidationAppException("You cannot delete your own account.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundAppException("User not found.");
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            throw new ValidationAppException("Failed to delete user: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    public async Task ToggleUserRoleAsync(string userId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == userId)
        {
            throw new ValidationAppException("You cannot modify your own roles.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundAppException("User not found.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var isAdmin = roles.Contains("Admin");

        if (isAdmin)
        {
            // Demote to User
            await _userManager.RemoveFromRoleAsync(user, "Admin");
            if (!await _userManager.IsInRoleAsync(user, "User"))
            {
                await _userManager.AddToRoleAsync(user, "User");
            }
        }
        else
        {
            // Promote to Admin
            await _userManager.RemoveFromRoleAsync(user, "User");
            if (!await _userManager.IsInRoleAsync(user, "Admin"))
            {
                await _userManager.AddToRoleAsync(user, "Admin");
            }
        }

        await _userManager.UpdateSecurityStampAsync(user); // Force roles to refresh
    }

    private string? GetCurrentUserId()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
