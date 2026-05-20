namespace Course.WebApp.ViewModels;

public class InventoryListItemViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public int ItemsCount { get; set; }
}
