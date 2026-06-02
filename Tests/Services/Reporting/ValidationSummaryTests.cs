using System.Security.Claims;
using dwa_ver_val.Services.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace dwa_ver_val.Tests.Services.Reporting;

public class ValidationSummaryTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ReportingService Svc(ApplicationDBContext db) =>
        new(db, new ScopedCaseQuery(db), new MemoryCache(new MemoryCacheOptions()));

    private static ClaimsPrincipal NationalManager() =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, DwsRoles.NationalManager) }, "Test"));

    [Fact]
    public async Task SumsEluVolumeAndCountsValidatedPropertiesPerCatchment()
    {
        using var db = NewDb();
        var cat = new CatchmentArea { CatchmentAreaId = Guid.NewGuid(), CatchmentName = "A21A", CatchmentCode = "A21A", WmaId = Guid.NewGuid() };
        db.CatchmentAreas.Add(cat);
        var etype = new EntitlementType { EntitlementTypeId = Guid.NewGuid(), EntitlementName = "Irrigation", EntitlementDescription = "Irrigation water use" };
        db.EntitlementTypes.Add(etype);
        var e1 = new Entitlement { EntitlementId = Guid.NewGuid(), Name = "E1", Volume = 1000m, EntitlementTypeId = etype.EntitlementTypeId, EntitlementType = etype };
        var e2 = new Entitlement { EntitlementId = Guid.NewGuid(), Name = "E2", Volume = 500m, EntitlementTypeId = etype.EntitlementTypeId, EntitlementType = etype };
        db.Entitlements.AddRange(e1, e2);

        FileMaster Case(Guid eid) => new()
        {
            FileMasterId = Guid.NewGuid(), PropertyId = Guid.NewGuid(), CatchmentAreaId = cat.CatchmentAreaId,
            EntitlementId = eid, ValidationStatusName = "Completed",
            RegistrationNumber = "N/A", SurveyorGeneralCode = "N/A", PrimaryCatchment = "N/A",
            QuaternaryCatchment = "N/A", FarmName = "N/A", FarmNumber = 1,
            RegistrationDivision = "N/A", FarmPortion = "0", FileCreatedDate = new DateOnly(2026,1,1)
        };
        db.FileMasters.AddRange(Case(e1.EntitlementId), Case(e2.EntitlementId));
        await db.SaveChangesAsync();

        var table = await Svc(db).ValidationSummaryAsync(new ReportFilter(), NationalManager(), CancellationToken.None);

        var row = Assert.Single(table.Rows);
        Assert.Equal("A21A", row[0]);       // Catchment
        Assert.Equal("2", row[1]);          // Properties validated
        Assert.Equal("1500.00", row[2]);    // Total ELU volume
    }
}
