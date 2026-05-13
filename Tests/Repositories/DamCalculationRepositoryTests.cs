using dwa_ver_val.Tests.Helpers;

namespace dwa_ver_val.Tests.Repositories;

public class DamCalculationRepositoryTests
{
    private static (Property property, River river) SeedLookups(ApplicationDBContext context, string riverName = "Olifants")
    {
        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            PropertySize = 100m
        };
        var river = new River
        {
            RiverId = Guid.NewGuid(),
            RiverName = riverName
        };
        context.Properties.Add(property);
        context.Rivers.Add(river);
        context.SaveChanges();
        return (property, river);
    }

    private static DamCalculation BuildDam(Property property, River river, decimal capacity = 45000m, string damNumber = "D01")
    {
        return new DamCalculation
        {
            DamCalculationId = Guid.NewGuid(),
            Property = property,
            PropertyId = property.PropertyId,
            River = river,
            RiverId = river.RiverId,
            CalculationDate = DateOnly.FromDateTime(DateTime.Today),
            SateliteSurveyDate = new DateOnly(1997, 6, 15),
            DamNumber = damNumber,
            DamCapacity = capacity,
            DamCalculationStatus = DamCalculationStatus.IN_PROGRESS
        };
    }

    [Fact]
    public async Task AddCalculationAsync_Persists_To_Database()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new DamCalculationRepository(context);
        var (property, river) = SeedLookups(context);

        var dam = BuildDam(property, river, capacity: 45000.0m, damNumber: "D01");

        var result = await repo.AddCalculationAsync(dam);

        Assert.NotNull(result);
        Assert.Equal(1, context.DamCalculations.Count());
        Assert.Equal(45000.0m, result.DamCapacity);
        Assert.Equal("D01", result.DamNumber);
    }

    [Fact]
    public async Task GetByPropertyIdAsync_Returns_Only_Matching_Property()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new DamCalculationRepository(context);

        var (propertyA, riverA) = SeedLookups(context, "Vaal");
        var propertyB = new Property { PropertyId = Guid.NewGuid(), PropertySize = 75m };
        context.Properties.Add(propertyB);
        await context.SaveChangesAsync();

        context.DamCalculations.AddRange(
            BuildDam(propertyA, riverA, capacity: 10_000m, damNumber: "A1"),
            BuildDam(propertyA, riverA, capacity: 20_000m, damNumber: "A2"),
            BuildDam(propertyB, riverA, capacity: 30_000m, damNumber: "B1")
        );
        await context.SaveChangesAsync();

        var resultsForA = await repo.GetByPropertyIdAsync(propertyA.PropertyId);
        var resultsForB = await repo.GetByPropertyIdAsync(propertyB.PropertyId);

        Assert.Equal(2, resultsForA.Count);
        Assert.All(resultsForA, d => Assert.Equal(propertyA.PropertyId, d.PropertyId));
        Assert.Single(resultsForB);
        Assert.Equal("B1", resultsForB.Single().DamNumber);
    }

    [Fact]
    public async Task DeleteAsync_Removes_Entity()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new DamCalculationRepository(context);
        var (property, river) = SeedLookups(context);

        var dam = BuildDam(property, river, capacity: 12_345m, damNumber: "DEL-1");
        context.DamCalculations.Add(dam);
        await context.SaveChangesAsync();
        Assert.Equal(1, context.DamCalculations.Count());

        await repo.DeleteAsync(dam.DamCalculationId);

        Assert.Equal(0, context.DamCalculations.Count());
        Assert.Null(await context.DamCalculations.FindAsync(dam.DamCalculationId));
    }

    [Fact]
    public async Task GetByIdAsync_Returns_Matching_Entity()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new DamCalculationRepository(context);
        var (property, river) = SeedLookups(context);

        var dam = BuildDam(property, river, capacity: 88_000m, damNumber: "FIND-1");
        context.DamCalculations.Add(dam);
        await context.SaveChangesAsync();

        var found = await repo.GetByIdAsync(dam.DamCalculationId);

        Assert.NotNull(found);
        Assert.Equal(dam.DamCalculationId, found!.DamCalculationId);
        Assert.Equal(88_000m, found.DamCapacity);
        Assert.Equal("FIND-1", found.DamNumber);
    }
}
