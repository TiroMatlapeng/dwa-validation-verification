using System.Security.Claims;
using dwa_ver_val.Services.Audit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace dwa_ver_val.Services.Portal.Auth;

public class PublicUserSignInService : IPublicUserSignInService
{
    public const string GenericLoginFailed = "Login failed.";
    public const string EmailNotConfirmed = "Please confirm your email before logging in.";
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly ApplicationDBContext _db;
    private readonly PasswordHasher<PublicUser> _hasher;
    private readonly IHttpContextAccessor _httpContext;
    private readonly IAuditService _audit;
    private readonly ILogger<PublicUserSignInService> _logger;

    public PublicUserSignInService(
        ApplicationDBContext db,
        PasswordHasher<PublicUser> hasher,
        IHttpContextAccessor httpContext,
        IAuditService audit,
        ILogger<PublicUserSignInService> logger)
    {
        _db = db;
        _hasher = hasher;
        _httpContext = httpContext;
        _audit = audit;
        _logger = logger;
    }

    public async Task<SignInResult> SignInAsync(string email, string password, CancellationToken ct)
    {
        var ctx = _httpContext.HttpContext
            ?? throw new InvalidOperationException("PublicUserSignInService requires an active HttpContext.");

        var user = await _db.PublicUsers
            .FirstOrDefaultAsync(u => u.EmailAddress == email, ct);

        if (user is null)
        {
            await _audit.LogAsync(new AuditEvent(
                EntityType: nameof(PublicUser),
                EntityId: "(unknown)",
                Action: "PublicUserSignInFailed",
                Reason: "UnknownEmail",
                ToValue: email));
            return new SignInResult(false, GenericLoginFailed);
        }

        // S2: Block locked-out accounts before attempting password hashing.
        if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.UtcNow)
        {
            await _audit.LogAsync(new AuditEvent(
                EntityType: nameof(PublicUser),
                EntityId: user.PublicUserId.ToString(),
                Action: "PublicUserSignInFailed",
                Reason: "AccountLocked",
                ToValue: email));
            return new SignInResult(false, GenericLoginFailed);
        }

        var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verify == PasswordVerificationResult.Failed)
        {
            // S2: Increment counter; lock on threshold.
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
                user.LockoutUntil = DateTime.UtcNow.Add(LockoutDuration);
            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync(new AuditEvent(
                EntityType: nameof(PublicUser),
                EntityId: user.PublicUserId.ToString(),
                Action: "PublicUserSignInFailed",
                Reason: "WrongPassword",
                ToValue: email));
            return new SignInResult(false, GenericLoginFailed);
        }

        if (!user.EmailConfirmed)
        {
            await _audit.LogAsync(new AuditEvent(
                EntityType: nameof(PublicUser),
                EntityId: user.PublicUserId.ToString(),
                Action: "PublicUserSignInFailed",
                Reason: "EmailNotConfirmed",
                ToValue: email));
            return new SignInResult(false, EmailNotConfirmed);
        }

        // S1: Refuse Suspended and Deactivated accounts.
        // Placed after password verify so attackers cannot discover account status without the password.
        if (user.Status != "Active")
        {
            await _audit.LogAsync(new AuditEvent(
                EntityType: nameof(PublicUser),
                EntityId: user.PublicUserId.ToString(),
                Action: "PublicUserSignInFailed",
                Reason: "AccountSuspended",
                ToValue: email));
            return new SignInResult(false, GenericLoginFailed);
        }

        // S2: Reset lockout counters on successful authentication.
        user.FailedLoginAttempts = 0;
        user.LockoutUntil = null;

        var identity = new ClaimsIdentity(PortalCookieOptions.SchemeName);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.PublicUserId.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.EmailAddress));
        identity.AddClaim(new Claim(PortalPolicies.EmailConfirmedClaim, "true"));
        identity.AddClaim(new Claim(PortalPolicies.MfaEnrolledClaim, user.MfaEnabled ? "true" : "false"));
        identity.AddClaim(new Claim(PortalPolicies.StatusClaim, user.Status));

        var principal = new ClaimsPrincipal(identity);

        await ctx.SignInAsync(PortalCookieOptions.SchemeName, principal, new AuthenticationProperties
        {
            IsPersistent = false
        });

        user.LastLoginDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEvent(
            EntityType: nameof(PublicUser),
            EntityId: user.PublicUserId.ToString(),
            Action: "PublicUserSignedIn",
            ToValue: email));

        return new SignInResult(true, null, user.PublicUserId);
    }

    public async Task SignOutAsync(CancellationToken ct)
    {
        var ctx = _httpContext.HttpContext
            ?? throw new InvalidOperationException("PublicUserSignInService requires an active HttpContext.");
        await ctx.SignOutAsync(PortalCookieOptions.SchemeName);
    }
}
