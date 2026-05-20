namespace Course.Services.DTOs;

public class UserListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public bool IsBlocked { get; set; }
    public List<string> Roles { get; set; } = new();
}
