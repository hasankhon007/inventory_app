using System.Security.Claims;
using Course.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Course.WebApp.Areas.Identity.Pages.Account;

public class ExternalLoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ExternalLoginModel> _logger;

    public ExternalLoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<ExternalLoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    public string? ReturnUrl { get; set; }

    public IActionResult OnPost(string provider, string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    public async Task<IActionResult> OnGetCallbackAsync(string? returnUrl = null, string? remoteError = null)
    {
        returnUrl ??= Url.Content("~/");
        if (!string.IsNullOrWhiteSpace(remoteError))
        {
            TempData["ErrorMessage"] = remoteError;
            return RedirectToPage("./Login", new { returnUrl });
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            TempData["ErrorMessage"] = "Error loading external login information.";
            return RedirectToPage("./Login", new { returnUrl });
        }

        var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (signInResult.Succeeded)
        {
            _logger.LogInformation("User logged in with {Provider}.", info.LoginProvider);
            return LocalRedirect(returnUrl);
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["ErrorMessage"] = "Google did not return an email address.";
            return RedirectToPage("./Login", new { returnUrl });
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user != null)
        {
            if (user.IsBlocked)
            {
                TempData["ErrorMessage"] = "Your account is blocked.";
                return RedirectToPage("./Login", new { returnUrl });
            }

            var addLoginResult = await _userManager.AddLoginAsync(user, info);
            if (!addLoginResult.Succeeded)
            {
                TempData["ErrorMessage"] = string.Join(", ", addLoginResult.Errors.Select(error => error.Description));
                return RedirectToPage("./Login", new { returnUrl });
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect(returnUrl);
        }

        var displayName = info.Principal.FindFirstValue(ClaimTypes.Name)
            ?? info.Principal.FindFirstValue("name")
            ?? email;

        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = displayName ?? email,
            PreferredTheme = "dark",
            PreferredCulture = "en",
            IsBlocked = false
        };

        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join(", ", createResult.Errors.Select(error => error.Description));
            return RedirectToPage("./Register", new { returnUrl });
        }

        var roleResult = await _userManager.AddToRoleAsync(user, "User");
        if (!roleResult.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join(", ", roleResult.Errors.Select(error => error.Description));
            return RedirectToPage("./Register", new { returnUrl });
        }

        var loginResult = await _userManager.AddLoginAsync(user, info);
        if (!loginResult.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join(", ", loginResult.Errors.Select(error => error.Description));
            return RedirectToPage("./Register", new { returnUrl });
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect(returnUrl);
    }
}