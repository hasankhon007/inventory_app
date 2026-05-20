using Course.Domain.Entities;
using Course.Services.Interfaces;
using Course.WebApp.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Course.WebApp.Controllers;

public class SearchController : Controller
{
    private readonly IInventorySearchService _searchService;
    private readonly UserManager<ApplicationUser> _userManager;

    public SearchController(IInventorySearchService searchService, UserManager<ApplicationUser> userManager)
    {
        _searchService = searchService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q)
    {
        var userId = _userManager.GetUserId(User);
        var searchResults = await _searchService.SearchAsync(q, userId);

        var model = new SearchIndexViewModel
        {
            Query = q ?? string.Empty,
            Results = searchResults.Select(r => new SearchResultRowViewModel
            {
                Type = r.Type,
                Title = r.Title,
                OwnerName = r.OwnerName,
                UpdatedAt = r.UpdatedAt,
                InventoryId = r.InventoryId,
                IsItem = r.IsItem
            }).ToList()
        };

        return View(model);
    }
}
