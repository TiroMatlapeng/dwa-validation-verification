using System.Security.Claims;
using dwa_ver_val.Services.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace dwa_ver_val.Tests.Services.Reporting;

public class ReportingServiceScopingTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IMemoryCache Cache() => new MemoryCache(new MemoryCacheOptions());

    private static ClaimsPrincipal NationalManager() =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, DwsRoles.NationalManager) }, "Test"));

    private static ReportingService Svc(ApplicationDBContext db) =>
        new(db, new ScopedCaseQuery(db), new MemoryCache(new MemoryCacheOptions()));

    private static ClaimsPrincipal RegionalManager(Guid wmaId) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, DwsRoles.RegionalManager),
            new Claim("wmaId", wmaId.ToString())
        }, "Test"));

    // Seeds a completed case in the given WMA + catchment; Property carries the WMA used by ScopedCaseQuery.
    private static void SeedCase(ApplicationDBContext db, Guid wmaId, Guid catchmentId)
    {
        var prop = new Property { PropertyId = Guid.NewGuid(), PropertyReferenceNumber = "P", SGCode = "SG", WmaId = wmaId };
        db.Properties.Add(prop);
        db.FileMasters.Add(new FileMaster
        {
            FileMasterId = Guid.NewGuid(), PropertyId = prop.PropertyId, Property = prop,
            CatchmentAreaId = catchmentId, ValidationStatusName = "Completed",
            RegistrationNumber = "N/A", SurveyorGeneralCode = "N/A", PrimaryCatchment = "N/A",
            QuaternaryCatchment = "N/A", FarmName = "N/A", FarmNumber = 1,
            RegistrationDivision = "N/A", FarmPortion = "0", FileCreatedDate = new DateOnly(2026, 1, 1)
        });
    }

    private static CatchmentArea Catchment(string name, Guid wmaId) =>
        new() { CatchmentAreaId = Guid.NewGuid(), CatchmentName = name, CatchmentCode = name, WmaId = wmaId };

    [Fact]
    public async Task Service_Constructs_AndReturnsTableForNationalManager()
    {
        using var db = NewDb();
        var svc = new ReportingService(db, new ScopedCaseQuery(db), Cache());
        var table = await svc.CatchmentProgressAsync(new ReportFilter(), NationalManager(), CancellationToken.None);
        Assert.NotNull(table);
        Assert.Equal("Catchment Progress", table.Title);
    }

    [Fact]
    public async Task RegionalManager_SeesOnlyOwnWma()
    {
        using var db = NewDb();
        Guid wmaA = Guid.NewGuid(), wmaB = Guid.NewGuid();
        var catA = Catchment("A-Catch", wmaA); var catB = Catchment("B-Catch", wmaB);
        db.CatchmentAreas.AddRange(catA, catB);
        SeedCase(db, wmaA, catA.CatchmentAreaId);
        SeedCase(db, wmaB, catB.CatchmentAreaId);
        await db.SaveChangesAsync();

        var table = await Svc(db).CatchmentProgressAsync(new ReportFilter(), RegionalManager(wmaA), CancellationToken.None);
        var row = Assert.Single(table.Rows);
        Assert.Equal("A-Catch", row[0]);
    }

    [Fact]
    public async Task RegionalManager_CannotWidenToAnotherWma()
    {
        using var db = NewDb();
        Guid wmaA = Guid.NewGuid(), wmaB = Guid.NewGuid();
        var catB = Catchment("B-Catch", wmaB);
        db.CatchmentAreas.Add(catB);
        SeedCase(db, wmaB, catB.CatchmentAreaId);
        await db.SaveChangesAsync();

        var table = await Svc(db).CatchmentProgressAsync(
            new ReportFilter(WaterManagementAreaId: wmaB), RegionalManager(wmaA), CancellationToken.None);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task Cache_DoesNotLeakBetweenDifferentWmaUsers()
    {
        using var db = NewDb();
        Guid wmaA = Guid.NewGuid(), wmaB = Guid.NewGuid();
        var catA = Catchment("A-Catch", wmaA); var catB = Catchment("B-Catch", wmaB);
        db.CatchmentAreas.AddRange(catA, catB);
        SeedCase(db, wmaA, catA.CatchmentAreaId);
        SeedCase(db, wmaB, catB.CatchmentAreaId);
        await db.SaveChangesAsync();

        var svc = new ReportingService(db, new ScopedCaseQuery(db), new MemoryCache(new MemoryCacheOptions()));
        var a = await svc.CatchmentProgressAsync(new ReportFilter(), RegionalManager(wmaA), CancellationToken.None);
        var b = await svc.CatchmentProgressAsync(new ReportFilter(), RegionalManager(wmaB), CancellationToken.None);

        Assert.Equal("A-Catch", Assert.Single(a.Rows)[0]);
        Assert.Equal("B-Catch", Assert.Single(b.Rows)[0]); // B did NOT receive A's cached result
    }

    [Fact]
    public async Task CatchmentProgress_BucketsNullCatchmentAsUnassigned()
    {
        using var db = NewDb();
        db.FileMasters.Add(new FileMaster
        {
            FileMasterId = Guid.NewGuid(), PropertyId = Guid.NewGuid(), CatchmentAreaId = null,
            ValidationStatusName = "In Process",
            RegistrationNumber = "N/A", SurveyorGeneralCode = "N/A", PrimaryCatchment = "N/A",
            QuaternaryCatchment = "N/A", FarmName = "N/A", FarmNumber = 1,
            RegistrationDivision = "N/A", FarmPortion = "0", FileCreatedDate = new DateOnly(2026, 1, 1)
        });
        await db.SaveChangesAsync();

        var table = await Svc(db).CatchmentProgressAsync(new ReportFilter(), NationalManager(), CancellationToken.None);
        var row = Assert.Single(table.Rows);
        Assert.Equal("(unassigned)", row[0]);
        Assert.Equal("0.0%", row[5]);
    }
}
