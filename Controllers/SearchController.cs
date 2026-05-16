using Microsoft.AspNetCore.Mvc;

namespace Course.Controllers;

public class SearchController : Controller
{
    [HttpGet]
    public IActionResult Index(string? q)
    {
        ViewData["Query"] = q ?? string.Empty;
        return View();
    }
}
