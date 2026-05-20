namespace Course.Services.DTOs;

public class DiscussionPostDto
{
    public Guid Id { get; set; }
    public Guid InventoryId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserPreferredName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
