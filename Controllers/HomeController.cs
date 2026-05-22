using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using dwa_ver_val.Models;

namespace dwa_ver_val.Controllers;

[Authorize(Policy = DwsPolicies.CanRead)]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDBContext _db;

    public HomeController(ILogger<HomeController> logger, ApplicationDBContext db)
    {
        _logger = logger;
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        ViewBag.TotalProperties = await _db.Properties.CountAsync();
        ViewBag.CompletedCases = await _db.FileMasters.CountAsync(f => f.ValidationStatusName == "Completed");
        ViewBag.InProcessCases = await _db.FileMasters.CountAsync(f => f.ValidationStatusName == "In Process");
        ViewBag.OverdueTasks = await _db.LetterIssuances
            .CountAsync(l => l.DueDate != null && l.DueDate < today && l.ResponseDate == null);
        ViewBag.LettersPending = await _db.LetterIssuances
            .CountAsync(l => l.ResponseDate == null && l.DueDate != null);

        return View();
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
