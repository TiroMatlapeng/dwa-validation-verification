using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Tests.Repositories;

public class PropertyRepositoryTests
{
    [Fact]
    public async Task AddAsync_Persists_To_Database()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new PropertyRepository(context);

        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            PropertyReferenceNumber = "PROP-001",
            SGCode = "T0LR00000000012300000",
            PropertySize = 250.75m
        };

        var result = await repo.AddAsync(property);

        Assert.Equal("PROP-001", result.PropertyReferenceNumber);
        Assert.Equal(1, context.Properties.Count());
    }

    [Fact]
    public async Task ListAllAsync_Returns_All_Properties()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new PropertyRepository(context);

        context.Properties.AddRange(
            new Property { PropertyId = Guid.NewGuid(), PropertySize = 100m },
            new Property { PropertyId = Guid.NewGuid(), PropertySize = 200m },
            new Property { PropertyId = Guid.NewGuid(), PropertySize = 300m }
        );
        await context.SaveChangesAsync();

        var result = await repo.ListAllAsync();

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task UpdateAsync_Modifies_Existing()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new PropertyRepository(context);

        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            PropertyReferenceNumber = "PROP-002",
            PropertySize = 100m
        };
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        property.PropertyReferenceNumber = "PROP-002-UPDATED";
        property.PropertySize = 150.5m;
        await repo.UpdateAsync(property);

        var updated = await context.Properties.FindAsync(property.PropertyId);
        Assert.Equal("PROP-002-UPDATED", updated!.PropertyReferenceNumber);
        Assert.Equal(150.5m, updated.PropertySize);
    }

    [Fact]
    public async Task DeleteAsync_Removes_From_Database()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new PropertyRepository(context);

        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            PropertyReferenceNumber = "PROP-003",
            PropertySize = 100m
        };
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var deleted = await repo.DeleteAsync(property.PropertyId);

        Assert.NotNull(deleted);
        Assert.Equal("PROP-003", deleted.PropertyReferenceNumber);
        Assert.Equal(0, context.Properties.Count());
    }

    [Fact]
    public async Task DeleteAsync_Returns_Null_For_NonExistent()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new PropertyRepository(context);

        var result = await repo.DeleteAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task ListByProvinceAsync_Filters_Correctly()
    {
        using var context = TestDbContextFactory.Create();
        var repo = new PropertyRepository(context);

        var gpAddress = new Address
        {
            AddressId = Guid.NewGuid(),
            StreetAddress = "1 Main Rd",
            SuburbName = "Sandton",
            CityName = "Johannesburg",
            Province = "Gauteng"
        };
        var lpAddress = new Address
        {
            AddressId = Guid.NewGuid(),
            StreetAddress = "2 Farm Rd",
            SuburbName = "Tzaneen",
            CityName = "Tzaneen",
            Province = "Limpopo"
        };
        context.Addresses.AddRange(gpAddress, lpAddress);

        context.Properties.AddRange(
            new Property { PropertyId = Guid.NewGuid(), PropertySize = 100m, AddressId = gpAddress.AddressId },
            new Property { PropertyId = Guid.NewGuid(), PropertySize = 200m, AddressId = gpAddress.AddressId },
            new Property { PropertyId = Guid.NewGuid(), PropertySize = 300m, AddressId = lpAddress.AddressId }
        );
        await context.SaveChangesAsync();

        var gautengProperties = await repo.ListByProvinceAsync("Gauteng");
        var limpopoProperties = await repo.ListByProvinceAsync("Limpopo");

        Assert.Equal(2, gautengProperties.Count);
        Assert.Single(limpopoProperties);
    }

    [Fact]
    public void Property_Decimal_Precision_Correct()
    {
        using var context = TestDbContextFactory.Create();

        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            PropertySize = 12345.67m,
            Longitude = -25.746111m,
            Latitude = 28.188056m
        };
        context.Properties.Add(property);
        context.SaveChanges();

        var retrieved = context.Properties.Find(property.PropertyId)!;
        Assert.Equal(12345.67m, retrieved.PropertySize);
        Assert.Equal(-25.746111m, retrieved.Longitude);
        Assert.Equal(28.188056m, retrieved.Latitude);
    }
}
