using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Course.Controllers;

[Authorize]
public class ProfileController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
