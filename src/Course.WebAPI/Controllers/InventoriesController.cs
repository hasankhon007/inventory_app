using System.Security.Claims;
using Course.Services.DTOs;
using Course.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Course.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoriesController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoriesController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetList()
    {
        var result = await _inventoryService.GetInventoryListAsync();
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDetails(Guid id)
    {
        var result = await _inventoryService.GetDetailsAsync(id);
        return Ok(result);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] InventoryCreateDto model)
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

        var inventoryId = await _inventoryService.CreateInventoryAsync(model, userId);
        return CreatedAtAction(nameof(GetDetails), new { id = inventoryId }, new { id = inventoryId });
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] InventoryEditDto model)
    {
        if (id != model.Id)
        {
            return BadRequest(new { error = "Route ID matches body ID check failed." });
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

        await _inventoryService.UpdateInventoryAsync(model, userId);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        await _inventoryService.DeleteInventoryAsync(id, userId);
        return NoContent();
    }

    [HttpGet("{id}/stats")]
    public async Task<IActionResult> GetStats(Guid id)
    {
        var result = await _inventoryService.GetStatsAsync(id);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("{id}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] CommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new { error = "Comment body is required." });
        }

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        await _inventoryService.AddCommentAsync(id, request.Body, userId);
        return Ok(new { message = "Comment added successfully." });
    }

    [Authorize]
    [HttpPost("{id}/id-format")]
    public async Task<IActionResult> UpdateIdFormat(Guid id, [FromBody] IdFormatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IdFormat))
        {
            return BadRequest(new { error = "Format is required." });
        }

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        await _inventoryService.UpdateIdFormatAsync(id, request.IdFormat, userId);
        return Ok(new { message = "ID format updated successfully." });
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}

public class CommentRequest
{
    public string Body { get; set; } = string.Empty;
}

public class IdFormatRequest
{
    public string IdFormat { get; set; } = string.Empty;
}
