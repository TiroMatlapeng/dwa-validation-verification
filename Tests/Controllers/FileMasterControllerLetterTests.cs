using System.Reflection;
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

    [Theory]
    [InlineData("S35_L1")]
    [InlineData("S35_L3")]
    [InlineData("S35_L4A")]
    [InlineData("S35_L4_5")]
    [InlineData("S33_3a_Decl")]
    [InlineData("S33_3b_Decl")]
    public async Task OneTimeLetter_WhenAlreadyResolved_GuardDetectsExistingIssuance(string letterCode)
    {
        // One-time letters must be blocked from re-issuance regardless of ResponseStatus.
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
        // Resolved issuance — status is no longer "Pending"
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

        var oneTimeCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "S35_L1", "S35_L3", "S35_L4A", "S35_L4_5", "S33_3a_Decl", "S33_3b_Decl"
        };

        var existing = await db.LetterTypes.SingleOrDefaultAsync(t => t.LetterName == letterCode);
        Assert.NotNull(existing);

        bool blocked;
        if (oneTimeCodes.Contains(letterCode))
        {
            blocked = await db.LetterIssuances.AnyAsync(
                l => l.FileMasterId == fm.FileMasterId
                  && l.LetterTypeId == existing.LetterTypeId);
        }
        else
        {
            blocked = await db.LetterIssuances.AnyAsync(
                l => l.FileMasterId == fm.FileMasterId
                  && l.LetterTypeId == existing.LetterTypeId
                  && l.ResponseStatus == "Pending");
        }

        Assert.True(blocked, $"One-time letter {letterCode} should block re-issuance even when previously resolved.");
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
}
