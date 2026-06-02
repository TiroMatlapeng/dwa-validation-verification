using System.Security.Claims;
using dwa_ver_val.Services.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace dwa_ver_val.Tests.Services.Reporting;

public class CatchmentProgressTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ReportingService Svc(ApplicationDBContext db) =>
        new(db, new ScopedCaseQuery(db), new MemoryCache(new MemoryCacheOptions()));

    private static ClaimsPrincipal NationalManager() =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, DwsRoles.NationalManager) }, "Test"));

    private static FileMaster Case(Guid catchmentId, string status) => new()
    {
        FileMasterId = Guid.NewGuid(), PropertyId = Guid.NewGuid(),
        CatchmentAreaId = catchmentId, ValidationStatusName = status,
        RegistrationNumber = "N/A", SurveyorGeneralCode = "N/A", PrimaryCatchment = "N/A",
        QuaternaryCatchment = "N/A", FarmName = "N/A", FarmNumber = 1,
        RegistrationDivision = "N/A", FarmPortion = "0",
        FileCreatedDate = new DateOnly(2026, 1, 1)
    };

    [Fact]
    public async Task GroupsByCatchment_WithCountsAndCompletionPct()
    {
        using var db = NewDb();
        var wma = new WaterManagementArea { WmaId = Guid.NewGuid(), WmaName = "Limpopo", WmaCode = "LIM", ProvinceId = Guid.NewGuid() };
        var cat = new CatchmentArea { CatchmentAreaId = Guid.NewGuid(), CatchmentName = "A21A", CatchmentCode = "A21A", WmaId = wma.WmaId };
        db.WaterManagementAreas.Add(wma); db.CatchmentAreas.Add(cat);
        db.FileMasters.AddRange(
            Case(cat.CatchmentAreaId, "Completed"),
            Case(cat.CatchmentAreaId, "Completed"),
            Case(cat.CatchmentAreaId, "In Process"),
            Case(cat.CatchmentAreaId, "Not Commenced"));
        await db.SaveChangesAsync();

        var table = await Svc(db).CatchmentProgressAsync(new ReportFilter(), NationalManager(), CancellationToken.None);

        Assert.Equal("Catchment Progress", table.Title);
        var row = Assert.Single(table.Rows);
        Assert.Equal("A21A", row[0]);   // Catchment
        Assert.Equal("4", row[1]);      // Total
        Assert.Equal("2", row[2]);      // Completed
        Assert.Equal("1", row[3]);      // In Process
        Assert.Equal("50.0%", row[5]);  // Completion % (2/4)
    }
}
