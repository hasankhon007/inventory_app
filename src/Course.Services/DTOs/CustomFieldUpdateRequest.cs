using System.ComponentModel.DataAnnotations;

namespace Course.Services.DTOs;

public class CustomFieldUpdateRequest
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string FieldType { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public int DisplayOrder { get; set; }

    [MaxLength(4000)]
    public string? SettingsJson { get; set; }
}
