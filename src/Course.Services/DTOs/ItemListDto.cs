namespace Course.Services.DTOs;

public class ItemListDto
{
    public Guid Id { get; set; }
    public string CustomId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int? SequenceNumber { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? UpdatedByName { get; set; }
    public Dictionary<Guid, string?> FieldValues { get; set; } = new();
    public int LikesCount { get; set; }
    public bool IsLikedByCurrentUser { get; set; }
}
