using dwa_ver_val.Controllers.Admin;
using dwa_ver_val.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Controllers;

/// <summary>
/// Admin console surface: policy gating (reflection — the [Authorize] attribute is the
/// enforcement point), happy CRUD paths, and the FK-usage delete protections that keep
/// in-use reference data from being orphaned.
/// </summary>
public class AdminConsoleControllerTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static T WithTempData<T>(T controller) where T : Controller
    {
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
        return controller;
    }

    [Theory]
    [InlineData(typeof(OrganisationalUnitsController))]
    [InlineData(typeof(GwcasController))]
    [InlineData(typeof(ReferenceDataController))]
    public void AdminControllers_RequireCanAdministerPolicy(Type controllerType)
    {
        var attr = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();
        Assert.NotNull(attr);
        Assert.Equal(DwsPolicies.CanAdminister, attr!.Policy);
    }

    [Fact]
    public async Task CreateRiver_Persists_AndRejectsDuplicateName()
    {
        using var db = NewDb();
        var c = WithTempData(new ReferenceDataController(db));

        var first = await c.CreateRiver(new RiverFormViewModel { RiverName = "Sabie" }, CancellationToken.None);
        Assert.IsType<RedirectToActionResult>(first);
        Assert.Equal(1, await db.Rivers.CountAsync());

        var dup = await c.CreateRiver(new RiverFormViewModel { RiverName = "Sabie" }, CancellationToken.None);
        Assert.IsType<ViewResult>(dup); // re-rendered with a model error, nothing persisted
        Assert.False(c.ModelState.IsValid);
        Assert.Equal(1, await db.Rivers.CountAsync());
    }

    [Fact]
    public async Task DeleteRiver_Refused_WhenReferencedByDamCalculation()
    {
        using var db = NewDb();
        var river = new River { RiverId = Guid.NewGuid(), RiverName = "Blyde" };
        var property = new Property { PropertyId = Guid.NewGuid() };
        db.Rivers.Add(river);
        db.Properties.Add(property);
        db.DamCalculations.Add(new DamCalculation
        {
            DamCalculationId = Guid.NewGuid(),
            Property = property,
            PropertyId = property.PropertyId,
            River = river,
            RiverId = river.RiverId,
            SateliteSurveyDate = new DateOnly(2026, 6, 11),
            DamCapacity = 1000m,
        });
        await db.SaveChangesAsync();

        var c = WithTempData(new ReferenceDataController(db));
        var result = await c.DeleteRiver(river.RiverId, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(c.TempData["Error"]);
        Assert.Equal(1, await db.Rivers.CountAsync()); // still there
    }

    [Fact]
    public async Task DeleteRiver_Succeeds_WhenUnreferenced()
    {
        using var db = NewDb();
        var river = new River { RiverId = Guid.NewGuid(), RiverName = "Orphan" };
        db.Rivers.Add(river);
        await db.SaveChangesAsync();

        var c = WithTempData(new ReferenceDataController(db));
        await c.DeleteRiver(river.RiverId, CancellationToken.None);

        Assert.NotNull(c.TempData["Success"]);
        Assert.Equal(0, await db.Rivers.CountAsync());
    }

    [Fact]
    public async Task DeleteCatchment_Refused_WhenReferencedByProperty()
    {
        using var db = NewDb();
        var wmaId = Guid.NewGuid();
        var catchment = new CatchmentArea
        {
            CatchmentAreaId = Guid.NewGuid(),
            CatchmentCode = "X21A",
            CatchmentName = "Test Catchment",
            WmaId = wmaId,
        };
        db.CatchmentAreas.Add(catchment);
        db.Properties.Add(new Property { PropertyId = Guid.NewGuid(), CatchmentAreaId = catchment.CatchmentAreaId });
        await db.SaveChangesAsync();

        var c = WithTempData(new ReferenceDataController(db));
        await c.DeleteCatchment(catchment.CatchmentAreaId, CancellationToken.None);

        Assert.NotNull(c.TempData["Error"]);
        Assert.Equal(1, await db.CatchmentAreas.CountAsync());
    }

    [Fact]
    public async Task DeleteOrgUnit_Refused_WhenUsersAssigned()
    {
        using var db = NewDb();
        var unit = new OrganisationalUnit
        {
            OrgUnitId = Guid.NewGuid(),
            Name = "Mpumalanga Regional Office",
            Type = "Regional",
        };
        db.OrganisationalUnits.Add(unit);
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "assigned@dwa.test",
            FirstName = "Thabo",
            LastName = "Assigned",
            EmployeeNumber = "EMP-001",
            OrgUnitId = unit.OrgUnitId,
        });
        await db.SaveChangesAsync();

        var c = WithTempData(new OrganisationalUnitsController(db));
        await c.Delete(unit.OrgUnitId, CancellationToken.None);

        Assert.NotNull(c.TempData["Error"]);
        Assert.Equal(1, await db.OrganisationalUnits.CountAsync());
    }

    [Fact]
    public async Task ToggleRule_FlipsIsActive()
    {
        using var db = NewDb();
        var gwca = new GovernmentWaterControlArea
        {
            WaterControlAreaId = Guid.NewGuid(),
            GovernmentWaterControlAreaName = "Blyde River GWCA",
        };
        var rule = new GwcaProclamationRule
        {
            RuleId = Guid.NewGuid(),
            WaterControlAreaId = gwca.WaterControlAreaId,
            RuleCode = "MAX_HECTARES",
            RuleDescription = "Max irrigable hectares",
            IsActive = true,
        };
        db.GovernmentWaterControlAreas.Add(gwca);
        db.GwcaProclamationRules.Add(rule);
        await db.SaveChangesAsync();

        var c = WithTempData(new GwcasController(db));
        await c.ToggleRule(rule.RuleId, CancellationToken.None);
        Assert.False((await db.GwcaProclamationRules.SingleAsync()).IsActive);

        await c.ToggleRule(rule.RuleId, CancellationToken.None);
        Assert.True((await db.GwcaProclamationRules.SingleAsync()).IsActive);
    }
}
