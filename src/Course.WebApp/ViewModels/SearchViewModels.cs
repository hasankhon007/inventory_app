namespace Course.WebApp.ViewModels;

public class SearchIndexViewModel
{
    public string Query { get; set; } = string.Empty;
    public IReadOnlyList<SearchResultRowViewModel> Results { get; set; } = Array.Empty<SearchResultRowViewModel>();
}

public class SearchResultRowViewModel
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid InventoryId { get; set; }
    public bool IsItem { get; set; }
}
