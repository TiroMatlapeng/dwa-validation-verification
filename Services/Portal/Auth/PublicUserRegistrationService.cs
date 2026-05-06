using System.Security.Cryptography;
using dwa_ver_val.Helpers;
using dwa_ver_val.Services.Audit;
using dwa_ver_val.Services.Infrastructure.Email;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace dwa_ver_val.Services.Portal.Auth;

public class PublicUserRegistrationService : IPublicUserRegistrationService
{
    private const string DataProtectionPurpose = "PortalEmailConfirm:v1";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    private readonly ApplicationDBContext _db;
    private readonly PasswordHasher<PublicUser> _hasher;
    private readonly IDataProtector _protector;
    private readonly IEmailSender _email;
    private readonly IAuditService _audit;
    private readonly ILogger<PublicUserRegistrationService> _logger;

    public PublicUserRegistrationService(
        ApplicationDBContext db,
        PasswordHasher<PublicUser> hasher,
        IDataProtectionProvider dataProtection,
        IEmailSender email,
        IAuditService audit,
        ILogger<PublicUserRegistrationService> logger)
    {
        _db = db;
        _hasher = hasher;
        _protector = dataProtection.CreateProtector(DataProtectionPurpose);
        _email = email;
        _audit = audit;
        _logger = logger;
    }

    public async Task<RegistrationResult> RegisterAsync(RegistrationRequest req, CancellationToken ct)
    {
        var errors = ValidateRequest(req);
        if (errors.Count > 0) return new RegistrationResult(false, errors);

        if (await _db.PublicUsers.AnyAsync(u => u.EmailAddress == req.EmailAddress, ct))
            return new RegistrationResult(false, new[] { "An account with this email already exists." });

        var user = new PublicUser
        {
            EmailAddress = req.EmailAddress,
            PasswordHash = "", // set below — needs the entity for typed PasswordHasher
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            IdentityNumber = req.IdentityNumber,
            PhoneNumber = req.PhoneNumber,
            EmailConfirmed = false,
            Status = "Pending",
            IsHDI = req.IsHDI,
            HdiConsentGivenDate = req.IsHDI ? DateTime.UtcNow : null,
            RegistrationDate = DateTime.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, req.Password);

        _db.PublicUsers.Add(user);
        await _db.SaveChangesAsync(ct);

        var token = ProtectToken(user.PublicUserId);

        var sent = await _email.SendAsync(new EmailMessage
        {
            To = req.EmailAddress,
            Subject = "Confirm your DWA V&V Portal account",
            BodyText =
                $"Hello {user.FirstName},\n\n" +
                $"Click the link below to confirm your account. The link expires in 24 hours.\n\n" +
                $"[Confirmation link will be substituted by AccountController.Register]\n\n" +
                $"Token: {token}\n\n" +
                $"If you did not register, you can ignore this email."
        }, ct);

        await _audit.LogAsync(new AuditEvent(
            EntityType: nameof(PublicUser),
            EntityId: user.PublicUserId.ToString(),
            Action: "PublicUserRegistered",
            UserDisplayName: $"{user.FirstName} {user.LastName}",
            ToValue: req.EmailAddress));

        if (!sent)
            _logger.LogWarning("Email dispatch returned false for {Email} during registration; the user will need a resend flow (Stage 2b).", req.EmailAddress);

        return new RegistrationResult(true, Array.Empty<string>(), token, user.PublicUserId);
    }

    public async Task<EmailConfirmationResult> ConfirmEmailAsync(string token, CancellationToken ct)
    {
        if (!TryUnprotectToken(token, out var publicUserId, out var expiresAtUtc))
            return new EmailConfirmationResult(false, new[] { "The confirmation link is invalid." });

        if (expiresAtUtc < DateTime.UtcNow)
            return new EmailConfirmationResult(false, new[] { "The confirmation link has expired. Please register again." });

        var user = await _db.PublicUsers.FirstOrDefaultAsync(u => u.PublicUserId == publicUserId, ct);
        if (user is null)
            return new EmailConfirmationResult(false, new[] { "The confirmation link is invalid." });

        if (user.EmailConfirmed)
            return new EmailConfirmationResult(true, Array.Empty<string>(), publicUserId);

        user.EmailConfirmed = true;
        user.Status = "Active";
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEvent(
            EntityType: nameof(PublicUser),
            EntityId: publicUserId.ToString(),
            Action: "PublicUserEmailConfirmed",
            ToValue: user.EmailAddress));

        return new EmailConfirmationResult(true, Array.Empty<string>(), publicUserId);
    }

    private static List<string> ValidateRequest(RegistrationRequest req)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(req.EmailAddress) || !req.EmailAddress.Contains('@'))
            errors.Add("A valid email address is required.");
        if (string.IsNullOrWhiteSpace(req.FirstName)) errors.Add("First name is required.");
        if (string.IsNullOrWhiteSpace(req.LastName)) errors.Add("Last name is required.");
        if (!SaIdValidator.IsValid(req.IdentityNumber))
            errors.Add("A valid 13-digit South African ID number is required.");
        errors.AddRange(PortalPasswordPolicy.Validate(req.Password));
        if (req.IsHDI && !req.HdiConsent)
            errors.Add("To declare HDI status you must consent to the processing of demographic information (POPIA Section 26).");
        if (!req.AcceptTerms)
            errors.Add("You must accept the terms of use to register.");
        return errors;
    }

    private string ProtectToken(Guid publicUserId)
    {
        using var ms = new MemoryStream(24);
        using var bw = new BinaryWriter(ms);
        bw.Write(publicUserId.ToByteArray());
        bw.Write(DateTimeOffset.UtcNow.Add(TokenLifetime).ToUnixTimeSeconds());
        bw.Flush();
        var protectedBytes = _protector.Protect(ms.ToArray());
        return Base64UrlEncode(protectedBytes);
    }

    private bool TryUnprotectToken(string token, out Guid publicUserId, out DateTime expiresAtUtc)
    {
        publicUserId = Guid.Empty;
        expiresAtUtc = DateTime.MinValue;
        if (string.IsNullOrEmpty(token)) return false;

        byte[] raw;
        try
        {
            var protectedBytes = Base64UrlDecode(token);
            raw = _protector.Unprotect(protectedBytes);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }

        if (raw.Length != 24) return false;
        publicUserId = new Guid(raw.AsSpan(0, 16));
        var unix = BitConverter.ToInt64(raw.AsSpan(16, 8));
        expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
        return true;
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static byte[] Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
