namespace Course.Services.DTOs;

public class InventoryEditDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? CategoryId { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsPublic { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public List<string> SharedUserIds { get; set; } = new();
    public bool SharedCanWrite { get; set; }
}
