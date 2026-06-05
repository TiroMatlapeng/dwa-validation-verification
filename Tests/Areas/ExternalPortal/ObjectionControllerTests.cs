using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.Controllers;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Models.Enums;
using dwa_ver_val.Services.Notifications;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class ObjectionControllerTests
{
    private static (ApplicationDBContext db, ObjectionController controller) Build(Guid userId)
    {
        var db = TestDbContextFactory.Create();
        var accessor = new PublicUserPropertyAccessor(db);
        var notify = new Mock<INotificationService>();
        var logger = new Mock<ILogger<ObjectionController>>();
        var controller = new ObjectionController(db, accessor, notify.Object, logger.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                }, "test"))
            }
        };
        return (db, controller);
    }

    private static async Task<FileMaster> SeedApprovedCase(ApplicationDBContext db, Guid userId)
    {
        var prop = new Property { PropertyId = Guid.NewGuid(), SGCode = "T001", WmaId = null, PropertyReferenceNumber = "R1" };
        var fm = SeedHelper.NewFileMaster(prop.PropertyId);
        db.Properties.Add(prop);
        db.FileMasters.Add(fm);
        db.PublicUserProperties.Add(new PublicUserProperty
        {
            Id = Guid.NewGuid(), PublicUserId = userId, PropertyId = prop.PropertyId,
            Status = PropertyClaimStatus.Approved,
            EvidenceType = PropertyClaimEvidenceType.IdMatch, RequestedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return fm;
    }

    // ── EXT-01: GET access check ─────────────────────────────────────────────

    [Fact]
    public async Task Lodge_Get_LinkedCase_ReturnsView()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);
        var fm = await SeedApprovedCase(db, userId);

        var result = await controller.Lodge(fm.FileMasterId, default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ObjectionViewModel>(view.Model);
        Assert.Equal(fm.FileMasterId, model.FileMasterId);
    }

    [Fact]
    public async Task Lodge_Get_UnlinkedCase_ReturnsForbid()
    {
        var userId = Guid.NewGuid();
        var (_, controller) = Build(userId);

        var result = await controller.Lodge(Guid.NewGuid(), default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Lodge_Post_ValidGrounds_CreatesObjection()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);
        var fm = await SeedApprovedCase(db, userId);

        var result = await controller.Lodge(
            new ObjectionViewModel
            {
                FileMasterId = fm.FileMasterId,
                Grounds = "The ELU determination underestimates our historical use by a significant amount."
            }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", redirect.ActionName);

        var objection = db.Objections.Single();
        Assert.Equal(fm.FileMasterId, objection.FileMasterId);
        Assert.Equal(userId, objection.PublicUserId);
        Assert.Equal("Lodged", objection.Status);
        Assert.Equal(
            "The ELU determination underestimates our historical use by a significant amount.",
            objection.Grounds);
    }

    [Fact]
    public async Task Lodge_Post_DuplicateObjection_ReturnsViewWithError()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);
        var fm = await SeedApprovedCase(db, userId);
        db.Objections.Add(new Objection
        {
            ObjectionId = Guid.NewGuid(), FileMasterId = fm.FileMasterId,
            PublicUserId = userId, Status = "Lodged", LodgedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await controller.Lodge(
            new ObjectionViewModel
            {
                FileMasterId = fm.FileMasterId,
                Grounds = "Second objection attempt at repeating the previous grounds of appeal."
            }, default);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Lodge_Post_AfterResolution_CanLodgeSecondObjection()
    {
        // Verifies that a RESOLVED objection does not block a new one.
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);
        var fm = await SeedApprovedCase(db, userId);

        // First objection — already resolved.
        db.Objections.Add(new Objection
        {
            ObjectionId = Guid.NewGuid(), FileMasterId = fm.FileMasterId,
            PublicUserId = userId, Status = "Resolved", LodgedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Second objection — should succeed because no "Lodged" objection exists.
        var result = await controller.Lodge(
            new ObjectionViewModel
            {
                FileMasterId = fm.FileMasterId,
                Grounds = "Second objection after first was resolved."
            }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", redirect.ActionName);
        Assert.Equal(2, db.Objections.Count());
    }
}
