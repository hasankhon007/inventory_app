using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Course.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
