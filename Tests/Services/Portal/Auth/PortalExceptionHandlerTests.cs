using dwa_ver_val.Helpers;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace dwa_ver_val.Tests.Services.Portal.Auth;

public class PortalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_NotFoundException_OnPortalPath_Returns404()
    {
        var handler = new PortalExceptionHandler(NullLogger<PortalExceptionHandler>.Instance);
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/ExternalPortal/Dashboard";

        var handled = await handler.TryHandleAsync(ctx, new NotFoundException("nope"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_NonPortalPath_DoesNotHandle()
    {
        var handler = new PortalExceptionHandler(NullLogger<PortalExceptionHandler>.Instance);
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/Admin/Dashboard";

        var handled = await handler.TryHandleAsync(ctx, new NotFoundException("nope"), CancellationToken.None);

        Assert.False(handled);
    }

    [Fact]
    public async Task TryHandleAsync_GenericException_OnPortalPath_Returns500()
    {
        var handler = new PortalExceptionHandler(NullLogger<PortalExceptionHandler>.Instance);
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/ExternalPortal/Case/abc";

        var handled = await handler.TryHandleAsync(ctx, new InvalidOperationException("oops"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, ctx.Response.StatusCode);
    }
}
