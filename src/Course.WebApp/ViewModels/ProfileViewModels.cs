namespace Course.WebApp.ViewModels;

public class ProfileIndexViewModel
{
    public IReadOnlyList<ProfileInventoryRowViewModel> OwnedInventories { get; set; } = Array.Empty<ProfileInventoryRowViewModel>();
    public IReadOnlyList<ProfileInventoryRowViewModel> SharedInventories { get; set; } = Array.Empty<ProfileInventoryRowViewModel>();
}

public class ProfileInventoryRowViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public int ItemsCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool CanWrite { get; set; }
}
