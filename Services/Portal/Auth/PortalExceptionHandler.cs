using dwa_ver_val.Helpers;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace dwa_ver_val.Services.Portal.Auth;

/// <summary>
/// IExceptionHandler scoped to /ExternalPortal/* requests. Maps
/// NotFoundException → 404, all other unhandled exceptions → 500.
/// Returns false for non-portal paths so the next handler (or the
/// default UseExceptionHandler middleware for /Home/Error) runs.
/// </summary>
public class PortalExceptionHandler : IExceptionHandler
{
    private const string PortalPathPrefix = "/ExternalPortal";

    private readonly ILogger<PortalExceptionHandler> _logger;

    public PortalExceptionHandler(ILogger<PortalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Path.StartsWithSegments(PortalPathPrefix, StringComparison.OrdinalIgnoreCase))
            return ValueTask.FromResult(false);

        switch (exception)
        {
            case NotFoundException:
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                _logger.LogInformation("Portal 404 for {Path}: {Message}", httpContext.Request.Path, exception.Message);
                return ValueTask.FromResult(true);

            default:
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                _logger.LogError(exception, "Unhandled portal exception for {Path}", httpContext.Request.Path);
                return ValueTask.FromResult(true);
        }
    }
}
