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
}

public class InventoryItemCreateViewModel
{
    public InventoryPageNavViewModel Nav { get; set; } = new();
    public Guid InventoryId { get; set; }

    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Range(typeof(decimal), "0", "9999999999")]
    public decimal? Price { get; set; }

    public string? PriceLabel { get; set; }

    public IReadOnlyList<InventoryItemFieldInputViewModel> NumberFields { get; set; }
        = Array.Empty<InventoryItemFieldInputViewModel>();

    public IReadOnlyList<InventoryItemFieldInputViewModel> Fields { get; set; }
        = Array.Empty<InventoryItemFieldInputViewModel>();
}

public class InventoryItemFieldInputViewModel
{
    public Guid Id { get; set; }
    public Guid FieldId { get; set; }
    public InventoryFieldType FieldType { get; set; }
    public int SlotIndex { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    [MaxLength(256)]
    public string? TextValue { get; set; }

    [MaxLength(2000)]
    public string? MultiTextValue { get; set; }

    [MaxLength(2048)]
    public string? LinkValue { get; set; }

    [Range(typeof(decimal), "0", "9999999999")]
    public decimal? NumberValue { get; set; }

    public bool BoolValue { get; set; }
}

public class InventoryItemListViewModel
{
    public string CustomId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
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
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public InventoryFieldType FieldType { get; set; }
    public int SlotIndex { get; set; }
    public bool ShowInTable { get; set; }
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
