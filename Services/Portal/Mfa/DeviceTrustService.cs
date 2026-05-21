using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace dwa_ver_val.Services.Portal.Mfa;

public class DeviceTrustService : IDeviceTrustService
{
    private readonly ApplicationDBContext _db;

    public DeviceTrustService(ApplicationDBContext db) => _db = db;

    public async Task<bool> IsTrustedAsync(Guid publicUserId, string rawToken, CancellationToken ct = default)
    {
        var hash = HashToken(rawToken);
        return await _db.TrustedDevices
            .AnyAsync(d => d.PublicUserId == publicUserId
                        && d.DeviceTokenHash == hash
                        && d.ExpiresAt > DateTimeOffset.UtcNow, ct);
    }

    public async Task<string> TrustAsync(Guid publicUserId, string? userAgent, CancellationToken ct = default)
    {
        var rawToken = GenerateToken();
        _db.TrustedDevices.Add(new TrustedDevice
        {
            TrustedDeviceId = Guid.NewGuid(),
            PublicUserId = publicUserId,
            DeviceTokenHash = HashToken(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
            UserAgent = userAgent
        });
        await _db.SaveChangesAsync(ct);
        return rawToken;
    }

    public async Task RevokeAllAsync(Guid publicUserId, CancellationToken ct = default)
    {
        var rows = await _db.TrustedDevices
            .Where(d => d.PublicUserId == publicUserId)
            .ToListAsync(ct);
        _db.TrustedDevices.RemoveRange(rows);
        await _db.SaveChangesAsync(ct);
    }

    private static string GenerateToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
}
