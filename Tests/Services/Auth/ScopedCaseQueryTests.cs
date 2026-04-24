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
}
