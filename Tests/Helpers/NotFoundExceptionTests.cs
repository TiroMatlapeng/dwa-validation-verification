using Xunit;
using dwa_ver_val.Helpers;

namespace dwa_ver_val.Tests.Helpers;

public class NotFoundExceptionTests
{
    [Fact]
    public void Constructor_SetsMessage()
    {
        var ex = new NotFoundException("missing case");
        Assert.Equal("missing case", ex.Message);
    }

    [Fact]
    public void IsAnException()
    {
        Assert.IsAssignableFrom<Exception>(new NotFoundException("x"));
    }
}
