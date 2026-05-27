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

    [HttpGet("items/{itemId}")]
    public async Task<IActionResult> GetItem(Guid itemId)
    {
        var userId = GetCurrentUserId();
        var item = await _itemService.GetItemAsync(itemId, userId);
        return Ok(item);
    }

    [HttpGet("inventories/{inventoryId}/items/fields")]
    public async Task<IActionResult> GetFieldInputs(Guid inventoryId)
    {
        var fields = await _itemService.GetFieldInputsAsync(inventoryId);
        return Ok(fields);
    }

    [HttpGet("inventories/{inventoryId}/items/search")]
    public async Task<IActionResult> SearchItems(Guid inventoryId, [FromQuery] string? q)
    {
        var userId = GetCurrentUserId();
        var items = await _itemService.SearchItemsAsync(inventoryId, q, userId);
        return Ok(items);
    }

    [HttpPost("inventories/{inventoryId}/items/filter")]
    public async Task<IActionResult> FilterItems(Guid inventoryId, [FromBody] ItemFilterDto filter)
    {
        var userId = GetCurrentUserId();
        var items = await _itemService.GetFilteredItemsAsync(inventoryId, filter, userId);
        return Ok(items);
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
    [HttpPut("inventories/{inventoryId}/items/{itemId}")]
    public async Task<IActionResult> UpdateItem(Guid inventoryId, Guid itemId, [FromBody] ItemEditDto model)
    {
        if (itemId != model.Id)
        {
            return BadRequest(new { error = "Route ID and body ID do not match." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        await _itemService.UpdateItemAsync(inventoryId, model, userId);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("items/{itemId}")]
    public async Task<IActionResult> DeleteItem(Guid itemId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        await _itemService.DeleteItemAsync(itemId, userId);
        return NoContent();
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
