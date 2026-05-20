using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.Controllers;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Models.Enums;
using dwa_ver_val.Services.Notifications;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class ResponseControllerTests
{
    private static (ApplicationDBContext db, ResponseController controller) Build(Guid userId)
    {
        var db = TestDbContextFactory.Create();
        var accessor = new PublicUserPropertyAccessor(db);
        var notify = new Mock<INotificationService>();
        var controller = new ResponseController(db, accessor, notify.Object);
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

    [Fact]
    public async Task Submit_Post_ValidResponse_CreatesCaseComment()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);
        var fm = await SeedApprovedCase(db, userId);

        var result = await controller.Submit(
            new LetterResponseViewModel
            {
                FileMasterId = fm.FileMasterId,
                ResponseText = "I agree with the findings."
            }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", redirect.ActionName);

        var comment = db.CaseComments.Single();
        Assert.Equal(fm.FileMasterId, comment.FileMasterId);
        Assert.Equal(userId, comment.PublicUserId);
        Assert.Equal("PublicUser", comment.AuthorType);
        Assert.Equal("I agree with the findings.", comment.CommentText);
    }

    [Fact]
    public async Task Submit_Post_UnlinkedCase_ReturnsForbid()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);

        var result = await controller.Submit(
            new LetterResponseViewModel
            {
                FileMasterId = Guid.NewGuid(),
                ResponseText = "Anything at all to pass length validation."
            }, default);

        Assert.IsType<ForbidResult>(result);
    }
}
