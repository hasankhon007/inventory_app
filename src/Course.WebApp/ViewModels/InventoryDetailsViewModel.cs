namespace Course.WebApp.ViewModels;

public class InventoryDetailsViewModel
{
    public InventoryPageNavViewModel Nav { get; set; } = new();
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CategoryName { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public string IdFormat { get; set; } = string.Empty;
    public IReadOnlyList<DiscussionPostViewModel> Comments { get; set; } = Array.Empty<DiscussionPostViewModel>();
    public bool CanComment { get; set; }
    public bool CanEditIdFormat { get; set; }
}

public class DiscussionPostViewModel
{
    public string UserName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
