using Microsoft.AspNetCore.Mvc;

public class PropertyController : Controller
{
    private readonly ILogger<PropertyController> _logger;
    private readonly IPropertyInterface _propertyRepository;

    public PropertyController(ILogger<PropertyController> logger, IPropertyInterface propertyRepository)
    {
        _logger = logger;
        _propertyRepository = propertyRepository;
    }

    public IActionResult Index()
    {
        var properties = _propertyRepository.ListAll();
        return View(properties);
    }

    // GET: Property/Register
    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    // POST: Property/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Register(Property property)
    {
        if (ModelState.IsValid)
        {
            _propertyRepository.AddProperty(property);
            return RedirectToAction(nameof(Index));
        }
        return View(property);
    }
}