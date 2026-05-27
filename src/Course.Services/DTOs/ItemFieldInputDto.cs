namespace Course.Services.DTOs;

public class ItemFieldInputDto
{
    public Guid CustomFieldId { get; set; }
    public string FieldType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsRequired { get; set; }
    public string? SettingsJson { get; set; }
    public string? Value { get; set; }
    public List<string>? SelectOptions { get; set; }
}
