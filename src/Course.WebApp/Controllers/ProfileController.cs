using Course.Domain.Entities;
using Course.Services.Interfaces;
using Course.WebApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Course.WebApp.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly IProfileService _profileService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfileController(IProfileService profileService, UserManager<ApplicationUser> userManager)
    {
        _profileService = profileService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var profileDto = await _profileService.GetProfileAsync(userId);

        var model = new ProfileIndexViewModel
        {
            OwnedInventories = profileDto.OwnedInventories.Select(i => new ProfileInventoryRowViewModel
            {
                Id = i.Id,
                Title = i.Title,
                CategoryName = i.CategoryName,
                OwnerName = i.OwnerName,
                ItemsCount = i.ItemsCount,
                UpdatedAt = i.UpdatedAt,
                CanWrite = i.CanWrite
            }).ToList(),
            SharedInventories = profileDto.SharedInventories.Select(i => new ProfileInventoryRowViewModel
            {
                Id = i.Id,
                Title = i.Title,
                CategoryName = i.CategoryName,
                OwnerName = i.OwnerName,
                ItemsCount = i.ItemsCount,
                UpdatedAt = i.UpdatedAt,
                CanWrite = i.CanWrite
            }).ToList()
        };

        return View(model);
    }
}
