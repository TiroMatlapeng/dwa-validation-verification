using dwa_ver_val.Helpers;
using dwa_ver_val.Models.Enums;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Tests.Helpers;
using Xunit;

namespace dwa_ver_val.Tests.Services.Portal.Auth;

public class PublicUserPropertyAccessorTests
{
    private static FileMaster NewFileMaster(Guid propertyId) => new()
    {
        FileMasterId = Guid.NewGuid(),
        PropertyId = propertyId,
        RegistrationNumber = "REG-1",
        SurveyorGeneralCode = "SG-1",
        PrimaryCatchment = "A",
        QuaternaryCatchment = "A21A",
        FarmName = "Farm",
        FarmNumber = 1,
        RegistrationDivision = "JR",
        FarmPortion = "0"
    };

    [Fact]
    public async Task GetApprovedPropertyIdsAsync_ReturnsOnlyApprovedRows()
    {
        using var ctx = TestDbContextFactory.Create();
        var user = PublicUserBuilder.Active("a@test.example");
        ctx.PublicUsers.Add(user);

        var p1 = new Property { PropertyId = Guid.NewGuid(), SGCode = "P1" };
        var p2 = new Property { PropertyId = Guid.NewGuid(), SGCode = "P2" };
        var p3 = new Property { PropertyId = Guid.NewGuid(), SGCode = "P3" };
        ctx.Properties.AddRange(p1, p2, p3);

        ctx.PublicUserProperties.AddRange(
            new PublicUserProperty { PublicUserId = user.PublicUserId, PropertyId = p1.PropertyId, Status = PropertyClaimStatus.Approved, EvidenceType = PropertyClaimEvidenceType.IdMatch, RequestedDate = DateTime.UtcNow },
            new PublicUserProperty { PublicUserId = user.PublicUserId, PropertyId = p2.PropertyId, Status = PropertyClaimStatus.Pending,  EvidenceType = PropertyClaimEvidenceType.IdMatch, RequestedDate = DateTime.UtcNow },
            new PublicUserProperty { PublicUserId = user.PublicUserId, PropertyId = p3.PropertyId, Status = PropertyClaimStatus.Rejected, EvidenceType = PropertyClaimEvidenceType.IdMatch, RequestedDate = DateTime.UtcNow }
        );
        await ctx.SaveChangesAsync();

        var accessor = new PublicUserPropertyAccessor(ctx);
        var ids = await accessor.GetApprovedPropertyIdsAsync(user.PublicUserId, default);

        Assert.Single(ids);
        Assert.Contains(p1.PropertyId, ids);
    }

    [Fact]
    public async Task GetApprovedPropertyIdsAsync_ReturnsEmptyForUnknownUser()
    {
        using var ctx = TestDbContextFactory.Create();
        var accessor = new PublicUserPropertyAccessor(ctx);
        var ids = await accessor.GetApprovedPropertyIdsAsync(Guid.NewGuid(), default);
        Assert.Empty(ids);
    }

    [Fact]
    public async Task AssertHasAccessToFileMasterAsync_ThrowsNotFoundForNonLinkedFileMaster()
    {
        using var ctx = TestDbContextFactory.Create();
        var user = PublicUserBuilder.Active("b@test.example");
        ctx.PublicUsers.Add(user);

        var p = new Property { PropertyId = Guid.NewGuid(), SGCode = "P" };
        ctx.Properties.Add(p);

        var fm = NewFileMaster(p.PropertyId);
        ctx.FileMasters.Add(fm);
        await ctx.SaveChangesAsync();

        var accessor = new PublicUserPropertyAccessor(ctx);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            accessor.AssertHasAccessToFileMasterAsync(user.PublicUserId, fm.FileMasterId, default));
    }

    [Fact]
    public async Task AssertHasAccessToFileMasterAsync_PassesForApprovedLinkedFileMaster()
    {
        using var ctx = TestDbContextFactory.Create();
        var user = PublicUserBuilder.Active("c@test.example");
        ctx.PublicUsers.Add(user);

        var p = new Property { PropertyId = Guid.NewGuid(), SGCode = "P" };
        ctx.Properties.Add(p);

        var fm = NewFileMaster(p.PropertyId);
        ctx.FileMasters.Add(fm);

        ctx.PublicUserProperties.Add(new PublicUserProperty
        {
            PublicUserId = user.PublicUserId,
            PropertyId = p.PropertyId,
            Status = PropertyClaimStatus.Approved,
            EvidenceType = PropertyClaimEvidenceType.IdMatch,
            RequestedDate = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var accessor = new PublicUserPropertyAccessor(ctx);
        await accessor.AssertHasAccessToFileMasterAsync(user.PublicUserId, fm.FileMasterId, default);
    }

    [Fact]
    public async Task AssertHasAccessToFileMasterAsync_ThrowsForPendingLink()
    {
        using var ctx = TestDbContextFactory.Create();
        var user = PublicUserBuilder.Active("d@test.example");
        ctx.PublicUsers.Add(user);

        var p = new Property { PropertyId = Guid.NewGuid(), SGCode = "P" };
        ctx.Properties.Add(p);

        var fm = NewFileMaster(p.PropertyId);
        ctx.FileMasters.Add(fm);

        ctx.PublicUserProperties.Add(new PublicUserProperty
        {
            PublicUserId = user.PublicUserId,
            PropertyId = p.PropertyId,
            Status = PropertyClaimStatus.Pending,
            EvidenceType = PropertyClaimEvidenceType.IdMatch,
            RequestedDate = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var accessor = new PublicUserPropertyAccessor(ctx);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            accessor.AssertHasAccessToFileMasterAsync(user.PublicUserId, fm.FileMasterId, default));
    }

    [Fact]
    public async Task AssertHasAccessToFileMasterAsync_ThrowsForOtherUsersApprovedLink()
    {
        // User B has an Approved link to property P; user A must NOT inherit that access.
        using var ctx = TestDbContextFactory.Create();
        var userA = PublicUserBuilder.Active("a@cross-user.test");
        var userB = PublicUserBuilder.Active("b@cross-user.test");
        ctx.PublicUsers.AddRange(userA, userB);

        var p = new Property { PropertyId = Guid.NewGuid(), SGCode = "P" };
        ctx.Properties.Add(p);

        var fm = NewFileMaster(p.PropertyId);
        ctx.FileMasters.Add(fm);

        // Only user B is approved on the property.
        ctx.PublicUserProperties.Add(new PublicUserProperty
        {
            PublicUserId = userB.PublicUserId,
            PropertyId = p.PropertyId,
            Status = PropertyClaimStatus.Approved,
            EvidenceType = PropertyClaimEvidenceType.IdMatch,
            RequestedDate = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var accessor = new PublicUserPropertyAccessor(ctx);

        // User A — no link at all — must throw 404.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            accessor.AssertHasAccessToFileMasterAsync(userA.PublicUserId, fm.FileMasterId, default));
    }
}
