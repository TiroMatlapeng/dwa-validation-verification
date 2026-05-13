using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Tests.Repositories;

public class FieldAndCropRepositoryTests
{
    private static (Property property, Period period, Crop crop, WaterSource waterSource) SeedLookups(
        ApplicationDBContext context,
        string propertyRef = "PROP-FC-001")
    {
        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            PropertyReferenceNumber = propertyRef,
            PropertySize = 100m
        };
        var period = new Period
        {
            PeriodId = Guid.NewGuid(),
            PeriodName = "Qualifying Period"
        };
        var crop = new Crop
        {
            CropId = Guid.NewGuid(),
            CropName = "Maize"
        };
        var waterSource = new WaterSource
        {
            WaterSourceId = Guid.NewGuid(),
            WaterSourceName = "River"
        };

        context.Properties.Add(property);
        context.Periods.Add(period);
        context.Crops.Add(crop);
        context.WaterSources.Add(waterSource);
        context.SaveChanges();

        return (property, period, crop, waterSource);
    }

    private static FieldAndCrop BuildFieldAndCrop(
        Property property,
        Period period,
        Crop crop,
        WaterSource waterSource,
        string? fieldNumber = null)
    {
        return new FieldAndCrop
        {
            FieldAndCropId = Guid.NewGuid(),
            Property = property,
            PropertyId = property.PropertyId,
            Period = period,
            PeriodId = period.PeriodId,
            Crop = crop,
            WaterSource = waterSource,
            FieldNumber = fieldNumber,
            FieldArea = 10.0m,
            RotationFactor = 0.5m,
            CropArea = 8.0m,
            SAPWATCalculationResult = 500.0m
        };
    }

    [Fact]
    public async Task AddFieldAndCrop_Persists_To_Database()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new FieldAndCropRepository(context);
        var (property, period, crop, waterSource) = SeedLookups(context);

        var entity = BuildFieldAndCrop(property, period, crop, waterSource, "F-01");

        var result = await repo.AddFieldAndCrop(entity);

        Assert.Equal(entity.FieldAndCropId, result.FieldAndCropId);
        Assert.Equal(1, context.FieldAndCrops.Count());
        var persisted = await context.FieldAndCrops.FindAsync(entity.FieldAndCropId);
        Assert.NotNull(persisted);
        Assert.Equal("F-01", persisted!.FieldNumber);
        Assert.Equal(10.0m, persisted.FieldArea);
        Assert.Equal(500.0m, persisted.SAPWATCalculationResult);
    }

    [Fact]
    public async Task GetByPropertyIdAsync_Returns_Only_Matching_Property()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new FieldAndCropRepository(context);

        var (propertyA, period, crop, waterSource) = SeedLookups(context, "PROP-FC-A");
        var propertyB = new Property
        {
            PropertyId = Guid.NewGuid(),
            PropertyReferenceNumber = "PROP-FC-B",
            PropertySize = 200m
        };
        context.Properties.Add(propertyB);
        await context.SaveChangesAsync();

        var entityForA1 = BuildFieldAndCrop(propertyA, period, crop, waterSource, "A-1");
        var entityForA2 = BuildFieldAndCrop(propertyA, period, crop, waterSource, "A-2");
        var entityForB = BuildFieldAndCrop(propertyB, period, crop, waterSource, "B-1");

        context.FieldAndCrops.AddRange(entityForA1, entityForA2, entityForB);
        await context.SaveChangesAsync();

        var resultsForA = await repo.GetByPropertyIdAsync(propertyA.PropertyId);
        var resultsForB = await repo.GetByPropertyIdAsync(propertyB.PropertyId);

        Assert.Equal(2, resultsForA.Count);
        Assert.All(resultsForA, fc => Assert.Equal(propertyA.PropertyId, fc.PropertyId));
        Assert.Single(resultsForB);
        Assert.Equal("B-1", resultsForB.Single().FieldNumber);
    }

    [Fact]
    public async Task DeleteAsync_Removes_Entity()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new FieldAndCropRepository(context);
        var (property, period, crop, waterSource) = SeedLookups(context);

        var entity = BuildFieldAndCrop(property, period, crop, waterSource, "F-DEL");
        context.FieldAndCrops.Add(entity);
        await context.SaveChangesAsync();
        Assert.Equal(1, context.FieldAndCrops.Count());

        await repo.DeleteAsync(entity.FieldAndCropId);

        Assert.Equal(0, context.FieldAndCrops.Count());
        var deleted = await context.FieldAndCrops.FindAsync(entity.FieldAndCropId);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task GetByIdAsync_Returns_Correct_Entity()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new FieldAndCropRepository(context);
        var (property, period, crop, waterSource) = SeedLookups(context);

        var entity = BuildFieldAndCrop(property, period, crop, waterSource, "F-ID");
        context.FieldAndCrops.Add(entity);
        await context.SaveChangesAsync();

        var found = await repo.GetByIdAsync(entity.FieldAndCropId);

        Assert.NotNull(found);
        Assert.Equal(entity.FieldAndCropId, found!.FieldAndCropId);
        Assert.Equal("F-ID", found.FieldNumber);
    }
}
