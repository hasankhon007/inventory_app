using Course.Services.DTOs;

namespace Course.Services.Interfaces;

public interface IAdminService
{
    Task<List<UserListItemDto>> GetUsersListAsync();
    Task<List<UserLookupDto>> GetUserLookupListAsync(IReadOnlyCollection<string>? excludedUserIds = null);
    Task BlockUserAsync(string userId);
    Task UnblockUserAsync(string userId);
    Task DeleteUserAsync(string userId);
    Task ToggleUserRoleAsync(string userId);
}
