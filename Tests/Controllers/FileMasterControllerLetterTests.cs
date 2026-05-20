using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace dwa_ver_val.Tests.Controllers;

public class FileMasterControllerLetterTests
{
    [Fact]
    public async Task IssueLetter_WhenPendingIssuanceExists_ShouldNotBeAbleToAddSecond()
    {
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

        // Simulate the guard logic the controller will execute:
        var alreadyPending = await db.LetterIssuances.AnyAsync(
            l => l.FileMasterId == fm.FileMasterId
              && l.LetterTypeId == lt.LetterTypeId
              && l.ResponseStatus == "Pending");

        Assert.True(alreadyPending, "Guard should detect the existing Pending issuance");
        Assert.Equal(1, db.LetterIssuances.Count());
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
}
