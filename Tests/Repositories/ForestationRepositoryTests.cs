using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Tests.Repositories;

public class ForestationRepositoryTests
{
    private static Property SeedProperty(ApplicationDBContext context, decimal size = 100m)
    {
        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            PropertySize = size
        };
        context.Properties.Add(property);
        context.SaveChanges();
        return property;
    }

    [Fact]
    public async Task RegisterForestation_Persists_To_Database()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new ForestationRepository(context);
        var property = SeedProperty(context);

        var entity = new Forestation
        {
            ForestationId = Guid.NewGuid(),
            Property = property,
            PropertyId = property.PropertyId,
            RegisteredHectares = 25.0m,
            RegisteredVolume = 12500.0m,
            Specie = "Pinus"
        };

        var result = await repo.RegisterForestation(entity);

        Assert.NotNull(result);
        Assert.Equal(1, context.Forestations.Count());
        Assert.Equal("Pinus", result.Specie);
        Assert.Equal(property.PropertyId, result.PropertyId);
    }

    [Fact]
    public async Task GetByPropertyIdAsync_Returns_Only_Matching_Property()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new ForestationRepository(context);

        var propertyA = SeedProperty(context, 100m);
        var propertyB = SeedProperty(context, 200m);

        context.Forestations.AddRange(
            new Forestation
            {
                ForestationId = Guid.NewGuid(),
                Property = propertyA,
                PropertyId = propertyA.PropertyId,
                RegisteredHectares = 10m,
                RegisteredVolume = 5000m,
                Specie = "Pinus"
            },
            new Forestation
            {
                ForestationId = Guid.NewGuid(),
                Property = propertyA,
                PropertyId = propertyA.PropertyId,
                RegisteredHectares = 15m,
                RegisteredVolume = 7500m,
                Specie = "Eucalyptus"
            },
            new Forestation
            {
                ForestationId = Guid.NewGuid(),
                Property = propertyB,
                PropertyId = propertyB.PropertyId,
                RegisteredHectares = 20m,
                RegisteredVolume = 10000m,
                Specie = "Acacia"
            }
        );
        await context.SaveChangesAsync();

        var resultsA = await repo.GetByPropertyIdAsync(propertyA.PropertyId);
        var resultsB = await repo.GetByPropertyIdAsync(propertyB.PropertyId);

        Assert.Equal(2, resultsA.Count);
        Assert.All(resultsA, f => Assert.Equal(propertyA.PropertyId, f.PropertyId));
        Assert.Single(resultsB);
        Assert.Equal("Acacia", resultsB.First().Specie);
    }

    [Fact]
    public async Task DeleteAsync_Removes_Entity()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new ForestationRepository(context);
        var property = SeedProperty(context);

        var entity = new Forestation
        {
            ForestationId = Guid.NewGuid(),
            Property = property,
            PropertyId = property.PropertyId,
            RegisteredHectares = 30m,
            RegisteredVolume = 15000m,
            Specie = "Pinus"
        };
        context.Forestations.Add(entity);
        await context.SaveChangesAsync();
        Assert.Equal(1, context.Forestations.Count());

        await repo.DeleteAsync(entity.ForestationId);

        Assert.Equal(0, context.Forestations.Count());
        var found = await context.Forestations.FindAsync(entity.ForestationId);
        Assert.Null(found);
    }
}
