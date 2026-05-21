using System.Reflection;
using System.Security.Claims;
using dwa_ver_val.Services.Letters;
using dwa_ver_val.Services.Notifications;
using dwa_ver_val.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Controllers;

public class FileMasterControllerLetterTests
{
    [Fact]
    public async Task IssueLetter_WhenPendingIssuanceExists_ShouldNotBeAbleToAddSecond()
    {
        // S35_L1 is a one-time letter — re-issuance is blocked even while the first is still Pending.
        var db = TestDbContextFactory.Create();
        var prop = new Property { PropertyId = Guid.NewGuid() };
        db.Properties.Add(prop);
        var fm = SeedHelper.NewFileMaster(prop.PropertyId);
        db.FileMasters.Add(fm);
        var lt = new LetterType { LetterTypeId = Guid.NewGuid(), LetterName = "S35_L1", LetterDescription = "S35 Letter 1" };
        db.LetterTypes.Add(lt);
        db.LetterIssuances.Add(new LetterIssuance
        {
            LetterIssuanceId = Guid.NewGuid(),
            FileMasterId = fm.FileMasterId,
            LetterTypeId = lt.LetterTypeId,
            IssuedDate = DateOnly.FromDateTime(DateTime.Today),
            GeneratedDate = DateOnly.FromDateTime(DateTime.Today),
            SignedDate = DateOnly.FromDateTime(DateTime.Today),
            ResponseStatus = "Pending"
        });
        await db.SaveChangesAsync();

        var repo = new Mock<IFileMaster>();
        repo.Setup(r => r.GetByIdAsync(fm.FileMasterId)).ReturnsAsync(fm);
        var scope = new Mock<IScopedCaseQuery>();
        scope.Setup(s => s.IsInScope(It.IsAny<FileMaster>(), It.IsAny<ClaimsPrincipal>())).Returns(true);
        var letters = new Mock<ILetterService>();
        var (sut, tempData) = BuildLetterController(
            repo.Object, db, scope.Object, letters.Object,
            new Mock<IWorkflowService>().Object, new Mock<INotificationService>().Object);

        var result = await sut.IssueLetter(fm.FileMasterId, "IssueLetter1", "User A", "RegisteredPost", DateTime.Today, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.True(tempData.ContainsKey("Error"));
        letters.Verify(l => l.IssueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IssueLetterRequest>()), Times.Never);
    }


    private static LetterIssuance PendingIssuance(Guid fileMasterId, Guid letterTypeId) => new()
    {
        LetterIssuanceId = Guid.NewGuid(),
        FileMasterId = fileMasterId,
        LetterTypeId = letterTypeId,
        IssuedDate = DateOnly.FromDateTime(DateTime.Today),
        GeneratedDate = DateOnly.FromDateTime(DateTime.Today),
        SignedDate = DateOnly.FromDateTime(DateTime.Today),
        ResponseStatus = "Pending"
    };

    [Theory]
    [InlineData("MarkUnlawfulUseFound")]
    [InlineData("MarkELUConfirmed")]
    [InlineData("CloseCase")]
    public async Task LetterIssuance_DeterminationClosing_DoesNotSetAgreedWithFindings(string action)
    {
        var db = TestDbContextFactory.Create();
        var prop = new Property { PropertyId = Guid.NewGuid() };
        db.Properties.Add(prop);
        var fm = SeedHelper.NewFileMaster(prop.PropertyId);
        db.FileMasters.Add(fm);
        var lt = new LetterType { LetterTypeId = Guid.NewGuid(), LetterName = "S35_L1", LetterDescription = "S35 Letter 1" };
        db.LetterTypes.Add(lt);
        var issuance = PendingIssuance(fm.FileMasterId, lt.LetterTypeId);
        db.LetterIssuances.Add(issuance);
        await db.SaveChangesAsync();

        var pending = db.LetterIssuances.Single(l => l.ResponseStatus == "Pending");
        pending.ResponseDate = DateOnly.FromDateTime(DateTime.Today);
        pending.ResponseStatus = "Closed";
        // AgreedWithFindings intentionally NOT set for determination actions
        await db.SaveChangesAsync();

        var saved = await db.LetterIssuances.SingleAsync();
        Assert.Null(saved.AgreedWithFindings);
        Assert.Equal("Closed", saved.ResponseStatus);
    }

    [Theory]
    [InlineData("MarkLetter1Responded")]
    [InlineData("MarkLetter1AResponded")]
    [InlineData("MarkLetter2Responded")]
    [InlineData("MarkLetter2AResponded")]
    public async Task LetterIssuance_UserResponseAgreed_SetsAgreedWithFindingsTrue(string action)
    {
        var db = TestDbContextFactory.Create();
        var prop = new Property { PropertyId = Guid.NewGuid() };
        db.Properties.Add(prop);
        var fm = SeedHelper.NewFileMaster(prop.PropertyId);
        db.FileMasters.Add(fm);
        var lt = new LetterType { LetterTypeId = Guid.NewGuid(), LetterName = "S35_L1", LetterDescription = "S35 Letter 1" };
        db.LetterTypes.Add(lt);
        db.LetterIssuances.Add(PendingIssuance(fm.FileMasterId, lt.LetterTypeId));
        await db.SaveChangesAsync();

        var pending = db.LetterIssuances.Single(l => l.ResponseStatus == "Pending");
        pending.ResponseDate = DateOnly.FromDateTime(DateTime.Today);
        pending.ResponseStatus = "Agreed";
        pending.AgreedWithFindings = true;
        await db.SaveChangesAsync();

        var saved = await db.LetterIssuances.SingleAsync();
        Assert.True(saved.AgreedWithFindings);
        Assert.Equal("Agreed", saved.ResponseStatus);
    }

    [Theory]
    [InlineData("S35_L1",      "IssueLetter1")]
    [InlineData("S35_L3",      "IssueLetter3")]
    [InlineData("S35_L4A",     "IssueLetter4A")]
    [InlineData("S35_L4_5",    "IssueLetter4_5")]
    [InlineData("S33_3a_Decl", "IssueS33_3a")]
    [InlineData("S33_3b_Decl", "IssueS33_3b")]
    [InlineData("S33_2_Decl",  "IssueS33_2")]
    public async Task OneTimeLetter_WhenAlreadyResolved_GuardDetectsExistingIssuance(string letterCode, string actionName)
    {
        // One-time letters must be blocked from re-issuance regardless of ResponseStatus.
        // The controller checks for any prior issuance before the letter-specific guards fire.
        var db = TestDbContextFactory.Create();
        var prop = new Property { PropertyId = Guid.NewGuid() };
        db.Properties.Add(prop);
        var fm = SeedHelper.NewFileMaster(prop.PropertyId);
        db.FileMasters.Add(fm);
        var lt = new LetterType
        {
            LetterTypeId = Guid.NewGuid(),
            LetterName = letterCode,
            LetterDescription = $"Test {letterCode}"
        };
        db.LetterTypes.Add(lt);
        // Resolved issuance — status is no longer "Pending"; one-time letters still block.
        db.LetterIssuances.Add(new LetterIssuance
        {
            LetterIssuanceId = Guid.NewGuid(),
            FileMasterId = fm.FileMasterId,
            LetterTypeId = lt.LetterTypeId,
            IssuedDate = DateOnly.FromDateTime(DateTime.Today),
            GeneratedDate = DateOnly.FromDateTime(DateTime.Today),
            SignedDate = DateOnly.FromDateTime(DateTime.Today),
            ResponseStatus = "Agreed"
        });
        await db.SaveChangesAsync();

        var repo = new Mock<IFileMaster>();
        repo.Setup(r => r.GetByIdAsync(fm.FileMasterId)).ReturnsAsync(fm);
        var scope = new Mock<IScopedCaseQuery>();
        scope.Setup(s => s.IsInScope(It.IsAny<FileMaster>(), It.IsAny<ClaimsPrincipal>())).Returns(true);
        var letters = new Mock<ILetterService>();
        var (sut, tempData) = BuildLetterController(
            repo.Object, db, scope.Object, letters.Object,
            new Mock<IWorkflowService>().Object, new Mock<INotificationService>().Object);

        var result = await sut.IssueLetter(fm.FileMasterId, actionName, "User A", "RegisteredPost", DateTime.Today, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.True(tempData.ContainsKey("Error"),
            $"One-time letter {letterCode} should block re-issuance even when previously resolved.");
        letters.Verify(l => l.IssueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IssueLetterRequest>()), Times.Never);
    }

    [Fact]
    public async Task IssueLetter_S33_2Decl_BlockedWhenRatesNotConfirmed()
    {
        // Guard: S33(2) declaration cannot be issued if S33_2_RatesPaidConfirmed is false.
        var db = TestDbContextFactory.Create();
        var prop = new Property { PropertyId = Guid.NewGuid() };
        db.Properties.Add(prop);
        var fm = SeedHelper.NewFileMaster(prop.PropertyId);
        fm.AssessmentTrack = "S33_2_Declaration";
        fm.S33_2_RatesPaidConfirmed = false;
        db.FileMasters.Add(fm);
        await db.SaveChangesAsync();

        var repo = new Mock<IFileMaster>();
        repo.Setup(r => r.GetByIdAsync(fm.FileMasterId)).ReturnsAsync(fm);

        var scope = new Mock<IScopedCaseQuery>();
        scope.Setup(s => s.IsInScope(It.IsAny<FileMaster>(), It.IsAny<ClaimsPrincipal>())).Returns(true);

        var letters = new Mock<ILetterService>();
        var (sut, tempData) = BuildLetterController(
            repo.Object, db, scope.Object, letters.Object,
            new Mock<IWorkflowService>().Object, new Mock<INotificationService>().Object);

        var result = await sut.IssueLetter(
            fm.FileMasterId, "IssueS33_2", "Water User A", "RegisteredPost", DateTime.Today, default);

        // Guard fired: redirect back, error set, no letter issued.
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.True(tempData.ContainsKey("Error"));
        Assert.Contains("rates paid", (string)tempData["Error"]!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(db.LetterIssuances.ToList());
        letters.Verify(l => l.IssueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IssueLetterRequest>()), Times.Never);
    }

    [Fact]
    public async Task IssueLetter_S33_2Decl_BlockedWhenNoEntitlement()
    {
        // Guard: S33(2) declaration cannot be issued if no ELU entitlement is linked.
        var db = TestDbContextFactory.Create();
        var prop = new Property { PropertyId = Guid.NewGuid() };
        db.Properties.Add(prop);
        var fm = SeedHelper.NewFileMaster(prop.PropertyId);
        fm.AssessmentTrack = "S33_2_Declaration";
        fm.S33_2_RatesPaidConfirmed = true; // rates guard passes; entitlement guard fires
        fm.EntitlementId = null;
        db.FileMasters.Add(fm);
        await db.SaveChangesAsync();

        var repo = new Mock<IFileMaster>();
        repo.Setup(r => r.GetByIdAsync(fm.FileMasterId)).ReturnsAsync(fm);
        var scope = new Mock<IScopedCaseQuery>();
        scope.Setup(s => s.IsInScope(It.IsAny<FileMaster>(), It.IsAny<ClaimsPrincipal>())).Returns(true);
        var letters = new Mock<ILetterService>();
        var (sut, tempData) = BuildLetterController(
            repo.Object, db, scope.Object, letters.Object,
            new Mock<IWorkflowService>().Object, new Mock<INotificationService>().Object);

        var result = await sut.IssueLetter(
            fm.FileMasterId, "IssueS33_2", "Water User A", "RegisteredPost", DateTime.Today, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.True(tempData.ContainsKey("Error"));
        Assert.Contains("entitlement", (string)tempData["Error"]!, StringComparison.OrdinalIgnoreCase);
        letters.Verify(l => l.IssueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IssueLetterRequest>()), Times.Never);
    }

    [Fact]
    public async Task IssueLetter_S33_2Decl_RatesConfirmed_ProceedsPastGuard()
    {
        // When S33_2_RatesPaidConfirmed is true and an Entitlement is linked, both guards must NOT fire.
        // Entity must be tracked by the same db instance used as _context so that
        // Reference().LoadAsync() can resolve the IrrigationBoard navigation.
        var db = TestDbContextFactory.Create();
        var board = new IrrigationBoard { IrrigationBoardId = Guid.NewGuid(), IrrigationBoardName = "Blyde River Board" };
        db.IrrigationBoards.Add(board);
        var ent = new Entitlement { EntitlementId = Guid.NewGuid(), Name = "S33(2) ELU", Volume = 12000m, EntitlementTypeId = Guid.NewGuid() };
        db.Entitlements.Add(ent);
        var prop = new Property { PropertyId = Guid.NewGuid() };
        db.Properties.Add(prop);
        var fm = SeedHelper.NewFileMaster(prop.PropertyId);
        fm.AssessmentTrack = "S33_2_Declaration";
        fm.S33_2_IrrigationBoardId = board.IrrigationBoardId;
        fm.S33_2_RatesPaidConfirmed = true;
        fm.EntitlementId = ent.EntitlementId;
        db.FileMasters.Add(fm);
        await db.SaveChangesAsync();

        // FindAsync returns the already-tracked entity — LoadAsync will work.
        var trackedFm = await db.FileMasters.FindAsync(fm.FileMasterId);

        var repo = new Mock<IFileMaster>();
        repo.Setup(r => r.GetByIdAsync(fm.FileMasterId)).ReturnsAsync(trackedFm);

        var scope = new Mock<IScopedCaseQuery>();
        scope.Setup(s => s.IsInScope(It.IsAny<FileMaster>(), It.IsAny<ClaimsPrincipal>())).Returns(true);

        var letters = new Mock<ILetterService>();
        letters.Setup(l => l.IssueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IssueLetterRequest>()))
               .ReturnsAsync(new LetterIssuance
               {
                   LetterIssuanceId = Guid.NewGuid(),
                   FileMasterId = fm.FileMasterId,
                   LetterTypeId = Guid.NewGuid(),
                   IssuedDate = DateOnly.FromDateTime(DateTime.Today),
                   GeneratedDate = DateOnly.FromDateTime(DateTime.Today),
                   SignedDate = DateOnly.FromDateTime(DateTime.Today),
                   ResponseStatus = "Pending"
               });

        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(w => w.TransitionToAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>()))
                .ReturnsAsync(new WorkflowInstance { WorkflowInstanceId = Guid.NewGuid(), FileMasterId = fm.FileMasterId, Status = "Active" });

        var (sut, tempData) = BuildLetterController(
            repo.Object, db, scope.Object, letters.Object, workflow.Object, new Mock<INotificationService>().Object);

        var result = await sut.IssueLetter(
            fm.FileMasterId, "IssueS33_2", "Water User A", "RegisteredPost", DateTime.Today, default);

        // Rates-paid guard did not fire — IssueAsync was called.
        Assert.IsType<RedirectToActionResult>(result);
        Assert.False(tempData.ContainsKey("Error"), "Rates-paid guard must not block when S33_2_RatesPaidConfirmed is true.");
        letters.Verify(l => l.IssueAsync(fm.FileMasterId, "S33_2_Decl", It.IsAny<IssueLetterRequest>()), Times.Once);
        // The controller must transition the workflow to S33_2_DeclarationIssued after issuance.
        // Without this, the case stays in ReadyForDeclaration and the declaration track never completes.
        workflow.Verify(
            w => w.TransitionToAsync(fm.FileMasterId, "S33_2_DeclarationIssued", It.IsAny<Guid?>(), It.IsAny<string?>()),
            Times.Once,
            "IssueLetter must call TransitionToAsync('S33_2_DeclarationIssued') after a successful IssueAsync.");
    }

    [Fact]
    public async Task IssueLetter_S33_2Decl_Idempotency_BlocksReissuanceWithCorrectMessage()
    {
        // A S33(2) Kader Asmal Declaration is a one-time legal instrument. Once issued, any
        // re-issuance attempt must be blocked by the idempotency guard — BEFORE the S33(2)-specific
        // guards fire. This test seeds a fully-valid case (rates confirmed, entitlement linked) so
        // we can be certain the block comes from idempotency, not from a downstream guard.
        var db = TestDbContextFactory.Create();
        var board = new IrrigationBoard { IrrigationBoardId = Guid.NewGuid(), IrrigationBoardName = "Blyde River Board" };
        db.IrrigationBoards.Add(board);
        var ent = new Entitlement { EntitlementId = Guid.NewGuid(), Name = "S33(2) ELU", Volume = 12000m, EntitlementTypeId = Guid.NewGuid() };
        db.Entitlements.Add(ent);
        var prop = new Property { PropertyId = Guid.NewGuid() };
        db.Properties.Add(prop);
        var fm = SeedHelper.NewFileMaster(prop.PropertyId);
        fm.AssessmentTrack = "S33_2_Declaration";
        fm.S33_2_IrrigationBoardId = board.IrrigationBoardId;
        fm.S33_2_RatesPaidConfirmed = true;
        fm.EntitlementId = ent.EntitlementId;
        db.FileMasters.Add(fm);

        // Seed the LetterType so the idempotency guard can look it up.
        var lt = new LetterType { LetterTypeId = Guid.NewGuid(), LetterName = "S33_2_Decl", LetterDescription = "S33(2) Kader Asmal Declaration" };
        db.LetterTypes.Add(lt);
        // Prior issuance (resolved — ResponseStatus not "Pending") must still block re-issuance.
        db.LetterIssuances.Add(new LetterIssuance
        {
            LetterIssuanceId = Guid.NewGuid(),
            FileMasterId = fm.FileMasterId,
            LetterTypeId = lt.LetterTypeId,
            IssuedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-30)),
            GeneratedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-30)),
            SignedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-30)),
            ResponseStatus = "Agreed"
        });
        await db.SaveChangesAsync();

        var repo = new Mock<IFileMaster>();
        repo.Setup(r => r.GetByIdAsync(fm.FileMasterId)).ReturnsAsync(fm);
        var scope = new Mock<IScopedCaseQuery>();
        scope.Setup(s => s.IsInScope(It.IsAny<FileMaster>(), It.IsAny<ClaimsPrincipal>())).Returns(true);
        var letters = new Mock<ILetterService>();
        var (sut, tempData) = BuildLetterController(
            repo.Object, db, scope.Object, letters.Object,
            new Mock<IWorkflowService>().Object, new Mock<INotificationService>().Object);

        var result = await sut.IssueLetter(
            fm.FileMasterId, "IssueS33_2", "Water User A", "RegisteredPost", DateTime.Today, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.True(tempData.ContainsKey("Error"), "Idempotency guard must set TempData[\"Error\"].");
        Assert.Contains("already been issued", (string)tempData["Error"]!, StringComparison.OrdinalIgnoreCase);
        letters.Verify(l => l.IssueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IssueLetterRequest>()), Times.Never);
    }

    [Fact]
    public void ResponseActionMap_StampsAgreement_OnlyForWaterUserResponseActions()
    {
        // ResponseActionMap is private static in FileMasterController.
        // StampsAgreement=true means the water user's reply is stamped as AgreedWithFindings=true
        // in LetterIssuance — a legal record under Section 35 of the National Water Act.
        // This test ensures no future edit can accidentally stamp agreement on a DWS determination.
        var field = typeof(FileMasterController).GetField(
            "ResponseActionMap",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);

        var map = field.GetValue(null) as Dictionary<string, (string TargetState, bool StampsAgreement)>;
        Assert.NotNull(map);

        // Exactly these 4 keys must have StampsAgreement=true (water user confirmed findings).
        var mustStampAgreement = new[]
        {
            "MarkLetter1Responded",
            "MarkLetter1AResponded",
            "MarkLetter2Responded",
            "MarkLetter2AResponded"
        };
        foreach (var key in mustStampAgreement)
        {
            Assert.True(map.ContainsKey(key), $"ResponseActionMap must contain key '{key}'.");
            Assert.True(map[key].StampsAgreement,
                $"'{key}' must have StampsAgreement=true. Changing this creates a false NWA legal record.");
        }

        // These 3 keys must have StampsAgreement=false (DWS determinations, not user agreement).
        var mustNotStampAgreement = new[]
        {
            "MarkELUConfirmed",
            "MarkUnlawfulUseFound",
            "CloseCase"
        };
        foreach (var key in mustNotStampAgreement)
        {
            Assert.True(map.ContainsKey(key), $"ResponseActionMap must contain key '{key}'.");
            Assert.False(map[key].StampsAgreement,
                $"'{key}' must have StampsAgreement=false. Stamping agreement here creates a false NWA legal record.");
        }

        // Map must have exactly 7 entries — no undocumented additions.
        Assert.Equal(7, map.Count);

        // Every key in the map must be in one of the two expected sets.
        var allExpected = mustStampAgreement.Concat(mustNotStampAgreement)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in map.Keys)
            Assert.Contains(key, allExpected);
    }

    [Fact]
    public async Task Edit_Post_SavesS33_2Fields()
    {
        // The Edit POST passes the entire model-bound FileMaster to UpdateAsync.
        // This test verifies S33_2_* fields are included in the update call.
        var db = TestDbContextFactory.Create();
        var board = new IrrigationBoard { IrrigationBoardId = Guid.NewGuid(), IrrigationBoardName = "Board A" };
        db.IrrigationBoards.Add(board);
        var prop = new Property { PropertyId = Guid.NewGuid() };
        db.Properties.Add(prop);
        var fm = SeedHelper.NewFileMaster(prop.PropertyId);
        fm.AssessmentTrack = "S33_2_Declaration";
        db.FileMasters.Add(fm);
        await db.SaveChangesAsync();

        FileMaster? capturedUpdate = null;

        var repo = new Mock<IFileMaster>();
        // GetByIdAsync used for scope check — return a fresh (untracked by db) instance to avoid EF identity conflict.
        repo.Setup(r => r.GetByIdAsync(fm.FileMasterId))
            .ReturnsAsync(new FileMaster
            {
                FileMasterId = fm.FileMasterId,
                PropertyId = prop.PropertyId,
                RegistrationNumber = fm.RegistrationNumber,
                SurveyorGeneralCode = fm.SurveyorGeneralCode,
                PrimaryCatchment = fm.PrimaryCatchment,
                QuaternaryCatchment = fm.QuaternaryCatchment,
                FarmName = fm.FarmName,
                FarmNumber = fm.FarmNumber,
                FarmPortion = fm.FarmPortion,
                RegistrationDivision = fm.RegistrationDivision,
                FileCreatedDate = fm.FileCreatedDate
            });
        repo.Setup(r => r.UpdateAsync(It.IsAny<FileMaster>()))
            .Callback<FileMaster>(f => capturedUpdate = f)
            .ReturnsAsync((FileMaster f) => f);

        var scope = new Mock<IScopedCaseQuery>();
        scope.Setup(s => s.IsInScope(It.IsAny<FileMaster>(), It.IsAny<ClaimsPrincipal>())).Returns(true);

        var (sut, _) = BuildLetterController(
            repo.Object, db, scope.Object,
            new Mock<ILetterService>().Object,
            new Mock<IWorkflowService>().Object,
            new Mock<INotificationService>().Object);

        // The POST-bound model includes S33_2 fields — as if the form was filled out.
        var postFm = SeedHelper.NewFileMaster(prop.PropertyId);
        postFm.FileMasterId = fm.FileMasterId;
        postFm.AssessmentTrack = "S33_2_Declaration";
        postFm.S33_2_IrrigationBoardId = board.IrrigationBoardId;
        postFm.S33_2_RatesPaidConfirmed = true;
        postFm.S33_2_ScheduledAreaName = "Upper Blyde Scheme";

        var result = await sut.Edit(fm.FileMasterId, postFm);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(capturedUpdate);
        Assert.Equal(board.IrrigationBoardId, capturedUpdate!.S33_2_IrrigationBoardId);
        Assert.True(capturedUpdate.S33_2_RatesPaidConfirmed);
        Assert.Equal("Upper Blyde Scheme", capturedUpdate.S33_2_ScheduledAreaName);
    }

    private static (FileMasterController controller, TempDataDictionary tempData) BuildLetterController(
        IFileMaster repo,
        ApplicationDBContext db,
        IScopedCaseQuery scope,
        ILetterService letters,
        IWorkflowService workflow,
        INotificationService notify)
    {
        var tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        var controller = new FileMasterController(
            repo, db, workflow, scope, letters,
            new Mock<ILawfulnessAssessmentService>().Object, notify)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = tempData
        };
        return (controller, tempData);
    }
}
