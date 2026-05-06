using Microsoft.AspNetCore.Mvc;

namespace dwa_ver_val.Areas.ExternalPortal.Controllers;

[Area("ExternalPortal")]
[Route("ExternalPortal/[controller]/[action]")]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        ViewData["UserEmail"] = User.Identity?.Name;
        return View();
    }
}
