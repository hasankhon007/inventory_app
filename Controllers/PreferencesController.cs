using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Course.Controllers;

public class PreferencesController : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Theme(string theme, string returnUrl)
    {
        var normalized = string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase) ? "dark" : "light";
        Response.Cookies.Append(
            "theme",
            normalized,
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            });

        return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Language(string culture, string returnUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(culture) ? "en" : culture;
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(normalized)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            });

        return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
    }
}
