using System.Security.Claims;

namespace dwa_ver_val.Services.Dashboard;

public interface IDashboardService
{
    Task<DashboardViewModel> GetAsync(ClaimsPrincipal user, CancellationToken ct);
}
