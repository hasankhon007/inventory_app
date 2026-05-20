using Course.Domain.Abstractions;
using Course.Domain.Entities;
using Course.Domain.Enums;
using Course.Domain.Exceptions;
using Course.Services.DTOs;
using Course.Services.Interfaces;
using Course.Services.Utilities;
using Course.WebApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Course.WebApp.Controllers;

public class InventoriesController : Controller
{
    private readonly IAdminService _adminService;
    private readonly IInventoryService _inventoryService;
    private readonly IInventoryItemService _inventoryItemService;
    private readonly IInventoryAccessService _inventoryAccessService;
    private readonly IInventoryFieldService _inventoryFieldService;
    private readonly IInventoryTagService _inventoryTagService;
    private readonly IInventoryCreateValidator _inventoryCreateValidator;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;

    public InventoriesController(
        IAdminService adminService,
        IInventoryService inventoryService,
        IInventoryItemService inventoryItemService,
        IInventoryAccessService inventoryAccessService,
        IInventoryFieldService inventoryFieldService,
        IInventoryTagService inventoryTagService,
        IInventoryCreateValidator inventoryCreateValidator,
        UserManager<ApplicationUser> userManager,
        IUnitOfWork unitOfWork)
    {
        _inventoryService = inventoryService;
        _adminService = adminService;
        _inventoryItemService = inventoryItemService;
        _inventoryAccessService = inventoryAccessService;
        _inventoryFieldService = inventoryFieldService;
        _inventoryTagService = inventoryTagService;
        _inventoryCreateValidator = inventoryCreateValidator;
        _userManager = userManager;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var inventories = await _inventoryService.GetInventoryListAsync();
        var model = inventories.Select(i => new InventoryListItemViewModel
        {
            Id = i.Id,
            Title = i.Title,
            CategoryName = i.CategoryName,
            OwnerName = i.OwnerName,
            ItemsCount = i.ItemsCount
        }).ToList();

        return View(model);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var currentUserId = _userManager.GetUserId(User);
        var model = new InventoryCreateViewModel
        {
            Categories = await _unitOfWork.Repository<InventoryCategory>().Query().AsNoTracking().ToListAsync(),
            AvailableUsers = await LoadAvailableUsersAsync(currentUserId)
        };
        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InventoryCreateViewModel model)
    {
        var createDto = new InventoryCreateDto
        {
            Title = model.Title,
            IdFormat = model.IdFormat,
            Description = model.Description,
            CategoryId = model.CategoryId,
            IsPublic = model.IsPublic,
            SharedUserIds = model.SharedUserIds,
            SharedCanWrite = model.SharedCanWrite
        };

        var validationResult = await _inventoryCreateValidator.ValidateAsync(createDto);
        foreach (var error in validationResult.Errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        if (!ModelState.IsValid)
        {
            var currentUserId = _userManager.GetUserId(User);
            model.Categories = await _unitOfWork.Repository<InventoryCategory>().Query().AsNoTracking().ToListAsync();
            model.AvailableUsers = await LoadAvailableUsersAsync(currentUserId);
            return View(model);
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        await _inventoryService.CreateInventoryAsync(createDto, userId);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        try
        {
            var details = await _inventoryService.GetDetailsAsync(id);
            var userId = _userManager.GetUserId(User);
            var accessSnapshot = await _inventoryService.GetAccessSnapshotAsync(id, userId);

            if (!accessSnapshot.CanView)
            {
                return User.Identity?.IsAuthenticated == true ? Forbid() : Challenge();
            }

            var viewModel = new InventoryDetailsViewModel
            {
                Nav = new InventoryPageNavViewModel
                {
                    InventoryId = details.Id,
                    Title = details.Title,
                    ActiveTab = "overview",
                    CanEdit = accessSnapshot.CanWrite || accessSnapshot.IsOwner,
                    CanManageAccess = accessSnapshot.CanManage,
                    CanAddItems = accessSnapshot.CanAddItems
                },
                Id = details.Id,
                Title = details.Title,
                Description = details.Description,
                CategoryName = details.CategoryName,
                OwnerName = details.OwnerName,
                IsPublic = details.IsPublic,
                IdFormat = details.IdFormat,
                Comments = details.DiscussionPosts.Select(c => new DiscussionPostViewModel
                {
                    UserName = c.UserPreferredName,
                    Body = c.Body,
                    CreatedAt = c.CreatedAt
                }).ToList(),
                CanComment = User.Identity?.IsAuthenticated == true && accessSnapshot.CanView,
                CanEditIdFormat = accessSnapshot.CanWrite || accessSnapshot.IsOwner
            };

            return View(viewModel);
        }
        catch (NotFoundAppException)
        {
            return NotFound();
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(Guid id, string body)
    {
        var trimmedBody = body?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedBody))
        {
            TempData["CommentError"] = "Comment cannot be empty.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (trimmedBody.Length > 4000)
        {
            TempData["CommentError"] = "Comment is too long.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        try
        {
            await _inventoryService.AddCommentAsync(id, trimmedBody, userId);
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (NotFoundAppException)
        {
            return NotFound();
        }
        catch (ForbiddenAppException)
        {
            return Forbid();
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateIdFormat(Guid id, string idFormat)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        try
        {
            await _inventoryService.UpdateIdFormatAsync(id, idFormat, userId);
        }
        catch (ValidationAppException ex)
        {
            TempData["IdFormatError"] = ex.Message;
        }
        catch (ForbiddenAppException)
        {
            return Forbid();
        }
        catch (NotFoundAppException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> PreviewIdFormat()
    {
        var form = await Request.ReadFormAsync();
        var idFormat = form["idFormat"].FirstOrDefault() ?? form["IdFormat"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idFormat))
        {
            return BadRequest(new { error = "Missing id format." });
        }

        if (!InventoryIdFormatService.TryParse(idFormat, out var idElements, out var parseError))
        {
            return BadRequest(new { error = parseError ?? "Invalid format." });
        }

        var requiresSequence = idElements.Any(e => e.ElementType == InventoryIdElementType.Sequence);
        int? sequenceNumber = requiresSequence ? 1 : null;
        var now = DateTimeOffset.UtcNow;

        if (!InventoryIdFormatService.TryBuildCustomId(idElements, sequenceNumber, now, out var customId, out var buildError))
        {
            return BadRequest(new { error = buildError ?? "Unable to build preview." });
        }

        return Json(new { example = customId });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        try
        {
            var editDto = await _inventoryService.GetEditModelAsync(id);
            var userId = _userManager.GetUserId(User);
            var accessSnapshot = await _inventoryService.GetAccessSnapshotAsync(id, userId);

            if (!accessSnapshot.CanWrite)
            {
                return Forbid();
            }

            var model = new InventoryEditViewModel
            {
                Nav = new InventoryPageNavViewModel
                {
                    InventoryId = editDto.Id,
                    Title = editDto.Title,
                    ActiveTab = "edit",
                    CanEdit = true,
                    CanManageAccess = accessSnapshot.CanManage,
                    CanAddItems = accessSnapshot.CanAddItems
                },
                Id = editDto.Id,
                Title = editDto.Title,
                Description = editDto.Description,
                CategoryId = editDto.CategoryId,
                ImageUrl = editDto.ImageUrl,
                IsPublic = editDto.IsPublic,
                Categories = await _unitOfWork.Repository<InventoryCategory>().Query().AsNoTracking().ToListAsync(),
                AvailableUsers = await LoadAvailableUsersAsync(userId, editDto.OwnerId),
                SharedUserIds = await _unitOfWork.Repository<InventoryAccess>().Query()
                    .AsNoTracking()
                    .Where(access => access.InventoryId == id && access.UserId != editDto.OwnerId)
                    .Select(access => access.UserId)
                    .ToListAsync(),
                SharedCanWrite = (await _unitOfWork.Repository<InventoryAccess>().Query()
                    .AsNoTracking()
                    .Where(access => access.InventoryId == id && access.UserId != editDto.OwnerId)
                    .Select(access => access.CanWrite)
                    .ToListAsync())
                    .DefaultIfEmpty(true)
                    .All(canWrite => canWrite)
            };

            return View(model);
        }
        catch (NotFoundAppException)
        {
            return NotFound();
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, InventoryEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var userId = _userManager.GetUserId(User);
        var canManageAccess = false;
        var canAddItems = false;
        try
        {
            var accessSnapshot = await _inventoryService.GetAccessSnapshotAsync(id, userId);
            canManageAccess = accessSnapshot.CanManage;
            canAddItems = accessSnapshot.CanAddItems;
            if (!accessSnapshot.CanWrite)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                model.Categories = await _unitOfWork.Repository<InventoryCategory>().Query().AsNoTracking().ToListAsync();
                model.AvailableUsers = await LoadAvailableUsersAsync(userId);
                model.Nav = new InventoryPageNavViewModel
                {
                    InventoryId = id,
                    Title = model.Title,
                    ActiveTab = "edit",
                    CanEdit = true,
                    CanManageAccess = canManageAccess,
                    CanAddItems = canAddItems
                };
                return View(model);
            }

            var editDto = new InventoryEditDto
            {
                Id = id,
                Title = model.Title,
                Description = model.Description,
                CategoryId = model.CategoryId,
                ImageUrl = model.ImageUrl,
                IsPublic = model.IsPublic,
                SharedUserIds = model.SharedUserIds,
                SharedCanWrite = model.SharedCanWrite
            };

            await _inventoryService.UpdateInventoryAsync(editDto, userId);
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (ValidationAppException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.Categories = await _unitOfWork.Repository<InventoryCategory>().Query().AsNoTracking().ToListAsync();
            model.AvailableUsers = await LoadAvailableUsersAsync(userId);
            model.Nav = new InventoryPageNavViewModel
            {
                InventoryId = id,
                Title = model.Title,
                ActiveTab = "edit",
                CanEdit = true,
                CanManageAccess = canManageAccess,
                CanAddItems = canAddItems
            };
            return View(model);
        }
        catch (NotFoundAppException)
        {
            return NotFound();
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = _userManager.GetUserId(User);
        try
        {
            await _inventoryService.DeleteInventoryAsync(id, userId);
            return RedirectToAction(nameof(Index));
        }
        catch (NotFoundAppException)
        {
            return NotFound();
        }
        catch (ForbiddenAppException)
        {
            return Forbid();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Stats(Guid id)
    {
        try
        {
            var details = await _inventoryService.GetDetailsAsync(id);
            var userId = _userManager.GetUserId(User);
            var accessSnapshot = await _inventoryService.GetAccessSnapshotAsync(id, userId);

            if (!accessSnapshot.CanView)
            {
                return Forbid();
            }

            var stats = await _inventoryService.GetStatsAsync(id);
            var model = new InventoryStatsViewModel
            {
                Nav = new InventoryPageNavViewModel
                {
                    InventoryId = id,
                    Title = details.Title,
                    ActiveTab = "stats",
                    CanEdit = accessSnapshot.CanWrite,
                    CanManageAccess = accessSnapshot.CanManage,
                    CanAddItems = accessSnapshot.CanAddItems
                },
                ItemsCount = stats.ItemsCount,
                AveragePrice = stats.AvgPrice,
                MinPrice = stats.MinPrice,
                MaxPrice = stats.MaxPrice,
                LastItemUpdatedAt = stats.LastItemUpdatedAt
            };

            return View(model);
        }
        catch (NotFoundAppException)
        {
            return NotFound();
        }
    }

    [HttpGet]
    public async Task<IActionResult> CreateItem(Guid id)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            var accessSnapshot = await _inventoryService.GetAccessSnapshotAsync(id, userId);

            if (!accessSnapshot.CanAddItems)
            {
                return Forbid();
            }

            var details = await _inventoryService.GetDetailsAsync(id);
            var fieldsDto = await _inventoryItemService.GetFieldInputsAsync(id);

            var definitions = await _unitOfWork.Repository<InventoryFieldDefinition>().Query()
                .AsNoTracking()
                .Where(f => f.InventoryId == id)
                .OrderBy(f => f.SortOrder)
                .ToListAsync();

            var numberFields = definitions.Where(f => f.FieldType == InventoryFieldType.Number).ToList();
            var priceLabel = numberFields.Count > 0 ? numberFields[0].Title : "Price";

            var model = new InventoryItemCreateViewModel
            {
                Nav = new InventoryPageNavViewModel
                {
                    InventoryId = id,
                    Title = details.Title,
                    ActiveTab = "items",
                    CanEdit = accessSnapshot.CanWrite,
                    CanManageAccess = accessSnapshot.CanManage,
                    CanAddItems = accessSnapshot.CanAddItems
                },
                InventoryId = id,
                PriceLabel = priceLabel,
                Fields = fieldsDto.Where(f => !string.Equals(f.FieldType, InventoryFieldType.Number.ToString(), StringComparison.OrdinalIgnoreCase)).Select(f => new InventoryItemFieldInputViewModel
                {
                    Id = f.FieldDefinitionId,
                    FieldId = f.FieldDefinitionId,
                    FieldType = Enum.Parse<InventoryFieldType>(f.FieldType),
                    SlotIndex = f.SlotIndex,
                    Title = f.Title,
                    Description = f.Description,
                    TextValue = f.Value
                }).ToList()
                ,
                NumberFields = definitions.Where(f => f.FieldType == InventoryFieldType.Number).Skip(1).Select(f => new InventoryItemFieldInputViewModel
                {
                    Id = f.Id,
                    FieldId = f.Id,
                    FieldType = f.FieldType,
                    SlotIndex = f.SlotIndex,
                    Title = f.Title,
                    Description = f.Description
                }).ToList()
            };

            return View(model);
        }
        catch (NotFoundAppException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateItem(Guid id, InventoryItemCreateViewModel model)
    {
        if (id != model.InventoryId)
        {
            return BadRequest();
        }

        var userId = _userManager.GetUserId(User);
        try
        {
            var accessSnapshot = await _inventoryService.GetAccessSnapshotAsync(id, userId);
            if (!accessSnapshot.CanAddItems)
            {
                return Forbid();
            }

            var details = await _inventoryService.GetDetailsAsync(id);
            var definitions = await _unitOfWork.Repository<InventoryFieldDefinition>().Query()
                .AsNoTracking()
                .Where(f => f.InventoryId == id)
                .OrderBy(f => f.SortOrder)
                .ToListAsync();

            var numberFields = definitions.Where(f => f.FieldType == InventoryFieldType.Number).ToList();
            model.PriceLabel = numberFields.Count == 1 ? numberFields[0].Title : "Price";
            model.Nav = new InventoryPageNavViewModel
            {
                InventoryId = id,
                Title = details.Title,
                ActiveTab = "items",
                CanEdit = accessSnapshot.CanWrite,
                CanAddItems = accessSnapshot.CanAddItems
            };

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var validationErrors = ValidateItemFieldValues(definitions, model);
            foreach (var validationError in validationErrors)
            {
                ModelState.AddModelError(string.Empty, validationError);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var createDto = new ItemCreateDto
            {
                Name = model.Name,
                Price = model.Price ?? 0,
                Fields = model.Fields.Select(f => new ItemFieldInputDto
                {
                    FieldDefinitionId = f.FieldId,
                    FieldType = f.FieldType.ToString(),
                    SlotIndex = f.SlotIndex,
                    Title = f.Title,
                    Description = f.Description,
                    Value = f.FieldType switch
                    {
                        InventoryFieldType.SingleLineText => f.TextValue,
                        InventoryFieldType.MultiLineText => f.MultiTextValue,
                        InventoryFieldType.Link => f.LinkValue,
                        InventoryFieldType.Boolean => f.BoolValue.ToString(),
                        InventoryFieldType.Number => f.NumberValue?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        _ => null
                    }
                }).Concat(model.NumberFields.Select(f => new ItemFieldInputDto
                {
                    FieldDefinitionId = f.FieldId,
                    FieldType = f.FieldType.ToString(),
                    SlotIndex = f.SlotIndex,
                    Title = f.Title,
                    Description = f.Description,
                    Value = f.NumberValue?.ToString(System.Globalization.CultureInfo.InvariantCulture)
                })).ToList()
            };

            await _inventoryItemService.CreateItemAsync(id, createDto, userId);
            return RedirectToAction(nameof(Items), new { id });
        }
        catch (NotFoundAppException)
        {
            return NotFound();
        }
        catch (ValidationAppException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Items(Guid id)
    {
        try
        {
            var details = await _inventoryService.GetDetailsAsync(id);
            var userId = _userManager.GetUserId(User);
            var accessSnapshot = await _inventoryService.GetAccessSnapshotAsync(id, userId);

            if (!accessSnapshot.CanView)
            {
                return Forbid();
            }

            var items = await _inventoryItemService.GetItemsAsync(id, userId);
            var model = new InventoryItemsViewModel
            {
                Nav = new InventoryPageNavViewModel
                {
                    InventoryId = id,
                    Title = details.Title,
                    ActiveTab = "items",
                    CanEdit = accessSnapshot.CanWrite,
                    CanManageAccess = accessSnapshot.CanManage,
                    CanAddItems = accessSnapshot.CanAddItems
                },
                Items = items.Select(i => new InventoryItemListViewModel
                {
                    CustomId = i.CustomId,
                    Name = i.Name,
                    Price = i.Price,
                    UpdatedAt = i.UpdatedAt
                }).ToList()
            };

            return View(model);
        }
        catch (NotFoundAppException)
        {
            return NotFound();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Access(Guid id)
    {
        try
        {
            var details = await _inventoryService.GetDetailsAsync(id);
            var userId = _userManager.GetUserId(User);
            var accessSnapshot = await _inventoryService.GetAccessSnapshotAsync(id, userId);

            if (!accessSnapshot.CanManage)
            {
                return Forbid();
            }

            var accessList = await _inventoryAccessService.GetAccessListAsync(id, userId);
            var model = new InventoryAccessViewModel
            {
                Nav = new InventoryPageNavViewModel
                {
                    InventoryId = id,
                    Title = details.Title,
                    ActiveTab = "access",
                    CanEdit = true,
                    CanManageAccess = true,
                    CanAddItems = accessSnapshot.CanAddItems
                },
                AccessList = accessList.Select(a => new InventoryAccessListItemViewModel
                {
                    UserId = a.UserId,
                    UserName = a.Username,
                    Email = a.Email,
                    CanWrite = a.CanWrite,
                    AddedAt = a.AddedAt
                }).ToList()
            };

            return View(model);
        }
        catch (NotFoundAppException)
        {
            return NotFound();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Fields(Guid id)
    {
        try
        {
            var details = await _inventoryService.GetDetailsAsync(id);
            var userId = _userManager.GetUserId(User);
            var accessSnapshot = await _inventoryService.GetAccessSnapshotAsync(id, userId);

            if (!accessSnapshot.CanWrite)
            {
                return Forbid();
            }

            var fields = await _inventoryFieldService.GetFieldsAsync(id, userId);
            var model = new InventoryFieldsViewModel
            {
                Nav = new InventoryPageNavViewModel
                {
                    InventoryId = id,
                    Title = details.Title,
                    ActiveTab = "fields",
                    CanEdit = true,
                    CanManageAccess = accessSnapshot.CanManage,
                    CanAddItems = accessSnapshot.CanAddItems
                },
                Fields = fields.Select(f => new InventoryFieldListItemViewModel
                {
                    Id = f.Id,
                    Title = f.Title,
                    Description = f.Description,
                    FieldType = Enum.Parse<InventoryFieldType>(f.FieldType),
                    SlotIndex = f.SlotIndex,
                    ShowInTable = f.ShowInTable
                }).ToList()
            };

            return View(model);
        }
        catch (NotFoundAppException)
        {
            return NotFound();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Tags(Guid id)
    {
        try
        {
            var details = await _inventoryService.GetDetailsAsync(id);
            var userId = _userManager.GetUserId(User);
            var accessSnapshot = await _inventoryService.GetAccessSnapshotAsync(id, userId);

            if (!accessSnapshot.CanWrite)
            {
                return Forbid();
            }

            var tags = await _inventoryTagService.GetTagsAsync(id, userId);
            var model = new InventoryTagsViewModel
            {
                Nav = new InventoryPageNavViewModel
                {
                    InventoryId = id,
                    Title = details.Title,
                    ActiveTab = "tags",
                    CanEdit = true,
                    CanManageAccess = accessSnapshot.CanManage,
                    CanAddItems = accessSnapshot.CanAddItems
                },
                Tags = tags.Select(t => new InventoryTagListItemViewModel
                {
                    Id = t.TagId,
                    Name = t.TagName
                }).ToList()
            };

            return View(model);
        }
        catch (NotFoundAppException)
        {
            return NotFound();
        }
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> UsersLookup(Guid? inventoryId)
    {
        try
        {
            var excludedUserIds = new List<string>();
            var currentUserId = _userManager.GetUserId(User);
            if (!string.IsNullOrWhiteSpace(currentUserId))
            {
                excludedUserIds.Add(currentUserId);
            }

            if (inventoryId.HasValue)
            {
                var details = await _inventoryService.GetDetailsAsync(inventoryId.Value);
                if (!string.IsNullOrWhiteSpace(details.OwnerId))
                {
                    excludedUserIds.Add(details.OwnerId);
                }
            }

            var users = await _adminService.GetUserLookupListAsync(excludedUserIds);
            return Json(users.Select(user => new
            {
                id = user.Id,
                name = user.DisplayName,
                email = user.Email
            }));
        }
        catch (NotFoundAppException)
        {
            return NotFound();
        }
        catch (ForbiddenAppException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GrantAccess(Guid id, string targetUserId, bool canWrite)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            await _inventoryAccessService.GrantAccessAsync(id, targetUserId, canWrite, userId);
            return RedirectToAction(nameof(Access), new { id });
        }
        catch (NotFoundAppException)
        {
            return NotFound();
        }
        catch (ForbiddenAppException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeAccess(Guid id, string userId)
    {
        try
        {
            var current = _userManager.GetUserId(User);
            await _inventoryAccessService.RevokeAccessAsync(id, userId, current);
            return RedirectToAction(nameof(Access), new { id });
        }
        catch (NotFoundAppException)
        {
            return NotFound();
        }
        catch (ForbiddenAppException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddField(Guid id, InventoryFieldDto field)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            await _inventoryFieldService.AddFieldAsync(id, field, userId);
            return RedirectToAction(nameof(Fields), new { id });
        }
        catch (Exception ex) when (ex is ValidationAppException or ForbiddenAppException or NotFoundAppException)
        {
            TempData["FieldError"] = ex.Message;
            return RedirectToAction(nameof(Fields), new { id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateField(Guid id, InventoryFieldDto field)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            await _inventoryFieldService.UpdateFieldAsync(id, field, userId);
            return RedirectToAction(nameof(Fields), new { id });
        }
        catch (Exception ex) when (ex is ValidationAppException or ForbiddenAppException or NotFoundAppException)
        {
            TempData["FieldError"] = ex.Message;
            return RedirectToAction(nameof(Fields), new { id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveField(Guid id, Guid fieldId)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            await _inventoryFieldService.RemoveFieldAsync(id, fieldId, userId);
            return RedirectToAction(nameof(Fields), new { id });
        }
        catch (Exception ex) when (ex is ValidationAppException or ForbiddenAppException or NotFoundAppException)
        {
            TempData["FieldError"] = ex.Message;
            return RedirectToAction(nameof(Fields), new { id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTag(Guid id, string tagName)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            await _inventoryTagService.AddTagAsync(id, tagName, userId);
            return RedirectToAction(nameof(Tags), new { id });
        }
        catch (Exception ex) when (ex is ValidationAppException or ForbiddenAppException or NotFoundAppException)
        {
            TempData["TagError"] = ex.Message;
            return RedirectToAction(nameof(Tags), new { id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTag(Guid id, int tagId, string tagName)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            await _inventoryTagService.UpdateTagAsync(id, tagId, tagName, userId);
            return RedirectToAction(nameof(Tags), new { id });
        }
        catch (Exception ex) when (ex is ValidationAppException or ForbiddenAppException or NotFoundAppException)
        {
            TempData["TagError"] = ex.Message;
            return RedirectToAction(nameof(Tags), new { id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTag(Guid id, int tagId)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            await _inventoryTagService.RemoveTagAsync(id, tagId, userId);
            return RedirectToAction(nameof(Tags), new { id });
        }
        catch (Exception ex) when (ex is ValidationAppException or ForbiddenAppException or NotFoundAppException)
        {
            TempData["TagError"] = ex.Message;
            return RedirectToAction(nameof(Tags), new { id });
        }
    }

    private async Task<List<UserLookupViewModel>> LoadAvailableUsersAsync(params string?[] excludedUserIds)
    {
        var filteredExcludedUserIds = new List<string>();
        foreach (var excludedUserId in excludedUserIds)
        {
            if (!string.IsNullOrWhiteSpace(excludedUserId))
            {
                filteredExcludedUserIds.Add(excludedUserId);
            }
        }

        return (await _adminService.GetUserLookupListAsync(filteredExcludedUserIds))
            .Select(user => new UserLookupViewModel
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email
            })
            .ToList();
    }

    private static List<string> ValidateItemFieldValues(IEnumerable<InventoryFieldDefinition> definitions, InventoryItemCreateViewModel model)
    {
        var errors = new List<string>();
        var numberInputs = model.NumberFields.ToList();
        var firstNumberDefinition = definitions.FirstOrDefault(definition => definition.FieldType == InventoryFieldType.Number);

        foreach (var definition in definitions)
        {
            if (definition.FieldType == InventoryFieldType.Number)
            {
                if (firstNumberDefinition != null && definition.Id == firstNumberDefinition.Id)
                {
                    if (!model.Price.HasValue)
                    {
                        errors.Add($"{definition.Title} is required.");
                    }
                }
                else
                {
                    var numberField = numberInputs.FirstOrDefault(field => field.FieldId == definition.Id);
                    if (numberField == null || !numberField.NumberValue.HasValue)
                    {
                        errors.Add($"{definition.Title} is required.");
                    }
                }

                continue;
            }

            var fieldInput = model.Fields.FirstOrDefault(field => field.FieldId == definition.Id);
            var valueMissing = definition.FieldType switch
            {
                InventoryFieldType.SingleLineText => string.IsNullOrWhiteSpace(fieldInput?.TextValue),
                InventoryFieldType.MultiLineText => string.IsNullOrWhiteSpace(fieldInput?.MultiTextValue),
                InventoryFieldType.Link => string.IsNullOrWhiteSpace(fieldInput?.LinkValue),
                InventoryFieldType.Boolean => fieldInput == null,
                _ => true
            };

            if (valueMissing)
            {
                errors.Add($"{definition.Title} is required.");
            }
        }

        return errors;
    }
}
