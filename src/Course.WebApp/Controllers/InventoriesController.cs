using Course.DataAccess.UnitOfWork;
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
using System.Globalization;
using System.IO;
using System.Text;
using Course.Services.Implementations;

namespace Course.WebApp.Controllers;

public class InventoriesController : Controller
{
    private readonly IAdminService _adminService;
    private readonly IInventoryService _inventoryService;
    private readonly IInventoryItemService _inventoryItemService;
    private readonly IInventoryAccessService _inventoryAccessService;
    private readonly ICustomFieldService _customFieldService;
    private readonly IInventoryTagService _inventoryTagService;
    private readonly IInventoryCreateValidator _inventoryCreateValidator;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;

    public InventoriesController(
        IAdminService adminService,
        IInventoryService inventoryService,
        IInventoryItemService inventoryItemService,
        IInventoryAccessService inventoryAccessService,
        ICustomFieldService customFieldService,
        IInventoryTagService inventoryTagService,
        IInventoryCreateValidator inventoryCreateValidator,
        UserManager<ApplicationUser> userManager,
        IUnitOfWork unitOfWork)
    {
        _inventoryService = inventoryService;
        _adminService = adminService;
        _inventoryItemService = inventoryItemService;
        _inventoryAccessService = inventoryAccessService;
        _customFieldService = customFieldService;
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
            var fieldInputs = await _inventoryItemService.GetFieldInputsAsync(id);

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
                FieldDefinitions = fieldInputs.Select(f => new CustomFieldInputViewModel
                {
                    CustomFieldId = f.CustomFieldId,
                    Name = f.Name,
                    FieldType = Enum.Parse<InventoryFieldType>(f.FieldType),
                    IsRequired = f.IsRequired,
                    SettingsJson = f.SettingsJson,
                    Value = f.Value,
                    SelectOptions = f.SelectOptions
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
            model.Nav = new InventoryPageNavViewModel
            {
                InventoryId = id,
                Title = details.Title,
                ActiveTab = "items",
                CanEdit = accessSnapshot.CanWrite,
                CanAddItems = accessSnapshot.CanAddItems
            };

            // Reload field definitions for re-rendering
            var fieldInputs = await _inventoryItemService.GetFieldInputsAsync(id);
            model.FieldDefinitions = fieldInputs.Select(f => new CustomFieldInputViewModel
            {
                CustomFieldId = f.CustomFieldId,
                Name = f.Name,
                FieldType = Enum.Parse<InventoryFieldType>(f.FieldType),
                IsRequired = f.IsRequired,
                SettingsJson = f.SettingsJson,
                Value = model.FieldValues.TryGetValue(f.CustomFieldId.ToString(), out var v) ? v : f.Value,
                SelectOptions = f.SelectOptions
            }).ToList();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Build field values dictionary (string key → Guid key)
            var fieldValues = new Dictionary<Guid, string?>();
            foreach (var (key, value) in model.FieldValues)
            {
                if (Guid.TryParse(key, out var fieldId))
                {
                    fieldValues[fieldId] = value;
                }
            }

            var createDto = new ItemCreateDto
            {
                Name = model.Name,
                Description = model.Description,
                Price = model.Price ?? 0,
                FieldValues = fieldValues
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
    public async Task<IActionResult> EditItem(Guid id, Guid itemId)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            var accessSnapshot = await _inventoryService.GetAccessSnapshotAsync(id, userId);

            if (!accessSnapshot.CanWrite && !accessSnapshot.IsOwner)
            {
                return Forbid();
            }

            var details = await _inventoryService.GetDetailsAsync(id);
            var item = await _inventoryItemService.GetItemAsync(itemId, userId);
            var fieldInputs = await _inventoryItemService.GetFieldInputsAsync(id, itemId);

            var model = new InventoryItemEditViewModel
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
                ItemId = itemId,
                Name = item.Name,
                Description = item.Description,
                Price = item.Price,
                FieldDefinitions = fieldInputs.Select(f => new CustomFieldInputViewModel
                {
                    CustomFieldId = f.CustomFieldId,
                    Name = f.Name,
                    FieldType = Enum.Parse<InventoryFieldType>(f.FieldType),
                    IsRequired = f.IsRequired,
                    SettingsJson = f.SettingsJson,
                    Value = f.Value,
                    SelectOptions = f.SelectOptions
                }).ToList(),
                FieldValues = fieldInputs.ToDictionary(f => f.CustomFieldId.ToString(), f => f.Value)
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
    public async Task<IActionResult> EditItem(Guid id, InventoryItemEditViewModel model)
    {
        if (id != model.InventoryId)
        {
            return BadRequest();
        }

        var userId = _userManager.GetUserId(User);
        try
        {
            var accessSnapshot = await _inventoryService.GetAccessSnapshotAsync(id, userId);
            if (!accessSnapshot.CanWrite && !accessSnapshot.IsOwner)
            {
                return Forbid();
            }

            var details = await _inventoryService.GetDetailsAsync(id);
            model.Nav = new InventoryPageNavViewModel
            {
                InventoryId = id,
                Title = details.Title,
                ActiveTab = "items",
                CanEdit = accessSnapshot.CanWrite,
                CanAddItems = accessSnapshot.CanAddItems
            };

            var fieldInputs = await _inventoryItemService.GetFieldInputsAsync(id, model.ItemId);
            model.FieldDefinitions = fieldInputs.Select(f => new CustomFieldInputViewModel
            {
                CustomFieldId = f.CustomFieldId,
                Name = f.Name,
                FieldType = Enum.Parse<InventoryFieldType>(f.FieldType),
                IsRequired = f.IsRequired,
                SettingsJson = f.SettingsJson,
                Value = model.FieldValues.TryGetValue(f.CustomFieldId.ToString(), out var v) ? v : f.Value,
                SelectOptions = f.SelectOptions
            }).ToList();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var fieldValues = new Dictionary<Guid, string?>();
            foreach (var (key, value) in model.FieldValues)
            {
                if (Guid.TryParse(key, out var fieldId))
                {
                    fieldValues[fieldId] = value;
                }
            }

            var editDto = new ItemEditDto
            {
                Id = model.ItemId,
                Name = model.Name,
                Description = model.Description,
                Price = model.Price ?? 0,
                FieldValues = fieldValues
            };

            await _inventoryItemService.UpdateItemAsync(id, editDto, userId);
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
    public async Task<IActionResult> Items(Guid id, string? q)
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

            var items = string.IsNullOrWhiteSpace(q)
                ? await _inventoryItemService.GetItemsAsync(id, userId)
                : await _inventoryItemService.SearchItemsAsync(id, q, userId);

            var customFields = await _customFieldService.GetFieldsAsync(id, userId ?? string.Empty);

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
                    Id = i.Id,
                    CustomId = i.CustomId,
                    Name = i.Name,
                    Description = i.Description,
                    Price = i.Price,
                    UpdatedAt = i.UpdatedAt,
                    FieldValues = i.FieldValues
                }).ToList(),
                FieldColumns = customFields.Select(f => new CustomFieldColumnViewModel
                {
                    Id = f.Id,
                    Name = f.Name,
                    FieldType = Enum.Parse<InventoryFieldType>(f.FieldType)
                }).ToList(),
                SearchQuery = q
            };

            return View(model);
        }
        catch (NotFoundAppException)
        {
            return NotFound();
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportItems(Guid id)
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
            var customFields = await _customFieldService.GetFieldsAsync(id, userId ?? string.Empty);

            // Build headers: fixed + dynamic
            var headers = new List<string> { "Custom ID", "Name", "Description", "Price", "Updated At" };
            headers.AddRange(customFields.Select(f => f.Name));

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers.Select(CsvEscape)));

            foreach (var item in items)
            {
                var row = new List<string>
                {
                    item.CustomId,
                    item.Name,
                    item.Description,
                    item.Price.ToString(CultureInfo.InvariantCulture),
                    item.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                };

                foreach (var field in customFields)
                {
                    var fieldId = Guid.Parse(field.Id.ToString());
                    row.Add(item.FieldValues.TryGetValue(fieldId, out var value) ? value ?? string.Empty : string.Empty);
                }

                sb.AppendLine(string.Join(",", row.Select(CsvEscape)));
            }

            var fileName = $"{SanitizeFileName(details.Title)}-items-{DateTimeOffset.UtcNow:yyyyMMddHHmm}.csv";
            var bytes = new UTF8Encoding(true).GetBytes(sb.ToString());
            return File(bytes, "text/csv", fileName);
        }
        catch (NotFoundAppException)
        {
            return NotFound();
        }
        catch (UnauthorizedAppException)
        {
            return Challenge();
        }
        catch (ForbiddenAppException)
        {
            return Forbid();
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportItemsExcel(Guid id)
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
            var customFields = await _customFieldService.GetFieldsAsync(id, userId ?? string.Empty);

            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Items");

            // Build headers
            var headers = new List<string> { "Custom ID", "Name", "Description", "Price", "Updated At" };
            headers.AddRange(customFields.Select(f => f.Name));

            for (var i = 0; i < headers.Count; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
                worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            }

            var rowIndex = 2;
            foreach (var item in items)
            {
                worksheet.Cell(rowIndex, 1).Value = item.CustomId;
                worksheet.Cell(rowIndex, 2).Value = item.Name;
                worksheet.Cell(rowIndex, 3).Value = item.Description;
                worksheet.Cell(rowIndex, 4).Value = item.Price;
                worksheet.Cell(rowIndex, 5).Value = item.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

                var colIndex = 6;
                foreach (var field in customFields)
                {
                    if (item.FieldValues.TryGetValue(field.Id, out var value))
                    {
                        worksheet.Cell(rowIndex, colIndex).Value = value ?? string.Empty;
                    }
                    colIndex++;
                }
                rowIndex++;
            }

            worksheet.Columns().AdjustToContents();

            var fileName = $"{SanitizeFileName(details.Title)}-items-{DateTimeOffset.UtcNow:yyyyMMddHHmm}.xlsx";
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();

            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
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

            var fields = await _customFieldService.GetFieldsAsync(id, userId);
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
                    Name = f.Name,
                    FieldType = Enum.Parse<InventoryFieldType>(f.FieldType),
                    IsRequired = f.IsRequired,
                    DisplayOrder = f.DisplayOrder,
                    SettingsJson = f.SettingsJson,
                    CreatedAt = f.CreatedAt
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
    public async Task<IActionResult> AddField([FromRoute] Guid id, CustomFieldCreateRequest request)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            await _customFieldService.AddFieldAsync(id, request, userId);
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
    public async Task<IActionResult> UpdateField([FromRoute] Guid id, CustomFieldUpdateRequest request)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            await _customFieldService.UpdateFieldAsync(id, request, userId);
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
    public async Task<IActionResult> RemoveField([FromRoute] Guid id, Guid fieldId)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            await _customFieldService.DeleteFieldAsync(id, fieldId, userId);
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
    public async Task<IActionResult> ReorderFields([FromRoute] Guid id, ReorderFieldsRequest request)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            await _customFieldService.ReorderFieldsAsync(id, request, userId);
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
    public async Task<IActionResult> DeleteItem(Guid id, Guid itemId)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            await _inventoryItemService.DeleteItemAsync(itemId, userId);
            return RedirectToAction(nameof(Items), new { id });
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

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var escaped = value.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{escaped}\"" : escaped;
    }

    private static string SanitizeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "inventory";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "inventory" : sanitized;
    }
}
