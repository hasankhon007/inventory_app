using Course.Domain.Entities;

namespace Course.Services.DTOs;

public class InventoryCreateValidationResultDto
{
    public List<InventoryIdElement> ParsedIdElements { get; set; } = new();
    public List<ApplicationUser> SharedUsers { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool IsValid => Errors.Count == 0;
}
