using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace dwa_ver_val.Tests.Services.Auth;

public class DwsClaimsTransformationTests
{
    private static ApplicationDBContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDBContext(options);
    }

    [Fact]
    public async Task TransformAsync_ValidatorScopedToLimpopo_MatchesClaimsFixture()
    {
        // Fixture: contracts/fixtures/auth/claims.json
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "contracts", "fixtures", "auth", "claims.json");
        var fixtureJson = File.ReadAllText(Path.GetFullPath(fixturePath));
        var expected = JsonSerializer.Deserialize<ExpectedClaims>(fixtureJson,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;

        using var db = CreateDb();
        var provinceId = Guid.Parse(expected.ProvinceId);
        var wmaId = Guid.Parse(expected.WmaId);
        var orgUnitId = Guid.Parse(expected.OrgUnitId);
        var userId = Guid.Parse(expected.UserId);

        db.Provinces.Add(new Province
        {
            ProvinceId = provinceId,
            ProvinceName = "Limpopo",
            ProvinceCode = "LP"
        });
        db.WaterManagementAreas.Add(new WaterManagementArea
        {
            WmaId = wmaId,
            WmaName = "Limpopo WMA",
            WmaCode = "LIM",
            ProvinceId = provinceId
        });
        db.OrganisationalUnits.Add(new OrganisationalUnit
        {
            OrgUnitId = orgUnitId,
            Name = "Limpopo Regional Office",
            Type = "Regional",
            ProvinceId = provinceId,
            WmaId = wmaId,
            CatchmentAreaId = null
        });
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = expected.UserName,
            NormalizedUserName = expected.UserName.ToUpperInvariant(),
            Email = expected.Email,
            NormalizedEmail = expected.Email.ToUpperInvariant(),
            FirstName = "Jane",
            LastName = "Validator",
            EmployeeNumber = expected.EmployeeNumber,
            IsActive = true,
            OrgUnitId = orgUnitId
        });
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, expected.UserName));
        identity.AddClaim(new Claim(ClaimTypes.Email, expected.Email));
        identity.AddClaim(new Claim(ClaimTypes.Role, "Validator"));
        var principal = new ClaimsPrincipal(identity);

        var sut = new DwsClaimsTransformation(db);
        var transformed = await sut.TransformAsync(principal);

        Assert.Equal(expected.DisplayName, transformed.FindFirst("displayName")?.Value);
        Assert.Equal(expected.EmployeeNumber, transformed.FindFirst("employeeNumber")?.Value);
        Assert.Equal(expected.OrgUnitId, transformed.FindFirst("orgUnitId")?.Value);
        Assert.Equal(expected.ProvinceId, transformed.FindFirst("provinceId")?.Value);
        Assert.Equal(expected.WmaId, transformed.FindFirst("wmaId")?.Value);
        Assert.Equal(expected.CatchmentId, transformed.FindFirst("catchmentId")?.Value);
        Assert.Contains(transformed.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Validator");
    }

    [Fact]
    public async Task TransformAsync_IsIdempotent_DoesNotDuplicateClaimsOnReCall()
    {
        using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "a@b.c",
            NormalizedUserName = "A@B.C",
            Email = "a@b.c",
            NormalizedEmail = "A@B.C",
            FirstName = "A",
            LastName = "B",
            EmployeeNumber = "X",
            IsActive = true,
            OrgUnitId = null
        });
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        var principal = new ClaimsPrincipal(identity);

        var sut = new DwsClaimsTransformation(db);
        var once = await sut.TransformAsync(principal);
        var twice = await sut.TransformAsync(once);

        Assert.Single(twice.Claims.Where(c => c.Type == "displayName"));
    }

    private record ExpectedClaims(
        string UserId,
        string UserName,
        string Email,
        string DisplayName,
        string EmployeeNumber,
        string[] Roles,
        string OrgUnitId,
        string ProvinceId,
        string WmaId,
        string CatchmentId);
}
