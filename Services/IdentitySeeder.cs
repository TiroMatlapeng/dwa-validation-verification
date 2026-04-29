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
        await PromoteAllowlistedAdminsAsync();
    }

    /// <summary>
    /// After seeding finishes, any ApplicationUser whose email appears in
    /// Identity:GrantAdminToEmails (comma-separated list) is auto-promoted to SystemAdmin.
    /// Idempotent: skips users already in the role. Designed for the demo so that the
    /// developer's own Microsoft account (after first JIT sign-in) lands as admin without
    /// needing a manual click through Manage Users.
    /// </summary>
    private async Task PromoteAllowlistedAdminsAsync()
    {
        var raw = _config["Identity:GrantAdminToEmails"];
        if (string.IsNullOrWhiteSpace(raw)) return;

        var emails = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant())
            .ToHashSet();

        foreach (var email in emails)
        {
            var user = await _users.FindByEmailAsync(email);
            if (user is null) continue;
            if (await _users.IsInRoleAsync(user, DwsRoles.SystemAdmin)) continue;
            var add = await _users.AddToRoleAsync(user, DwsRoles.SystemAdmin);
            if (add.Succeeded)
            {
                _logger.LogInformation("Promoted {Email} to SystemAdmin via Identity:GrantAdminToEmails allowlist.", email);
            }
            else
            {
                _logger.LogWarning("Failed to promote {Email} to SystemAdmin: {Errors}",
                    email, string.Join(", ", add.Errors.Select(e => e.Description)));
            }
        }
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
            if (existing is not null)
            {
                // Idempotently sync the password to the current Identity:InitialDemoPassword
                // value. Without this, rotating the config (e.g. promoting from dev → demo)
                // would never propagate, because seeded users created with the old password
                // are skipped here forever. Only the 6 hard-coded *@dwa.demo emails are touched.
                var token = await _users.GeneratePasswordResetTokenAsync(existing);
                var reset = await _users.ResetPasswordAsync(existing, token, initialPassword);
                if (!reset.Succeeded)
                {
                    _logger.LogWarning("Failed to sync password for demo user {Email}: {Errors}",
                        demo.Email, string.Join(", ", reset.Errors.Select(e => e.Description)));
                }

                // Idempotently ensure the role assignment. If a previous run / manual cleanup
                // stripped AspNetUserRoles, this restores it without touching any other user.
                var currentRoles = await _users.GetRolesAsync(existing);
                if (!currentRoles.Contains(demo.Role))
                {
                    var addRole = await _users.AddToRoleAsync(existing, demo.Role);
                    if (!addRole.Succeeded)
                    {
                        _logger.LogWarning("Failed to restore role {Role} for demo user {Email}: {Errors}",
                            demo.Role, demo.Email, string.Join(", ", addRole.Errors.Select(e => e.Description)));
                    }
                }
                continue;
            }

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
