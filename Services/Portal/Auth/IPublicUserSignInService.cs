namespace dwa_ver_val.Services.Portal.Auth;

public record SignInResult(bool Success, string? Error = null, Guid? PublicUserId = null);

public interface IPublicUserSignInService
{
    Task<SignInResult> SignInAsync(string email, string password, CancellationToken ct);
    Task SignOutAsync(CancellationToken ct);
}
