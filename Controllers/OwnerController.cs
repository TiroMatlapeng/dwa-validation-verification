using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize(Policy = DwsPolicies.CanAdminister)]
public class OwnerController : Controller
{
    private readonly ILogger<OwnerController> _logger;

    public OwnerController(ILogger<OwnerController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Register()
    {
        return View();
    }
}
