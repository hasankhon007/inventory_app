namespace Course.Services.DTOs;

public class SearchResultDto
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid InventoryId { get; set; }
    public bool IsItem { get; set; }
    public string? CustomId { get; set; }
}
