namespace Course.Services.DTOs;

public class InventoryCreateDto
{
    public string Title { get; set; } = string.Empty;
    public string IdFormat { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? CategoryId { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsPublic { get; set; }
    public string? SharedWithUsers { get; set; }
    public List<string> SharedUserIds { get; set; } = new();
    public bool SharedCanWrite { get; set; }
}
