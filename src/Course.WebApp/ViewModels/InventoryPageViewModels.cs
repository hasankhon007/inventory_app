using System.ComponentModel.DataAnnotations;
using Course.Domain.Entities;
using Course.Domain.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Course.WebApp.ViewModels;

public class InventoryPageNavViewModel
{
    public Guid InventoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ActiveTab { get; set; } = "overview";
    public bool CanEdit { get; set; }
    public bool CanManageAccess { get; set; }
    public bool CanAddItems { get; set; }
}

public class InventoryEditViewModel
{
    public InventoryPageNavViewModel Nav { get; set; } = new();

    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public int? CategoryId { get; set; }

    [MaxLength(2048)]
    public string? ImageUrl { get; set; }

    public bool IsPublic { get; set; }

    [ValidateNever]
    public IReadOnlyList<UserLookupViewModel> AvailableUsers { get; set; } = Array.Empty<UserLookupViewModel>();

    public List<string> SharedUserIds { get; set; } = new();

    public bool SharedCanWrite { get; set; } = true;

    [ValidateNever]
    public IReadOnlyList<InventoryCategory> Categories { get; set; } = Array.Empty<InventoryCategory>();
}

public class InventoryStatsViewModel
{
    public InventoryPageNavViewModel Nav { get; set; } = new();
    public int ItemsCount { get; set; }
    public decimal? AveragePrice { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public DateTimeOffset? LastItemUpdatedAt { get; set; }
}

public class InventoryItemsViewModel
{
    public InventoryPageNavViewModel Nav { get; set; } = new();
    public IReadOnlyList<InventoryItemListViewModel> Items { get; set; } = Array.Empty<InventoryItemListViewModel>();
    public IReadOnlyList<CustomFieldColumnViewModel> FieldColumns { get; set; } = Array.Empty<CustomFieldColumnViewModel>();
    public string? SearchQuery { get; set; }
}

public class CustomFieldColumnViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public InventoryFieldType FieldType { get; set; }
}

public class InventoryItemCreateViewModel
{
    public InventoryPageNavViewModel Nav { get; set; } = new();
    public Guid InventoryId { get; set; }

    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(typeof(decimal), "0", "9999999999")]
    public decimal? Price { get; set; }

    [ValidateNever]
    public IReadOnlyList<CustomFieldInputViewModel> FieldDefinitions { get; set; }
        = Array.Empty<CustomFieldInputViewModel>();

    public Dictionary<string, string?> FieldValues { get; set; } = new();
}

public class InventoryItemEditViewModel
{
    public InventoryPageNavViewModel Nav { get; set; } = new();
    public Guid InventoryId { get; set; }
    public Guid ItemId { get; set; }

    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(typeof(decimal), "0", "9999999999")]
    public decimal? Price { get; set; }

    [ValidateNever]
    public IReadOnlyList<CustomFieldInputViewModel> FieldDefinitions { get; set; }
        = Array.Empty<CustomFieldInputViewModel>();

    public Dictionary<string, string?> FieldValues { get; set; } = new();
}

public class CustomFieldInputViewModel
{
    public Guid CustomFieldId { get; set; }
    public string Name { get; set; } = string.Empty;
    public InventoryFieldType FieldType { get; set; }
    public bool IsRequired { get; set; }
    public string? SettingsJson { get; set; }
    public string? Value { get; set; }
    public List<string>? SelectOptions { get; set; }
}

public class InventoryItemListViewModel
{
    public Guid Id { get; set; }
    public string CustomId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Dictionary<Guid, string?> FieldValues { get; set; } = new();
}

public class InventoryAccessViewModel
{
    public InventoryPageNavViewModel Nav { get; set; } = new();
    public IReadOnlyList<InventoryAccessListItemViewModel> AccessList { get; set; } = Array.Empty<InventoryAccessListItemViewModel>();
}

public class InventoryAccessListItemViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool CanWrite { get; set; }
    public DateTimeOffset AddedAt { get; set; }
}

public class InventoryFieldsViewModel
{
    public InventoryPageNavViewModel Nav { get; set; } = new();
    public IReadOnlyList<InventoryFieldListItemViewModel> Fields { get; set; } = Array.Empty<InventoryFieldListItemViewModel>();
}

public class InventoryFieldListItemViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public InventoryFieldType FieldType { get; set; }
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }
    public string? SettingsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class InventoryTagsViewModel
{
    public InventoryPageNavViewModel Nav { get; set; } = new();
    public IReadOnlyList<InventoryTagListItemViewModel> Tags { get; set; } = Array.Empty<InventoryTagListItemViewModel>();
}

public class InventoryTagListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
