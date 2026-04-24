using dwa_ver_val.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Controllers.Admin;

[Authorize(Policy = DwsPolicies.CanAdminister)]
[Route("Admin/[controller]/[action]")]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole<Guid>> _roles;
    private readonly ApplicationDBContext _db;

    public UsersController(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles,
        ApplicationDBContext db)
    {
        _users = users;
        _roles = roles;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await _db.Users
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.EmployeeNumber,
                u.IsActive,
                OrgUnitName = u.OrgUnit != null ? u.OrgUnit.Name : null
            })
            .ToListAsync();

        var userRoleLookup = await (
            from ur in _db.Set<IdentityUserRole<Guid>>()
            join r in _db.Roles on ur.RoleId equals r.Id
            select new { ur.UserId, RoleName = r.Name! })
            .ToListAsync();
        var rolesByUser = userRoleLookup
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).ToList());

        var items = users.Select(u => new UserListItemViewModel
        {
            Id = u.Id,
            Email = u.Email ?? string.Empty,
            FullName = $"{u.FirstName} {u.LastName}",
            EmployeeNumber = u.EmployeeNumber,
            Role = rolesByUser.TryGetValue(u.Id, out var rs) ? rs.FirstOrDefault() ?? "(none)" : "(none)",
            OrgUnitName = u.OrgUnitName,
            IsActive = u.IsActive
        }).ToList();

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CreateUserViewModel
        {
            AvailableRoles = DwsRoles.All,
            AvailableOrgUnits = await LoadOrgUnits()
        };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableRoles = DwsRoles.All;
            model.AvailableOrgUnits = await LoadOrgUnits();
            return View(model);
        }

        if (!DwsRoles.All.Contains(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Unknown role.");
            model.AvailableRoles = DwsRoles.All;
            model.AvailableOrgUnits = await LoadOrgUnits();
            return View(model);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            FirstName = model.FirstName,
            LastName = model.LastName,
            EmployeeNumber = model.EmployeeNumber,
            OrgUnitId = model.OrgUnitId,
            IsActive = true
        };

        var create = await _users.CreateAsync(user, model.InitialPassword);
        if (!create.Succeeded)
        {
            foreach (var e in create.Errors) ModelState.AddModelError(string.Empty, e.Description);
            model.AvailableRoles = DwsRoles.All;
            model.AvailableOrgUnits = await LoadOrgUnits();
            return View(model);
        }

        await _users.AddToRoleAsync(user, model.Role);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        var roles = await _users.GetRolesAsync(user);

        return View(new EditUserViewModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            EmployeeNumber = user.EmployeeNumber,
            Role = roles.FirstOrDefault() ?? string.Empty,
            OrgUnitId = user.OrgUnitId,
            IsActive = user.IsActive,
            AvailableRoles = DwsRoles.All,
            AvailableOrgUnits = await LoadOrgUnits()
        });
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, EditUserViewModel model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            model.AvailableRoles = DwsRoles.All;
            model.AvailableOrgUnits = await LoadOrgUnits();
            return View(model);
        }

        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.EmployeeNumber = model.EmployeeNumber;
        user.OrgUnitId = model.OrgUnitId;
        user.IsActive = model.IsActive;

        var update = await _users.UpdateAsync(user);
        if (!update.Succeeded)
        {
            foreach (var e in update.Errors) ModelState.AddModelError(string.Empty, e.Description);
            model.AvailableRoles = DwsRoles.All;
            model.AvailableOrgUnits = await LoadOrgUnits();
            return View(model);
        }

        var currentRoles = await _users.GetRolesAsync(user);
        if (!currentRoles.Contains(model.Role))
        {
            await _users.RemoveFromRolesAsync(user, currentRoles);
            await _users.AddToRoleAsync(user, model.Role);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ResetPassword(Guid id)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        return View(new ResetPasswordViewModel { UserId = user.Id, Email = user.Email ?? string.Empty });
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordViewModel model)
    {
        if (id != model.UserId) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        user.IsActive = false;
        await _users.UpdateAsync(user);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        user.IsActive = true;
        await _users.UpdateAsync(user);
        return RedirectToAction(nameof(Index));
    }

    private async Task<IEnumerable<(Guid Id, string Name)>> LoadOrgUnits() =>
        await _db.OrganisationalUnits
            .OrderBy(ou => ou.Name)
            .Select(ou => new ValueTuple<Guid, string>(ou.OrgUnitId, ou.Name))
            .ToListAsync();
}
