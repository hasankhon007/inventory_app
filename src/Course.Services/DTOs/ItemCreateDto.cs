namespace Course.Services.DTOs;

public class ItemCreateDto
{
    public string Name { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string? CustomId { get; set; }
    public List<ItemFieldInputDto> Fields { get; set; } = new();
}
