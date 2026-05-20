using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.Controllers;
using dwa_ver_val.Models.Enums;
using dwa_ver_val.Services.Letters;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class CaseControllerTests
{
    private static (ApplicationDBContext db, CaseController controller) Build(Guid userId)
    {
        var db = TestDbContextFactory.Create();
        var accessor = new PublicUserPropertyAccessor(db);
        var blobs = new Mock<IBlobStore>();
        var controller = new CaseController(db, accessor, blobs.Object);
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

    private static FileMaster NewFileMaster(Guid propertyId, string farmName)
    {
        return new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = propertyId,
            RegistrationNumber = "WARMS-0001",
            SurveyorGeneralCode = "T0001",
            PrimaryCatchment = "A",
            QuaternaryCatchment = "A21A",
            FarmName = farmName,
            FarmNumber = 1,
            RegistrationDivision = "JR",
            FarmPortion = "0",
            FileCreatedDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
    }

    private static async Task<(Property prop, FileMaster fm)> SeedApprovedCase(
        ApplicationDBContext db, Guid userId)
    {
        var prop = new Property
        {
            PropertyId = Guid.NewGuid(),
            SGCode = "T0001",
            WmaId = null,
            PropertyReferenceNumber = "R001"
        };
        var fm = NewFileMaster(prop.PropertyId, "Test Farm");
        db.Properties.Add(prop);
        db.FileMasters.Add(fm);
        db.PublicUserProperties.Add(new PublicUserProperty
        {
            Id = Guid.NewGuid(),
            PublicUserId = userId,
            PropertyId = prop.PropertyId,
            Status = PropertyClaimStatus.Approved,
            EvidenceType = PropertyClaimEvidenceType.IdMatch,
            RequestedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return (prop, fm);
    }

    [Fact]
    public async Task Index_ReturnsOnlyApprovedCases()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);
        await SeedApprovedCase(db, userId);

        var prop2 = new Property
        {
            PropertyId = Guid.NewGuid(), SGCode = "T0002", WmaId = null, PropertyReferenceNumber = "R002"
        };
        var fm2 = NewFileMaster(prop2.PropertyId, "Pending Farm");
        db.Properties.Add(prop2);
        db.FileMasters.Add(fm2);
        db.PublicUserProperties.Add(new PublicUserProperty
        {
            Id = Guid.NewGuid(),
            PublicUserId = userId,
            PropertyId = prop2.PropertyId,
            Status = PropertyClaimStatus.Pending,
            EvidenceType = PropertyClaimEvidenceType.IdMatch,
            RequestedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await controller.Index(default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<dwa_ver_val.Areas.ExternalPortal.ViewModels.CaseSummaryViewModel>>(view.Model);
        Assert.Single(model);
    }

    [Fact]
    public async Task Detail_ApprovedCase_ReturnsView()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);
        var (_, fm) = await SeedApprovedCase(db, userId);

        var result = await controller.Detail(fm.FileMasterId, default);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Detail_UnlinkedCase_Returns404()
    {
        var userId = Guid.NewGuid();
        var (db, controller) = Build(userId);

        var result = await controller.Detail(Guid.NewGuid(), default);

        Assert.IsType<NotFoundResult>(result);
    }
}
