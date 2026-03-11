using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Tests.Helpers;

public static class TestDbContextFactory
{
    public static ApplicationDBContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDBContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
