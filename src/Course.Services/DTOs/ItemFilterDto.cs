namespace Course.Services.DTOs;

public class ItemFilterDto
{
    public string? NameFilter { get; set; }
    public string? DescriptionFilter { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
    public Dictionary<Guid, string?> FieldFilters { get; set; } = new();
}
