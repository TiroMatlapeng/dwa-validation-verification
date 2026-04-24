using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public class IdentitySeeder
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole<Guid>> _roles;
    private readonly ApplicationDBContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles,
        ApplicationDBContext db,
        IConfiguration config,
        ILogger<IdentitySeeder> logger)
    {
        _users = users;
        _roles = roles;
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await SeedRolesAsync();
        await SeedDemoUsersAsync();
    }

    private async Task SeedRolesAsync()
    {
        foreach (var roleName in DwsRoles.All)
        {
            if (!await _roles.RoleExistsAsync(roleName))
            {
                await _roles.CreateAsync(new IdentityRole<Guid>(roleName) { Id = Guid.NewGuid() });
                _logger.LogInformation("Seeded role {Role}", roleName);
            }
        }
    }

    private async Task SeedDemoUsersAsync()
    {
        var initialPassword = _config["Identity:InitialDemoPassword"];
        if (string.IsNullOrWhiteSpace(initialPassword))
        {
            _logger.LogInformation("Identity:InitialDemoPassword not set; skipping demo-user seed.");
            return;
        }

        var orgUnit = await _db.OrganisationalUnits
            .Include(ou => ou.WaterManagementArea)
            .FirstOrDefaultAsync(ou => ou.Type == "Regional" && ou.WaterManagementArea != null);
        if (orgUnit is null)
        {
            _logger.LogWarning("No Regional OrganisationalUnit with a WMA found; skipping demo-user seed.");
            return;
        }

        var wmaCode = orgUnit.WaterManagementArea!.WmaCode.ToLowerInvariant();

        var demos = new (string Email, string Role, string First, string Last, string EmpNo, Guid? OrgUnitId)[]
        {
            ("admin@dwa.demo",                DwsRoles.SystemAdmin,      "System",   "Admin",     "EMP-0001", null),
            ("national@dwa.demo",             DwsRoles.NationalManager,  "Nate",     "National",  "EMP-0002", null),
            ($"regional-{wmaCode}@dwa.demo",  DwsRoles.RegionalManager,  "Rita",     "Regional",  "EMP-0003", orgUnit.OrgUnitId),
            ($"validator-{wmaCode}@dwa.demo", DwsRoles.Validator,        "Jane",     "Validator", "EMP-1001", orgUnit.OrgUnitId),
            ($"capturer-{wmaCode}@dwa.demo",  DwsRoles.Capturer,         "Cody",     "Capturer",  "EMP-2001", orgUnit.OrgUnitId),
            ("readonly@dwa.demo",             DwsRoles.ReadOnly,         "Rosa",     "Reader",    "EMP-3001", orgUnit.OrgUnitId),
        };

        foreach (var demo in demos)
        {
            var existing = await _users.FindByEmailAsync(demo.Email);
            if (existing is not null) continue;

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = demo.Email,
                Email = demo.Email,
                EmailConfirmed = true,
                FirstName = demo.First,
                LastName = demo.Last,
                EmployeeNumber = demo.EmpNo,
                IsActive = true,
                OrgUnitId = demo.OrgUnitId
            };
            var create = await _users.CreateAsync(user, initialPassword);
            if (!create.Succeeded)
            {
                _logger.LogError("Failed to create demo user {Email}: {Errors}",
                    demo.Email, string.Join(", ", create.Errors.Select(e => e.Description)));
                continue;
            }
            await _users.AddToRoleAsync(user, demo.Role);
            _logger.LogInformation("Seeded demo user {Email} with role {Role}", demo.Email, demo.Role);
        }
    }
}
