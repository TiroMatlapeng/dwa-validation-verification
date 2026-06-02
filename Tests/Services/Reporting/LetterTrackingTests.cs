using System.Security.Claims;
using dwa_ver_val.Services.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace dwa_ver_val.Tests.Services.Reporting;

public class LetterTrackingTests
{
    private static ApplicationDBContext NewDb() =>
        new ApplicationDBContext(new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ReportingService Svc(ApplicationDBContext db) =>
        new(db, new ScopedCaseQuery(db), new MemoryCache(new MemoryCacheOptions()));

    private static ClaimsPrincipal NationalManager() =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, DwsRoles.NationalManager) }, "Test"));

    private static FileMaster Case() => new()
    {
        FileMasterId = Guid.NewGuid(), PropertyId = Guid.NewGuid(),
        RegistrationNumber = "N/A", SurveyorGeneralCode = "N/A", PrimaryCatchment = "N/A",
        QuaternaryCatchment = "N/A", FarmName = "N/A", FarmNumber = 1,
        RegistrationDivision = "N/A", FarmPortion = "0", FileCreatedDate = new DateOnly(2026, 1, 1)
    };

    [Fact]
    public async Task GroupsByLetterType_CountsIssuedResponsesOverdueRts()
    {
        using var db = NewDb();
        var fm = Case();
        db.FileMasters.Add(fm);
        var lt = new LetterType { LetterTypeId = Guid.NewGuid(), LetterName = "S35_L1", LetterDescription = "Letter 1", NWASection = "S35" };
        db.LetterTypes.Add(lt);

        // Issued + responded
        db.LetterIssuances.Add(new LetterIssuance { LetterIssuanceId = Guid.NewGuid(), FileMasterId = fm.FileMasterId, LetterTypeId = lt.LetterTypeId, IssuedDate = new DateOnly(2026,1,2), ResponseDate = new DateOnly(2026,1,20) });
        // Issued + overdue (due in the past, no response)
        db.LetterIssuances.Add(new LetterIssuance { LetterIssuanceId = Guid.NewGuid(), FileMasterId = fm.FileMasterId, LetterTypeId = lt.LetterTypeId, IssuedDate = new DateOnly(2020,1,2), DueDate = new DateOnly(2020,2,2) });
        // Issued + returned to sender
        db.LetterIssuances.Add(new LetterIssuance { LetterIssuanceId = Guid.NewGuid(), FileMasterId = fm.FileMasterId, LetterTypeId = lt.LetterTypeId, IssuedDate = new DateOnly(2026,1,2), ReturnedToSender = true });
        await db.SaveChangesAsync();

        var table = await Svc(db).LetterTrackingAsync(new ReportFilter(), NationalManager(), CancellationToken.None);

        var row = Assert.Single(table.Rows);
        Assert.Equal("S35_L1", row[0]); // Letter type
        Assert.Equal("3", row[1]);      // Issued
        Assert.Equal("1", row[2]);      // Responses
        Assert.Equal("1", row[3]);      // Overdue
        Assert.Equal("1", row[4]);      // Returned to sender
    }
}
