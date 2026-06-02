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

    [Fact]
    public async Task Service_Constructs_AndReturnsTableForNationalManager()
    {
        using var db = NewDb();
        var svc = new ReportingService(db, new ScopedCaseQuery(db), Cache());
        var table = await svc.CatchmentProgressAsync(new ReportFilter(), NationalManager(), CancellationToken.None);
        Assert.NotNull(table);
        Assert.Equal("Catchment Progress", table.Title);
    }
}
