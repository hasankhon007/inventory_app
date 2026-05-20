namespace Course.Services.DTOs;

public class InventoryListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public int ItemsCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool CanWrite { get; set; }
}
