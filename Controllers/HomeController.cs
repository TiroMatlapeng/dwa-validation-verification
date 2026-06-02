using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using dwa_ver_val.Models;
using dwa_ver_val.Services.Dashboard;

namespace dwa_ver_val.Controllers;

[Authorize(Policy = DwsPolicies.CanRead)]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IDashboardService _dashboard;

    public HomeController(ILogger<HomeController> logger, IDashboardService dashboard)
    {
        _logger = logger;
        _dashboard = dashboard;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var vm = await _dashboard.GetAsync(User, ct);
        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
