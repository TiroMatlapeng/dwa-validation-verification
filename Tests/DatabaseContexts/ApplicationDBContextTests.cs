using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Tests.DatabaseContexts;

public class ApplicationDBContextTests
{
    [Fact]
    public void Context_Creates_InMemory_Database_Successfully()
    {
        using var context = TestDbContextFactory.Create();
        Assert.NotNull(context);
    }

    [Fact]
    public void Context_Has_All_Core_DbSets()
    {
        using var context = TestDbContextFactory.Create();

        Assert.NotNull(context.Properties);
        Assert.NotNull(context.FileMasters);
        Assert.NotNull(context.Entitlements);
        Assert.NotNull(context.Validations);
        Assert.NotNull(context.Irrigations);
        Assert.NotNull(context.FieldAndCrops);
        Assert.NotNull(context.Forestations);
        Assert.NotNull(context.Storings);
        Assert.NotNull(context.DamCalculations);
    }

    [Fact]
    public void Context_Has_All_Organisational_DbSets()
    {
        using var context = TestDbContextFactory.Create();

        Assert.NotNull(context.Provinces);
        Assert.NotNull(context.WaterManagementAreas);
        Assert.NotNull(context.OrganisationalUnits);
        Assert.NotNull(context.Users);
    }

    [Fact]
    public void Context_Has_All_Workflow_DbSets()
    {
        using var context = TestDbContextFactory.Create();

        Assert.NotNull(context.WorkflowStates);
        Assert.NotNull(context.WorkflowInstances);
        Assert.NotNull(context.WorkflowStepRecords);
    }

    [Fact]
    public void Context_Has_All_Letter_And_Signature_DbSets()
    {
        using var context = TestDbContextFactory.Create();

        Assert.NotNull(context.LetterTypes);
        Assert.NotNull(context.LetterIssuances);
        Assert.NotNull(context.Documents);
        Assert.NotNull(context.DigitalSignatures);
        Assert.NotNull(context.SignatureRequests);
    }

    [Fact]
    public void Context_Has_All_Portal_DbSets()
    {
        using var context = TestDbContextFactory.Create();

        Assert.NotNull(context.PublicUsers);
        Assert.NotNull(context.PublicUserProperties);
        Assert.NotNull(context.CaseComments);
        Assert.NotNull(context.Objections);
        Assert.NotNull(context.ObjectionDocuments);
    }

    [Fact]
    public void Context_Has_All_Catchment_And_Mapbook_DbSets()
    {
        using var context = TestDbContextFactory.Create();

        Assert.NotNull(context.CatchmentAreas);
        Assert.NotNull(context.GwcaProclamationRules);
        Assert.NotNull(context.Mapbooks);
        Assert.NotNull(context.MapbookImages);
    }

    [Fact]
    public void Context_Has_Audit_And_Notification_DbSets()
    {
        using var context = TestDbContextFactory.Create();

        Assert.NotNull(context.AuditLogs);
        Assert.NotNull(context.Notifications);
    }

    [Fact]
    public async Task Can_Add_And_Retrieve_Province()
    {
        using var context = TestDbContextFactory.Create();

        var province = new Province
        {
            ProvinceId = Guid.NewGuid(),
            ProvinceName = "Gauteng",
            ProvinceCode = "GP"
        };

        context.Provinces.Add(province);
        await context.SaveChangesAsync();

        var retrieved = await context.Provinces.FindAsync(province.ProvinceId);
        Assert.NotNull(retrieved);
        Assert.Equal("Gauteng", retrieved.ProvinceName);
    }

    [Fact]
    public async Task Can_Add_And_Retrieve_WaterManagementArea()
    {
        using var context = TestDbContextFactory.Create();

        var province = new Province
        {
            ProvinceId = Guid.NewGuid(),
            ProvinceName = "Limpopo",
            ProvinceCode = "LP"
        };
        context.Provinces.Add(province);

        var wma = new WaterManagementArea
        {
            WmaId = Guid.NewGuid(),
            WmaName = "Limpopo",
            WmaCode = "A",
            ProvinceId = province.ProvinceId
        };
        context.WaterManagementAreas.Add(wma);
        await context.SaveChangesAsync();

        var retrieved = await context.WaterManagementAreas
            .Include(w => w.Province)
            .FirstAsync(w => w.WmaId == wma.WmaId);

        Assert.Equal("Limpopo", retrieved.WmaName);
        Assert.NotNull(retrieved.Province);
        Assert.Equal("Limpopo", retrieved.Province.ProvinceName);
    }

    [Fact]
    public async Task All_Delete_Behaviors_Are_Restrict_Except_Whitelisted()
    {
        // FKs that are explicitly configured with non-Restrict delete behaviour.
        // Each entry is (DependentType, PrincipalType, FKPropertyName, AllowedBehavior).
        var allowList = new HashSet<(Type, Type, string, DeleteBehavior)>
        {
            (typeof(PublicUserRecoveryCode), typeof(PublicUser), nameof(PublicUserRecoveryCode.PublicUserId), DeleteBehavior.Cascade),
            (typeof(PublicUserProperty), typeof(Document), nameof(PublicUserProperty.EvidenceDocumentId), DeleteBehavior.SetNull),
            (typeof(LetterIssuance), typeof(PublicUser), nameof(LetterIssuance.RecipientPublicUserId), DeleteBehavior.SetNull),
        };

        using var context = TestDbContextFactory.Create();

        var foreignKeys = context.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys())
            .ToList();

        Assert.NotEmpty(foreignKeys);

        foreach (var fk in foreignKeys)
        {
            var dependent = fk.DeclaringEntityType.ClrType;
            var principal = fk.PrincipalEntityType.ClrType;
            var fkProp = fk.Properties.FirstOrDefault()?.Name ?? string.Empty;

            var expectedBehavior = allowList
                .Where(a => a.Item1 == dependent && a.Item2 == principal && a.Item3 == fkProp)
                .Select(a => (DeleteBehavior?)a.Item4)
                .FirstOrDefault();

            if (expectedBehavior.HasValue)
            {
                Assert.True(
                    fk.DeleteBehavior == expectedBehavior.Value,
                    $"FK {fk.DeclaringEntityType.Name} → {fk.PrincipalEntityType.Name} ({fkProp}) " +
                    $"expected {expectedBehavior.Value} but was {fk.DeleteBehavior}");
            }
            else
            {
                Assert.True(
                    fk.DeleteBehavior == DeleteBehavior.Restrict,
                    $"FK {fk.DeclaringEntityType.Name} → {fk.PrincipalEntityType.Name} ({fkProp}) " +
                    $"should be Restrict but was {fk.DeleteBehavior}");
            }
        }
    }
}
