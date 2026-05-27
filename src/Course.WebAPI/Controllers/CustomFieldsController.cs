using System.Security.Claims;
using Course.Services.DTOs;
using Course.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Course.WebAPI.Controllers;

[ApiController]
[Route("api/inventories/{inventoryId}/custom-fields")]
public class CustomFieldsController : ControllerBase
{
    private readonly ICustomFieldService _customFieldService;

    public CustomFieldsController(ICustomFieldService customFieldService)
    {
        _customFieldService = customFieldService;
    }

    [HttpGet]
    public async Task<IActionResult> GetFields(Guid inventoryId)
    {
        var userId = GetCurrentUserId() ?? string.Empty;
        var fields = await _customFieldService.GetFieldsAsync(inventoryId, userId);
        return Ok(fields);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> AddField(Guid inventoryId, [FromBody] CustomFieldCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _customFieldService.AddFieldAsync(inventoryId, request, userId);
        return CreatedAtAction(nameof(GetFields), new { inventoryId }, result);
    }

    [Authorize]
    [HttpPut("{fieldId}")]
    public async Task<IActionResult> UpdateField(Guid inventoryId, Guid fieldId, [FromBody] CustomFieldUpdateRequest request)
    {
        if (fieldId != request.Id)
        {
            return BadRequest(new { error = "Route ID and body ID do not match." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await _customFieldService.UpdateFieldAsync(inventoryId, request, userId);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{fieldId}")]
    public async Task<IActionResult> DeleteField(Guid inventoryId, Guid fieldId)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await _customFieldService.DeleteFieldAsync(inventoryId, fieldId, userId);
        return NoContent();
    }

    [Authorize]
    [HttpPost("reorder")]
    public async Task<IActionResult> ReorderFields(Guid inventoryId, [FromBody] ReorderFieldsRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await _customFieldService.ReorderFieldsAsync(inventoryId, request, userId);
        return NoContent();
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
