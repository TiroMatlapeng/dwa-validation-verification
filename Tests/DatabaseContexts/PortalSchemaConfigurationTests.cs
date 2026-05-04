using dwa_ver_val.Models.Enums;
using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace dwa_ver_val.Tests.DatabaseContexts;

public class PortalSchemaConfigurationTests
{
    [Fact]
    public void PublicUserRecoveryCodes_DbSetIsRegistered()
    {
        using var ctx = TestDbContextFactory.Create();
        Assert.NotNull(ctx.PublicUserRecoveryCodes);
    }

    [Fact]
    public void PublicUserProperty_StatusEnum_RoundTrips()
    {
        using var ctx = TestDbContextFactory.Create();

        var publicUser = PublicUserBuilder.Active("status@test.example");
        ctx.PublicUsers.Add(publicUser);
        ctx.SaveChanges();

        var prop = new Property { PropertyId = Guid.NewGuid(), SGCode = "TEST-1" };
        ctx.Properties.Add(prop);
        ctx.SaveChanges();

        var pup = new PublicUserProperty
        {
            PublicUserId = publicUser.PublicUserId,
            PropertyId = prop.PropertyId,
            Status = PropertyClaimStatus.Approved,
            EvidenceType = PropertyClaimEvidenceType.IdMatch,
            RequestedDate = DateTime.UtcNow
        };
        ctx.PublicUserProperties.Add(pup);
        ctx.SaveChanges();

        var fetched = ctx.PublicUserProperties.AsNoTracking().Single(x => x.Id == pup.Id);
        Assert.Equal(PropertyClaimStatus.Approved, fetched.Status);
        Assert.Equal(PropertyClaimEvidenceType.IdMatch, fetched.EvidenceType);
    }

    [Fact]
    public void RecoveryCode_FK_To_PublicUser_IsCascade_NotRestrict()
    {
        using var ctx = TestDbContextFactory.Create();
        var entityType = ctx.Model.FindEntityType(typeof(PublicUserRecoveryCode))!;
        var fk = entityType.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(PublicUser));
        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
    }

    [Fact]
    public void EvidenceDocument_FK_To_Document_IsSetNull_NotRestrict()
    {
        using var ctx = TestDbContextFactory.Create();
        var entityType = ctx.Model.FindEntityType(typeof(PublicUserProperty))!;
        var fk = entityType.GetForeignKeys()
            .Single(f => f.PrincipalEntityType.ClrType == typeof(Document)
                         && f.Properties.Any(p => p.Name == "EvidenceDocumentId"));
        Assert.Equal(DeleteBehavior.SetNull, fk.DeleteBehavior);
    }

    [Fact]
    public void RecipientPublicUserId_FK_To_PublicUser_IsSetNull_NotRestrict()
    {
        using var ctx = TestDbContextFactory.Create();
        var entityType = ctx.Model.FindEntityType(typeof(LetterIssuance))!;
        var fk = entityType.GetForeignKeys()
            .Single(f => f.PrincipalEntityType.ClrType == typeof(PublicUser)
                         && f.Properties.Any(p => p.Name == "RecipientPublicUserId"));
        Assert.Equal(DeleteBehavior.SetNull, fk.DeleteBehavior);
    }
}
