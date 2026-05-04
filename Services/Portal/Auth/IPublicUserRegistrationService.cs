namespace dwa_ver_val.Services.Portal.Auth;

public record RegistrationRequest(
    string EmailAddress,
    string Password,
    string FirstName,
    string LastName,
    string IdentityNumber,
    string? PhoneNumber,
    bool IsHDI,
    bool HdiConsent,
    bool AcceptTerms);

public record RegistrationResult(
    bool Success,
    IReadOnlyList<string> Errors,
    string? ConfirmationToken = null,
    Guid? PublicUserId = null);

public record EmailConfirmationResult(
    bool Success,
    IReadOnlyList<string> Errors,
    Guid? PublicUserId = null);

public interface IPublicUserRegistrationService
{
    Task<RegistrationResult> RegisterAsync(RegistrationRequest request, CancellationToken ct);
    Task<EmailConfirmationResult> ConfirmEmailAsync(string token, CancellationToken ct);
}
