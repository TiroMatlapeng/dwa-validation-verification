using dwa_ver_val.Services.Calculator;
using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Tests.Services.Calculator;

public class CalculatorServiceTests
{
    [Fact]
    public async Task ComputeSapwat_SetsCalculationResult()
    {
        // Arrange
        var db = TestDbContextFactory.Create();

        var crop = new Crop { CropId = Guid.NewGuid(), CropName = "Maize" };
        var waterSource = new WaterSource { WaterSourceId = Guid.NewGuid(), WaterSourceName = "River" };
        var period = new Period { PeriodId = Guid.NewGuid(), PeriodName = "Qualifying" };
        var property = new Property { PropertyId = Guid.NewGuid(), PropertyReferenceNumber = "P-SAPWAT", SGCode = "SG-SAPWAT" };

        db.Crops.Add(crop);
        db.WaterSources.Add(waterSource);
        db.Periods.Add(period);
        db.Properties.Add(property);

        db.CropWaterRates.Add(new CropWaterRate
        {
            CropWaterRateId = Guid.NewGuid(),
            CropId = crop.CropId,
            IrrigationSystemId = null,
            RatePerHaPerAnnum = 550m
        });

        var fc = new FieldAndCrop
        {
            FieldAndCropId = Guid.NewGuid(),
            Property = property,
            PropertyId = property.PropertyId,
            Period = period,
            PeriodId = period.PeriodId,
            FieldArea = 5m,
            Crop = crop,
            WaterSource = waterSource,
            IrrigationSystem = null,
            CropArea = 4m,
            RotationFactor = 0.75m,
            SAPWATCalculationResult = 0m
        };
        db.FieldAndCrops.Add(fc);
        await db.SaveChangesAsync();

        var sut = new CalculatorService(db);

        // Act
        var result = await sut.ComputeSapwatAsync(fc.FieldAndCropId);

        // Assert
        Assert.Equal(412.5m, result);

        var reloaded = await db.FieldAndCrops.FirstAsync(f => f.FieldAndCropId == fc.FieldAndCropId);
        Assert.Equal(412.5m, reloaded.SAPWATCalculationResult);
    }

    [Fact]
    public async Task ComputeDamVolume_Method2_SetsCapacity()
    {
        // Arrange
        var db = TestDbContextFactory.Create();

        var river = new River { RiverId = Guid.NewGuid(), RiverName = "Test River" };
        var property = new Property { PropertyId = Guid.NewGuid(), PropertyReferenceNumber = "P-DAM", SGCode = "SG-DAM" };
        db.Rivers.Add(river);
        db.Properties.Add(property);

        var dam = new DamCalculation
        {
            DamCalculationId = Guid.NewGuid(),
            CalculationDate = new DateOnly(2026, 5, 19),
            Property = property,
            PropertyId = property.PropertyId,
            SateliteSurveyDate = new DateOnly(1998, 6, 1),
            River = river,
            RiverId = river.RiverId,
            CalculationMethod = "Method2",
            DamArea = 2m,
            DamDepth = 3m,
            ShapeFactor = 0.5m,
            DamCapacity = 0m
        };
        db.DamCalculations.Add(dam);
        await db.SaveChangesAsync();

        var sut = new CalculatorService(db);

        // Act
        var result = await sut.ComputeDamVolumeAsync(dam.DamCalculationId);

        // Assert: 2 ha × 3 m × 0.5 × 1000 = 3000 m³
        Assert.Equal(3000m, result);

        var reloaded = await db.DamCalculations.FirstAsync(d => d.DamCalculationId == dam.DamCalculationId);
        Assert.Equal(3000m, reloaded.DamCapacity);
    }

    [Fact]
    public async Task ComputeSfra_UpdatesForestationFields()
    {
        // Arrange
        var db = TestDbContextFactory.Create();

        var property = new Property { PropertyId = Guid.NewGuid(), PropertyReferenceNumber = "P-SFRA", SGCode = "SG-SFRA" };
        db.Properties.Add(property);

        db.SfraSpeciesRates.Add(new SfraSpeciesRate
        {
            SfraSpeciesRateId = Guid.NewGuid(),
            SpeciesName = "Pine",
            RateM3PerHaPerAnnum = 5500m
        });

        var forestation = new Forestation
        {
            ForestationId = Guid.NewGuid(),
            PropertyId = property.PropertyId,
            Property = property,
            Specie = "Pine",
            Pre1972Hectares = 0m,
            SFRAPermitHectares = 8m,
            QualifyPeriodSFRAHectares = 12m,
            RegisteredHectares = 0m,
            RegisteredVolume = 0m
        };
        db.Forestations.Add(forestation);
        await db.SaveChangesAsync();

        var sut = new CalculatorService(db);

        // Act
        var result = await sut.ComputeSfraAsync(forestation.ForestationId);

        // Assert: authorisedHa = min(0 + 8, 12) = 8; unlawful = 12 - 8 = 4; eluVolume = 8 × 5500 = 44000
        Assert.Equal(8m, result.EluHa);
        Assert.Equal(44_000m, result.EluVolume);
        Assert.Equal(4m, result.UnlawfulHa);

        var reloaded = await db.Forestations.FirstAsync(f => f.ForestationId == forestation.ForestationId);
        Assert.Equal(8m, reloaded.ELUHectares);
        Assert.Equal(44_000m, reloaded.ELUVolume);
        Assert.Equal(4m, reloaded.UnlawfulHectares);
    }
}
