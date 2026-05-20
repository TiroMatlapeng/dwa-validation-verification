using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.Controllers;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Models.Enums;
using dwa_ver_val.Services.Notifications;
using dwa_ver_val.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class PropertyClaimControllerTests
{
    private static (ApplicationDBContext db, PropertyClaimController controller) Build(Guid userId)
    {
        var db = TestDbContextFactory.Create();
        var notify = new Mock<INotificationService>();
        var controller = new PropertyClaimController(db, notify.Object);
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

    [Fact]
    public async Task Submit_Post_ValidSGCode_CreatesPendingClaim()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);

        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            SGCode = "T1234",
            WmaId = null,
            PropertyReferenceNumber = "REF-001"
        };
        db.Properties.Add(property);
        db.PublicUsers.Add(new PublicUser
        {
            PublicUserId = userId,
            EmailAddress = "u@t.com",
            PasswordHash = "h",
            FirstName = "A",
            LastName = "B",
            Status = "Active",
            IsHDI = false
        });
        await db.SaveChangesAsync();

        var result = await controller.Submit(
            new PropertyClaimViewModel { PropertyCode = "T1234" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Pending", redirect.ActionName);

        var claim = db.PublicUserProperties.Single();
        Assert.Equal(userId, claim.PublicUserId);
        Assert.Equal(property.PropertyId, claim.PropertyId);
        Assert.Equal(PropertyClaimStatus.Pending, claim.Status);
    }

    [Fact]
    public async Task Submit_Post_UnknownCode_ReturnsViewWithError()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);
        db.PublicUsers.Add(new PublicUser
        {
            PublicUserId = userId,
            EmailAddress = "u@t.com",
            PasswordHash = "h",
            FirstName = "A",
            LastName = "B",
            Status = "Active",
            IsHDI = false
        });
        await db.SaveChangesAsync();

        var result = await controller.Submit(
            new PropertyClaimViewModel { PropertyCode = "NOPE" }, default);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Submit_Post_DuplicateClaim_ReturnsViewWithError()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);

        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            SGCode = "T9999",
            WmaId = null,
            PropertyReferenceNumber = "REF-DUP"
        };
        db.Properties.Add(property);
        db.PublicUsers.Add(new PublicUser
        {
            PublicUserId = userId,
            EmailAddress = "u@t.com",
            PasswordHash = "h",
            FirstName = "A",
            LastName = "B",
            Status = "Active",
            IsHDI = false
        });
        db.PublicUserProperties.Add(new PublicUserProperty
        {
            Id = Guid.NewGuid(),
            PublicUserId = userId,
            PropertyId = property.PropertyId,
            Status = PropertyClaimStatus.Pending,
            EvidenceType = PropertyClaimEvidenceType.IdMatch,
            RequestedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await controller.Submit(
            new PropertyClaimViewModel { PropertyCode = "T9999" }, default);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }
}
