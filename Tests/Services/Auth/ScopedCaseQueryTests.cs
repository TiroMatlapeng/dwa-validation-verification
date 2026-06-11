using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace dwa_ver_val.Tests.Services.Auth;

public class ScopedCaseQueryTests
{
    private static ApplicationDBContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDBContext(options);
    }

    private static ClaimsPrincipal UserWithRoleAndOrgUnit(string role, Guid? orgUnitId, Guid? wmaId = null)
    {
        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Role, role));
        identity.AddClaim(new Claim("orgUnitId", orgUnitId?.ToString() ?? string.Empty));
        identity.AddClaim(new Claim("wmaId", wmaId?.ToString() ?? string.Empty));
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Builds a principal whose claims mirror what DwsClaimsTransformation stamps:
    /// catchmentId, wmaId, provinceId — each empty string when null.
    /// </summary>
    private static ClaimsPrincipal MakeUser(
        string role,
        Guid? catchmentId = null,
        Guid? wmaId = null,
        Guid? provinceId = null)
    {
        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Role, role));
        identity.AddClaim(new Claim("catchmentId", catchmentId?.ToString() ?? string.Empty));
        identity.AddClaim(new Claim("wmaId", wmaId?.ToString() ?? string.Empty));
        identity.AddClaim(new Claim("provinceId", provinceId?.ToString() ?? string.Empty));
        return new ClaimsPrincipal(identity);
    }

    private static FileMaster CreateTestFileMaster(Guid propertyId, string fileNumber)
    {
        return new FileMaster
        {
            FileMasterId = Guid.NewGuid(),
            PropertyId = propertyId,
            FileNumber = fileNumber,
            RegistrationNumber = "N/A",
            SurveyorGeneralCode = "N/A",
            PrimaryCatchment = "N/A",
            QuaternaryCatchment = "N/A",
            FarmName = "N/A",
            FarmNumber = 0,
            RegistrationDivision = "N/A",
            FarmPortion = "N/A"
        };
    }

    [Fact]
    public async Task FilterFileMasters_Validator_SeesOnlyOwnWmaCases()
    {
        using var db = CreateDb();
        var limpopoWma = Guid.NewGuid();
        var mpumalangaWma = Guid.NewGuid();

        db.Properties.AddRange(
            new Property { PropertyId = Guid.NewGuid(), SGCode = "LIM-01", WmaId = limpopoWma },
            new Property { PropertyId = Guid.NewGuid(), SGCode = "MP-01", WmaId = mpumalangaWma });
        await db.SaveChangesAsync();

        var limpopoProperty = db.Properties.First(p => p.WmaId == limpopoWma);
        var mpumalangaProperty = db.Properties.First(p => p.WmaId == mpumalangaWma);

        db.FileMasters.AddRange(
            CreateTestFileMaster(limpopoProperty.PropertyId, "LIM-0001"),
            CreateTestFileMaster(mpumalangaProperty.PropertyId, "MP-0001"));
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var user = UserWithRoleAndOrgUnit(DwsRoles.Validator, orgUnitId: Guid.NewGuid(), wmaId: limpopoWma);

        var result = await sut.FilterFileMasters(db.FileMasters, user).ToListAsync();

        Assert.Single(result);
        Assert.Equal("LIM-0001", result[0].FileNumber);
    }

    [Fact]
    public async Task FilterFileMasters_NationalManager_SeesAllCases()
    {
        using var db = CreateDb();
        var wmaA = Guid.NewGuid();
        var wmaB = Guid.NewGuid();
        var pA = new Property { PropertyId = Guid.NewGuid(), SGCode = "A", WmaId = wmaA };
        var pB = new Property { PropertyId = Guid.NewGuid(), SGCode = "B", WmaId = wmaB };
        db.Properties.AddRange(pA, pB);
        db.FileMasters.AddRange(
            CreateTestFileMaster(pA.PropertyId, "A"),
            CreateTestFileMaster(pB.PropertyId, "B"));
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var user = UserWithRoleAndOrgUnit(DwsRoles.NationalManager, orgUnitId: null, wmaId: null);

        var result = await sut.FilterFileMasters(db.FileMasters, user).ToListAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task IsInScope_Validator_OutOfWmaCase_ReturnsFalse()
    {
        using var db = CreateDb();
        var limpopoWma = Guid.NewGuid();
        var mpumalangaWma = Guid.NewGuid();
        var mpumalangaProperty = new Property { PropertyId = Guid.NewGuid(), SGCode = "MP-01", WmaId = mpumalangaWma };
        db.Properties.Add(mpumalangaProperty);
        var fm = CreateTestFileMaster(mpumalangaProperty.PropertyId, "MP-0001");
        db.FileMasters.Add(fm);
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var limpopoValidator = UserWithRoleAndOrgUnit(DwsRoles.Validator, orgUnitId: Guid.NewGuid(), wmaId: limpopoWma);

        Assert.False(sut.IsInScope(fm, limpopoValidator));
    }

    [Fact]
    public async Task IsInScope_Validator_InWmaCase_ReturnsTrue()
    {
        using var db = CreateDb();
        var limpopoWma = Guid.NewGuid();
        var property = new Property { PropertyId = Guid.NewGuid(), SGCode = "LIM-01", WmaId = limpopoWma };
        db.Properties.Add(property);
        var fm = CreateTestFileMaster(property.PropertyId, "LIM-0001");
        db.FileMasters.Add(fm);
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var validator = UserWithRoleAndOrgUnit(DwsRoles.Validator, orgUnitId: Guid.NewGuid(), wmaId: limpopoWma);

        Assert.True(sut.IsInScope(fm, validator));
    }

    [Fact]
    public async Task IsInScope_NationalManager_AnyCase_ReturnsTrue()
    {
        using var db = CreateDb();
        var property = new Property { PropertyId = Guid.NewGuid(), SGCode = "X", WmaId = Guid.NewGuid() };
        db.Properties.Add(property);
        var fm = CreateTestFileMaster(property.PropertyId, "X");
        db.FileMasters.Add(fm);
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var national = UserWithRoleAndOrgUnit(DwsRoles.NationalManager, orgUnitId: null, wmaId: null);

        Assert.True(sut.IsInScope(fm, national));
    }

    [Fact]
    public async Task IsInScope_SystemAdmin_AnyCase_ReturnsTrue()
    {
        using var db = CreateDb();
        var property = new Property { PropertyId = Guid.NewGuid(), SGCode = "Y", WmaId = Guid.NewGuid() };
        db.Properties.Add(property);
        var fm = CreateTestFileMaster(property.PropertyId, "Y");
        db.FileMasters.Add(fm);
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var admin = UserWithRoleAndOrgUnit(DwsRoles.SystemAdmin, orgUnitId: null, wmaId: null);

        Assert.True(sut.IsInScope(fm, admin));
    }

    // ─── AUTH-01: narrowest-scope enforcement ──────────────────────────────────

    // Helper: seed Province → WMA → CatchmentArea → Property → FileMaster
    private static (Guid provinceId, Guid wmaId, Guid catchmentId, Property prop, FileMaster fm)
        SeedHierarchy(ApplicationDBContext db, string suffix)
    {
        var provinceId = Guid.NewGuid();
        var wmaId = Guid.NewGuid();
        var catchmentId = Guid.NewGuid();

        db.Provinces.Add(new Province
        {
            ProvinceId = provinceId,
            ProvinceName = $"Province-{suffix}",
            ProvinceCode = suffix
        });

        db.WaterManagementAreas.Add(new WaterManagementArea
        {
            WmaId = wmaId,
            WmaName = $"WMA-{suffix}",
            WmaCode = suffix,
            ProvinceId = provinceId
        });

        db.CatchmentAreas.Add(new CatchmentArea
        {
            CatchmentAreaId = catchmentId,
            CatchmentCode = $"C{suffix}",
            CatchmentName = $"Catchment-{suffix}",
            WmaId = wmaId
        });

        var prop = new Property
        {
            PropertyId = Guid.NewGuid(),
            SGCode = $"SG-{suffix}",
            WmaId = wmaId,
            CatchmentAreaId = catchmentId
        };
        db.Properties.Add(prop);

        var fm = CreateTestFileMaster(prop.PropertyId, $"FM-{suffix}");
        db.FileMasters.Add(fm);

        return (provinceId, wmaId, catchmentId, prop, fm);
    }

    // ── FilterFileMasters ──────────────────────────────────────────────────────

    [Fact]
    public async Task FilterFileMasters_CatchmentScopedUser_SeesOnlyOwnCatchment()
    {
        using var db = CreateDb();
        var (_, wmaId, catchA, _, _) = SeedHierarchy(db, "A");
        // Same WMA, different catchment:
        var catchB = Guid.NewGuid();
        db.CatchmentAreas.Add(new CatchmentArea
        {
            CatchmentAreaId = catchB,
            CatchmentCode = "CB", CatchmentName = "Catchment-B", WmaId = wmaId
        });
        var propB = new Property { PropertyId = Guid.NewGuid(), SGCode = "SG-B", WmaId = wmaId, CatchmentAreaId = catchB };
        db.Properties.Add(propB);
        db.FileMasters.Add(CreateTestFileMaster(propB.PropertyId, "FM-B"));
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var user = MakeUser(DwsRoles.Validator, catchmentId: catchA, wmaId: wmaId);

        var result = await sut.FilterFileMasters(db.FileMasters, user).ToListAsync();

        Assert.Single(result);
        Assert.Equal("FM-A", result[0].FileNumber);
    }

    [Fact]
    public async Task FilterFileMasters_WmaScopedUser_SeesAllCatchmentsInWma()
    {
        using var db = CreateDb();
        var (_, wmaA, _, _, _) = SeedHierarchy(db, "A");
        var (_, _, _, _, _) = SeedHierarchy(db, "X"); // different WMA entirely
        // Second catchment in wmaA:
        var catchA2 = Guid.NewGuid();
        db.CatchmentAreas.Add(new CatchmentArea
        {
            CatchmentAreaId = catchA2,
            CatchmentCode = "CA2", CatchmentName = "Catchment-A2", WmaId = wmaA
        });
        var propA2 = new Property { PropertyId = Guid.NewGuid(), SGCode = "SG-A2", WmaId = wmaA, CatchmentAreaId = catchA2 };
        db.Properties.Add(propA2);
        db.FileMasters.Add(CreateTestFileMaster(propA2.PropertyId, "FM-A2"));
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var user = MakeUser(DwsRoles.RegionalManager, wmaId: wmaA);

        var result = await sut.FilterFileMasters(db.FileMasters, user).ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.StartsWith("FM-A", r.FileNumber));
    }

    [Fact]
    public async Task FilterFileMasters_ProvinceScopedUser_SeesAllWmasInProvince_NotOtherProvinces()
    {
        using var db = CreateDb();
        var (provA, _, _, _, _) = SeedHierarchy(db, "A");
        var (_, _, _, _, _) = SeedHierarchy(db, "X"); // different province
        // Second WMA in provA:
        var wmaA2 = Guid.NewGuid();
        db.WaterManagementAreas.Add(new WaterManagementArea
        {
            WmaId = wmaA2, WmaName = "WMA-A2", WmaCode = "A2", ProvinceId = provA
        });
        var catchA2 = Guid.NewGuid();
        db.CatchmentAreas.Add(new CatchmentArea
        {
            CatchmentAreaId = catchA2, CatchmentCode = "CA2", CatchmentName = "Catchment-A2", WmaId = wmaA2
        });
        var propA2 = new Property { PropertyId = Guid.NewGuid(), SGCode = "SG-A2", WmaId = wmaA2, CatchmentAreaId = catchA2 };
        db.Properties.Add(propA2);
        db.FileMasters.Add(CreateTestFileMaster(propA2.PropertyId, "FM-A2"));
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var user = MakeUser(DwsRoles.RegionalManager, provinceId: provA);

        var result = await sut.FilterFileMasters(db.FileMasters, user).ToListAsync();

        Assert.Equal(2, result.Count);
        // Both cases must be in province A — neither is from province X
        Assert.All(result, r => Assert.DoesNotMatch("FM-X", r.FileNumber!));
    }

    [Fact]
    public async Task FilterFileMasters_NoScopeUser_SeesNothing()
    {
        using var db = CreateDb();
        SeedHierarchy(db, "A");
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var user = MakeUser(DwsRoles.Validator); // no catchment, wma or province claims

        var result = await sut.FilterFileMasters(db.FileMasters, user).ToListAsync();

        Assert.Empty(result);
    }

    // ── FilterProperties ───────────────────────────────────────────────────────

    [Fact]
    public async Task FilterProperties_CatchmentScopedUser_SeesOnlyOwnCatchment()
    {
        using var db = CreateDb();
        var (_, wmaId, catchA, _, _) = SeedHierarchy(db, "A");
        var catchB = Guid.NewGuid();
        db.CatchmentAreas.Add(new CatchmentArea
        {
            CatchmentAreaId = catchB, CatchmentCode = "CB", CatchmentName = "B", WmaId = wmaId
        });
        db.Properties.Add(new Property
        {
            PropertyId = Guid.NewGuid(), SGCode = "SG-B", WmaId = wmaId, CatchmentAreaId = catchB
        });
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var user = MakeUser(DwsRoles.Validator, catchmentId: catchA, wmaId: wmaId);

        var result = await sut.FilterProperties(db.Properties, user).ToListAsync();

        Assert.Single(result);
        Assert.Equal("SG-A", result[0].SGCode);
    }

    [Fact]
    public async Task FilterProperties_ProvinceScopedUser_SeesAllPropertiesInProvince()
    {
        using var db = CreateDb();
        var (provA, _, _, _, _) = SeedHierarchy(db, "A");
        SeedHierarchy(db, "Z"); // different province
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var user = MakeUser(DwsRoles.Validator, provinceId: provA);

        var result = await sut.FilterProperties(db.Properties, user).ToListAsync();

        Assert.Single(result);
        Assert.Equal("SG-A", result[0].SGCode);
    }

    // ── IsInScope ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task IsInScope_CatchmentScopedUser_InCatchment_ReturnsTrue()
    {
        using var db = CreateDb();
        var (_, _, catchA, prop, fm) = SeedHierarchy(db, "A");
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var user = MakeUser(DwsRoles.Validator, catchmentId: catchA);

        Assert.True(sut.IsInScope(fm, user));
    }

    [Fact]
    public async Task IsInScope_CatchmentScopedUser_OutOfCatchment_ReturnsFalse()
    {
        using var db = CreateDb();
        var (_, wmaA, catchA, propA, fmA) = SeedHierarchy(db, "A");
        // Second catchment in same WMA:
        var catchB = Guid.NewGuid();
        db.CatchmentAreas.Add(new CatchmentArea
        {
            CatchmentAreaId = catchB, CatchmentCode = "CB", CatchmentName = "B", WmaId = wmaA
        });
        var propB = new Property { PropertyId = Guid.NewGuid(), SGCode = "B", WmaId = wmaA, CatchmentAreaId = catchB };
        db.Properties.Add(propB);
        var fmB = CreateTestFileMaster(propB.PropertyId, "FM-B");
        db.FileMasters.Add(fmB);
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        // User is scoped to catchA only — fmB lives in catchB
        var user = MakeUser(DwsRoles.Validator, catchmentId: catchA, wmaId: wmaA);

        Assert.False(sut.IsInScope(fmB, user));
    }

    [Fact]
    public async Task IsInScope_ProvinceScopedUser_InProvince_ReturnsTrue()
    {
        using var db = CreateDb();
        var (provA, _, _, _, fm) = SeedHierarchy(db, "A");
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var user = MakeUser(DwsRoles.RegionalManager, provinceId: provA);

        Assert.True(sut.IsInScope(fm, user));
    }

    [Fact]
    public async Task IsInScope_ProvinceScopedUser_OutOfProvince_ReturnsFalse()
    {
        using var db = CreateDb();
        var (_, _, _, _, fmA) = SeedHierarchy(db, "A");
        var (provX, _, _, _, _) = SeedHierarchy(db, "X");
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        // User is scoped to province X — fmA lives in province A
        var user = MakeUser(DwsRoles.RegionalManager, provinceId: provX);

        Assert.False(sut.IsInScope(fmA, user));
    }

    // ── FilterWaterManagementAreas ─────────────────────────────────────────────

    [Fact]
    public async Task FilterWaterManagementAreas_NationalManager_SeesAll()
    {
        using var db = CreateDb();
        var (_, _, _, _, _) = SeedHierarchy(db, "A");
        var (_, _, _, _, _) = SeedHierarchy(db, "B");
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var user = MakeUser(DwsRoles.NationalManager);

        var result = await sut.FilterWaterManagementAreas(db.WaterManagementAreas, user).ToListAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task FilterWaterManagementAreas_SystemAdmin_SeesAll()
    {
        using var db = CreateDb();
        SeedHierarchy(db, "A");
        SeedHierarchy(db, "B");
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var user = MakeUser(DwsRoles.SystemAdmin);

        var result = await sut.FilterWaterManagementAreas(db.WaterManagementAreas, user).ToListAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task FilterWaterManagementAreas_WmaScopedUser_SeesOnlyOwnWma()
    {
        using var db = CreateDb();
        var (_, wmaA, _, _, _) = SeedHierarchy(db, "A");
        SeedHierarchy(db, "B"); // different WMA
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var user = MakeUser(DwsRoles.RegionalManager, wmaId: wmaA);

        var result = await sut.FilterWaterManagementAreas(db.WaterManagementAreas, user).ToListAsync();

        Assert.Single(result);
        Assert.Equal(wmaA, result[0].WmaId);
    }

    [Fact]
    public async Task FilterWaterManagementAreas_ProvinceScopedUser_SeesWmasInProvince_NotOthers()
    {
        using var db = CreateDb();
        var (provA, wmaA, _, _, _) = SeedHierarchy(db, "A");
        // Second WMA in same province:
        var wmaA2 = Guid.NewGuid();
        db.WaterManagementAreas.Add(new WaterManagementArea
        {
            WmaId = wmaA2, WmaName = "WMA-A2", WmaCode = "A2", ProvinceId = provA
        });
        SeedHierarchy(db, "Z"); // different province
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var user = MakeUser(DwsRoles.RegionalManager, provinceId: provA);

        var result = await sut.FilterWaterManagementAreas(db.WaterManagementAreas, user).ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.All(result, w => Assert.Equal(provA, w.ProvinceId));
    }

    [Fact]
    public async Task FilterWaterManagementAreas_CatchmentScopedUser_SeesOnlyOwningWma()
    {
        using var db = CreateDb();
        var (_, wmaA, catchA, _, _) = SeedHierarchy(db, "A");
        SeedHierarchy(db, "B"); // different WMA
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        // User is scoped to catchA, which lives in wmaA.
        var user = MakeUser(DwsRoles.Validator, catchmentId: catchA, wmaId: wmaA);

        var result = await sut.FilterWaterManagementAreas(db.WaterManagementAreas, user).ToListAsync();

        Assert.Single(result);
        Assert.Equal(wmaA, result[0].WmaId);
    }

    [Fact]
    public async Task FilterWaterManagementAreas_NoScopeUser_SeesNothing()
    {
        using var db = CreateDb();
        SeedHierarchy(db, "A");
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var user = MakeUser(DwsRoles.Validator); // no catchment, wma, or province claims

        var result = await sut.FilterWaterManagementAreas(db.WaterManagementAreas, user).ToListAsync();

        Assert.Empty(result);
    }
}
