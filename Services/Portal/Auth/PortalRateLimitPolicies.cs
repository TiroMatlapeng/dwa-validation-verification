using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace dwa_ver_val.Services.Portal.Auth;

public static class PortalRateLimitPolicies
{
    public const string AuthStrict = "portal-auth-strict";
    public const string AuthModerate = "portal-auth-moderate";
    public const string WriteDefault = "portal-write-default";

    public static void Configure(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy(AuthStrict, httpContext =>
            RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: PartitionByIp(httpContext),
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(15),
                    SegmentsPerWindow = 5,
                    QueueLimit = 0
                }));

        options.AddPolicy(AuthModerate, httpContext =>
            RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: PartitionByIp(httpContext),
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 3,
                    Window = TimeSpan.FromHours(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0
                }));

        options.AddPolicy(WriteDefault, httpContext =>
            RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: PartitionByPublicUserOrIp(httpContext),
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0
                }));
    }

    private static string PartitionByIp(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";

    private static string PartitionByPublicUserOrIp(HttpContext ctx)
    {
        var publicUserId = ctx.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return publicUserId ?? PartitionByIp(ctx);
    }
}
