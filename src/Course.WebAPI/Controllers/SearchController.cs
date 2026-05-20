using System.Security.Claims;
using Course.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Course.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly IInventorySearchService _searchService;

    public SearchController(IInventorySearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { error = "Search query is required." });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var results = await _searchService.SearchAsync(q, userId);
        return Ok(results);
    }
}
