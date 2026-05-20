using Course.Services.DTOs;

namespace Course.Services.Interfaces;

public interface IInventorySearchService
{
    Task<List<SearchResultDto>> SearchAsync(string? query, string? currentUserId);
}
