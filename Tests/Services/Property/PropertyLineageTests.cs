using System.Security.Claims;
using dwa_ver_val.Tests.Helpers;
using dwa_ver_val.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace dwa_ver_val.Tests.Services.PropertyLineageTestsNamespace;

/// <summary>
/// Direct controller-level tests of the subdivide / consolidate flows.
/// Uses the InMemory provider; the controller already guards
/// <see cref="Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseFacadeExtensions"/>
/// usage with <c>Database.IsRelational()</c>, so transaction calls are no-ops here.
/// </summary>
public class PropertyLineageTests
{
    private static ApplicationDBContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ClaimsPrincipal Validator(Guid wmaId, Guid? userId = null)
    {
        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, (userId ?? Guid.NewGuid()).ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, "validator@dwa.demo"));
        identity.AddClaim(new Claim(ClaimTypes.Role, DwsRoles.Validator));
        identity.AddClaim(new Claim("wmaId", wmaId.ToString()));
        return new ClaimsPrincipal(identity);
    }

    private static PropertyController CreateController(
        ApplicationDBContext db,
        TestAuditService audit,
        ClaimsPrincipal user)
    {
        var controller = new PropertyController(
            NullLogger<PropertyController>.Instance,
            new PropertyRepository(db),
            db,
            new ScopedCaseQuery(db),
            audit);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.ControllerContext.HttpContext,
            new EmptyTempDataProvider());
        return controller;
    }

    private sealed class EmptyTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    [Fact]
    public async Task Subdivide_CreatesNChildren_AndMarksParentSubdivided()
    {
        await using var db = CreateDb();
        var wmaId = Guid.NewGuid();
        var parent = new Property
        {
            PropertyId = Guid.NewGuid(),
            SGCode = "PARENT-001",
            PropertySize = 100m,
            WmaId = wmaId,
            QuaternaryDrainage = "A21A",
            PropertyStatus = "Active"
        };
        db.Properties.Add(parent);
        await db.SaveChangesAsync();

        var audit = new TestAuditService();
        var sut = CreateController(db, audit, Validator(wmaId));
        var form = new SubdivideViewModel
        {
            Children = new List<SubdivideChildRow>
            {
                new() { SGCode = "PARENT-001-A", PropertySize = 30m },
                new() { SGCode = "PARENT-001-B", PropertySize = 30m },
                new() { SGCode = "PARENT-001-C", PropertySize = 40m }
            }
        };

        var result = await sut.Subdivide(parent.PropertyId, form);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);

        var refreshedParent = await db.Properties.FindAsync(parent.PropertyId);
        Assert.NotNull(refreshedParent);
        Assert.Equal("Subdivided", refreshedParent!.PropertyStatus);

        var children = await db.Properties
            .Where(p => p.ParentPropertyId == parent.PropertyId)
            .ToListAsync();
        Assert.Equal(3, children.Count);
        Assert.All(children, c => Assert.Equal("Active", c.PropertyStatus));
        Assert.All(children, c => Assert.Equal(wmaId, c.WmaId));
        Assert.Contains(children, c => c.SGCode == "PARENT-001-A" && c.PropertySize == 30m);

        Assert.Contains(audit.Events, e => e.Action == "PropertySubdivided" && e.EntityId == parent.PropertyId.ToString());
        Assert.Equal(3, audit.Events.Count(e => e.Action == "PropertyCreated"));
    }

    [Fact]
    public async Task Consolidate_MarksAllSources_AndCreatesNew()
    {
        await using var db = CreateDb();
        var wmaId = Guid.NewGuid();
        var s1 = new Property { PropertyId = Guid.NewGuid(), SGCode = "S1", PropertySize = 50m, WmaId = wmaId, PropertyStatus = "Active" };
        var s2 = new Property { PropertyId = Guid.NewGuid(), SGCode = "S2", PropertySize = 25m, WmaId = wmaId, PropertyStatus = "Active" };
        var s3 = new Property { PropertyId = Guid.NewGuid(), SGCode = "S3", PropertySize = 25m, WmaId = wmaId, PropertyStatus = "Active" };
        db.Properties.AddRange(s1, s2, s3);
        await db.SaveChangesAsync();

        var audit = new TestAuditService();
        var sut = CreateController(db, audit, Validator(wmaId));
        var form = new ConsolidateViewModel
        {
            Sources = new[] { s1.PropertyId, s2.PropertyId, s3.PropertyId },
            SGCode = "CONS-001",
            PropertyReferenceNumber = "REF-CONS-001"
        };

        var result = await sut.Consolidate(form);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        var newId = (Guid)redirect.RouteValues!["id"]!;

        var newProp = await db.Properties.FindAsync(newId);
        Assert.NotNull(newProp);
        Assert.Equal("Active", newProp!.PropertyStatus);
        Assert.Equal(100m, newProp.PropertySize); // 50 + 25 + 25
        Assert.Equal("CONS-001", newProp.SGCode);

        var refreshedSources = await db.Properties
            .Where(p => new[] { s1.PropertyId, s2.PropertyId, s3.PropertyId }.Contains(p.PropertyId))
            .ToListAsync();
        Assert.Equal(3, refreshedSources.Count);
        Assert.All(refreshedSources, p => Assert.Equal("Consolidated", p.PropertyStatus));
        Assert.All(refreshedSources, p => Assert.Equal(newId, p.SuccessorPropertyId));

        Assert.Contains(audit.Events, e => e.Action == "PropertyCreated" && e.EntityId == newId.ToString());
        Assert.Equal(3, audit.Events.Count(e => e.Action == "PropertyConsolidated"));
    }

    [Fact]
    public async Task Subdivide_OnAlreadySubdividedProperty_RejectsCleanly()
    {
        await using var db = CreateDb();
        var wmaId = Guid.NewGuid();
        var parent = new Property
        {
            PropertyId = Guid.NewGuid(),
            SGCode = "ALREADY-SUB",
            PropertySize = 100m,
            WmaId = wmaId,
            PropertyStatus = "Subdivided"
        };
        db.Properties.Add(parent);
        await db.SaveChangesAsync();
        var initialCount = await db.Properties.CountAsync();

        var audit = new TestAuditService();
        var sut = CreateController(db, audit, Validator(wmaId));
        var form = new SubdivideViewModel
        {
            Children = new List<SubdivideChildRow>
            {
                new() { SGCode = "X-A", PropertySize = 50m },
                new() { SGCode = "X-B", PropertySize = 50m }
            }
        };

        var result = await sut.Subdivide(parent.PropertyId, form);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.True(sut.TempData.ContainsKey("Error"));

        Assert.Equal(initialCount, await db.Properties.CountAsync());
        Assert.Empty(audit.Events);
    }

    [Fact]
    public async Task Subdivide_OutOfWmaProperty_ReturnsForbidAndDoesNotMutate()
    {
        using var db = CreateDb();
        var parentWma = Guid.NewGuid();
        var validatorWma = Guid.NewGuid();   // different WMA — out of scope
        var parent = new Property
        {
            PropertyId = Guid.NewGuid(),
            SGCode = "MP-OUT-OF-SCOPE",
            PropertySize = 100m,
            PropertyStatus = "Active",
            WmaId = parentWma
        };
        db.Properties.Add(parent);
        await db.SaveChangesAsync();
        var initialCount = await db.Properties.CountAsync();

        var audit = new TestAuditService();
        var sut = CreateController(db, audit, Validator(validatorWma));
        var form = new SubdivideViewModel
        {
            Children = new List<SubdivideChildRow>
            {
                new() { SGCode = "MP-A", PropertySize = 40m },
                new() { SGCode = "MP-B", PropertySize = 60m }
            }
        };

        var result = await sut.Subdivide(parent.PropertyId, form);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(initialCount, await db.Properties.CountAsync());
        Assert.Equal("Active", (await db.Properties.FindAsync(parent.PropertyId))!.PropertyStatus);
        Assert.Empty(audit.Events);
    }
}
