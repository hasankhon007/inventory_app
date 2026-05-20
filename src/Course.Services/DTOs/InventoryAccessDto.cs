namespace Course.Services.DTOs;

public class InventoryAccessDto
{
    public Guid InventoryId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool CanWrite { get; set; }
    public DateTimeOffset AddedAt { get; set; }
}
