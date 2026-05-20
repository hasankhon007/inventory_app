using System.ComponentModel.DataAnnotations;
using Course.Domain.Entities;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Course.WebApp.ViewModels;

public class InventoryCreateViewModel
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string IdFormat { get; set; } = "INV-{SEQ:0000}";

    [MaxLength(2000)]
    public string? Description { get; set; }

    public int? CategoryId { get; set; }

    [MaxLength(2048)]
    public string? ImageUrl { get; set; }

    public bool IsPublic { get; set; } = true;

    [ValidateNever]
    public IReadOnlyList<UserLookupViewModel> AvailableUsers { get; set; } = Array.Empty<UserLookupViewModel>();

    public List<string> SharedUserIds { get; set; } = new();

    public bool SharedCanWrite { get; set; } = true;

    [ValidateNever]
    public IReadOnlyList<InventoryCategory> Categories { get; set; } = Array.Empty<InventoryCategory>();
}

public class UserLookupViewModel
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
}
