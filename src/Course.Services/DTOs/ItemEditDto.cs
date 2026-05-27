namespace Course.Services.DTOs;

public class ItemEditDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public Dictionary<Guid, string?> FieldValues { get; set; } = new();
}
