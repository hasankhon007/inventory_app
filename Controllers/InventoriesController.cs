using Microsoft.AspNetCore.Mvc;

namespace Course.Controllers;

public class InventoriesController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
