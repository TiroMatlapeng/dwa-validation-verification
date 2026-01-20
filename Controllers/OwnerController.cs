using Microsoft.AspNetCore.Mvc;

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