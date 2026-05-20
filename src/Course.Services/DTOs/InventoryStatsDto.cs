namespace Course.Services.DTOs;

public class InventoryStatsDto
{
    public int ItemsCount { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? AvgPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public DateTimeOffset? LastItemUpdatedAt { get; set; }
}
