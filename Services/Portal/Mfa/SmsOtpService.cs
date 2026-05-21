using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace dwa_ver_val.Services.Portal.Mfa;

public class SmsOtpService : ISmsOtpService
{
    private readonly ApplicationDBContext _db;
    private readonly ISmsGateway _gateway;

    public SmsOtpService(ApplicationDBContext db, ISmsGateway gateway)
    {
        _db = db;
        _gateway = gateway;
    }

    public async Task SendAsync(Guid publicUserId, CancellationToken ct = default)
    {
        var user = await _db.PublicUsers.FindAsync(new object[] { publicUserId }, ct)
            ?? throw new InvalidOperationException($"User {publicUserId} not found.");

        // Prune prior OTPs for this user
        var old = await _db.SmsOtps.Where(s => s.PublicUserId == publicUserId).ToListAsync(ct);
        _db.SmsOtps.RemoveRange(old);

        var code = GenerateCode();
        _db.SmsOtps.Add(new SmsOtp
        {
            SmsOtpId = Guid.NewGuid(),
            PublicUserId = publicUserId,
            CodeHash = HashCode(code),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            CreatedAt = DateTimeOffset.UtcNow,
            Used = false
        });
        await _db.SaveChangesAsync(ct);

        await _gateway.SendAsync(user.PhoneNumber ?? "", $"Your DWA V&V verification code is: {code}", ct);
    }

    public async Task<bool> ValidateAsync(Guid publicUserId, string code, CancellationToken ct = default)
    {
        var hash = HashCode(code);
        var row = await _db.SmsOtps
            .Where(s => s.PublicUserId == publicUserId
                     && s.CodeHash == hash
                     && !s.Used
                     && s.ExpiresAt > DateTimeOffset.UtcNow)
            .FirstOrDefaultAsync(ct);

        if (row is null) return false;

        row.Used = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string GenerateCode()
        => RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    private static string HashCode(string code)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
}
