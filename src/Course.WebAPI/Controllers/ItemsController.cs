using System.Security.Claims;
using Course.Services.DTOs;
using Course.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Course.WebAPI.Controllers;

[ApiController]
[Route("api")]
public class ItemsController : ControllerBase
{
    private readonly IInventoryItemService _itemService;

    public ItemsController(IInventoryItemService itemService)
    {
        _itemService = itemService;
    }

    [HttpGet("inventories/{inventoryId}/items")]
    public async Task<IActionResult> GetItems(Guid inventoryId)
    {
        var userId = GetCurrentUserId();
        var items = await _itemService.GetItemsAsync(inventoryId, userId);
        return Ok(items);
    }

    [HttpGet("inventories/{inventoryId}/items/fields")]
    public async Task<IActionResult> GetFieldInputs(Guid inventoryId)
    {
        var fields = await _itemService.GetFieldInputsAsync(inventoryId);
        return Ok(fields);
    }

    [Authorize]
    [HttpPost("inventories/{inventoryId}/items")]
    public async Task<IActionResult> CreateItem(Guid inventoryId, [FromBody] ItemCreateDto model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        await _itemService.CreateItemAsync(inventoryId, model, userId);
        return StatusCode(StatusCodes.Status201Created, new { message = "Item created successfully." });
    }

    [Authorize]
    [HttpPost("items/{itemId}/like")]
    public async Task<IActionResult> ToggleLike(Guid itemId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        await _itemService.ToggleLikeAsync(itemId, userId);
        return Ok(new { message = "Like status toggled." });
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
