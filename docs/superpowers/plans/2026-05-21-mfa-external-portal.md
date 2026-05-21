# MFA External Portal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add mandatory TOTP + SMS OTP multi-factor authentication to the external portal, with 7-day device trust.

**Architecture:** `MfaController` owns all enrolment and verification actions. `PublicUserSignInService` gains two new methods — `IssuePartialSessionAsync` (writes `MfaPending=true` cookie) and `IssueFullSessionAsync` (writes `MfaEnrolled=true` cookie). `AccountController.Login` and `ConfirmEmail` are updated to redirect through the MFA flow. Four new services (`ITotpService`, `ISmsOtpService`, `IDeviceTrustService`, `ISmsGateway`) live in `Services/Portal/Mfa/`.

**Tech Stack:** `OtpNet` 1.4.0 (TOTP RFC 6238), `QRCoder` 1.6.0 (QR code PNG), EF Core 10, xUnit + Moq.

**Spec:** `docs/superpowers/specs/2026-05-21-mfa-external-portal-design.md`

---

## File Map

**New files:**
- `Models/TrustedDevice.cs`
- `Models/SmsOtp.cs`
- `Services/Portal/Mfa/ISmsGateway.cs`
- `Services/Portal/Mfa/LoggingSmsGateway.cs`
- `Services/Portal/Mfa/ITotpService.cs`
- `Services/Portal/Mfa/TotpService.cs`
- `Services/Portal/Mfa/ISmsOtpService.cs`
- `Services/Portal/Mfa/SmsOtpService.cs`
- `Services/Portal/Mfa/IDeviceTrustService.cs`
- `Services/Portal/Mfa/DeviceTrustService.cs`
- `Areas/ExternalPortal/Controllers/MfaController.cs`
- `Areas/ExternalPortal/ViewModels/MfaViewModels.cs`
- `Areas/ExternalPortal/Views/Mfa/SelectMethod.cshtml`
- `Areas/ExternalPortal/Views/Mfa/EnrolTotp.cshtml`
- `Areas/ExternalPortal/Views/Mfa/EnrolSms.cshtml`
- `Areas/ExternalPortal/Views/Mfa/VerifySmsEnrolment.cshtml`
- `Areas/ExternalPortal/Views/Mfa/Verify.cshtml`
- `Tests/Services/Portal/Mfa/TotpServiceTests.cs`
- `Tests/Services/Portal/Mfa/SmsOtpServiceTests.cs`
- `Tests/Services/Portal/Mfa/DeviceTrustServiceTests.cs`
- `Tests/Areas/ExternalPortal/MfaControllerEnrolmentTests.cs`
- `Tests/Areas/ExternalPortal/MfaControllerVerifyTests.cs`

**Modified files:**
- `dwa_ver_val.csproj` — add OtpNet, QRCoder packages
- `Models/PublicUser.cs` — add `MfaMethod`
- `DatabaseContexts/ApplicationDBContext.cs` — add DbSets, configure TrustedDevice + SmsOtp
- `Services/Portal/Auth/IPublicUserSignInService.cs` — extend `SignInResult`, add two new interface methods
- `Services/Portal/Auth/PublicUserSignInService.cs` — implement new methods, update `SignInAsync` return
- `Services/Portal/Auth/PortalPolicies.cs` — `PortalAuthenticated` gains `MfaEnrolled=true` requirement
- `Areas/ExternalPortal/Controllers/AccountController.cs` — Login + ConfirmEmail redirects
- `Program.cs` — register new services
- `Tests/Areas/ExternalPortal/AccountControllerLoginTests.cs` — update + add MFA-aware tests

---

## Task 1: NuGet Packages + Data Models + Migration

**Files:**
- Modify: `dwa_ver_val.csproj`
- Create: `Models/TrustedDevice.cs`
- Create: `Models/SmsOtp.cs`
- Modify: `Models/PublicUser.cs`
- Modify: `DatabaseContexts/ApplicationDBContext.cs`

- [ ] **Step 1: Add NuGet packages**

```bash
cd "dwa_ver_val ai"
dotnet add package OtpNet --version 1.4.0
dotnet add package QRCoder --version 1.6.0
```

Expected: packages added to `dwa_ver_val.csproj`.

- [ ] **Step 2: Add `MfaMethod` to `PublicUser`**

In `Models/PublicUser.cs`, add after `MfaEnrolledDate`:

```csharp
public string? MfaMethod { get; set; } // "TOTP" | "SMS" | null
```

- [ ] **Step 3: Create `Models/TrustedDevice.cs`**

```csharp
public class TrustedDevice
{
    public Guid TrustedDeviceId { get; set; }
    public Guid PublicUserId { get; set; }
    public required string DeviceTokenHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? UserAgent { get; set; }
    public PublicUser? PublicUser { get; set; }
}
```

- [ ] **Step 4: Create `Models/SmsOtp.cs`**

```csharp
public class SmsOtp
{
    public Guid SmsOtpId { get; set; }
    public Guid PublicUserId { get; set; }
    public required string CodeHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Used { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public PublicUser? PublicUser { get; set; }
}
```

- [ ] **Step 5: Register in `ApplicationDBContext`**

Add DbSets after `PublicUserRecoveryCodes`:

```csharp
public DbSet<TrustedDevice> TrustedDevices { get; set; }
public DbSet<SmsOtp> SmsOtps { get; set; }
```

Add to `NonRestrictForeignKeys` array (after the last entry):

```csharp
(typeof(TrustedDevice), typeof(PublicUser), nameof(TrustedDevice.PublicUserId), DeleteBehavior.Cascade),
(typeof(SmsOtp),        typeof(PublicUser), nameof(SmsOtp.PublicUserId),        DeleteBehavior.Cascade),
```

Add to `OnModelCreating` (after the `PublicUserRecoveryCode` block):

```csharp
// ── TrustedDevice ──
modelBuilder.Entity<TrustedDevice>().HasKey(e => e.TrustedDeviceId);
modelBuilder.Entity<TrustedDevice>()
    .HasOne(e => e.PublicUser)
    .WithMany()
    .HasForeignKey(e => e.PublicUserId)
    .OnDelete(DeleteBehavior.Cascade);
modelBuilder.Entity<TrustedDevice>()
    .Property(e => e.DeviceTokenHash).HasMaxLength(64).IsRequired();
modelBuilder.Entity<TrustedDevice>()
    .HasIndex(e => new { e.PublicUserId, e.ExpiresAt })
    .HasDatabaseName("IX_TrustedDevices_PublicUserId_ExpiresAt");

// ── SmsOtp ──
modelBuilder.Entity<SmsOtp>().HasKey(e => e.SmsOtpId);
modelBuilder.Entity<SmsOtp>()
    .HasOne(e => e.PublicUser)
    .WithMany()
    .HasForeignKey(e => e.PublicUserId)
    .OnDelete(DeleteBehavior.Cascade);
modelBuilder.Entity<SmsOtp>()
    .Property(e => e.CodeHash).HasMaxLength(64).IsRequired();
modelBuilder.Entity<SmsOtp>()
    .HasIndex(e => new { e.PublicUserId, e.Used, e.ExpiresAt })
    .HasDatabaseName("IX_SmsOtps_PublicUserId_Used_ExpiresAt");
```

- [ ] **Step 6: Build and run tests to confirm no regressions**

```bash
dotnet test
```

Expected: same count as before (303), all green.

- [ ] **Step 7: Add EF migration**

```bash
dotnet ef migrations add AddMfaTables
dotnet ef database update
```

Expected: migration created and applied successfully.

- [ ] **Step 8: Commit**

```bash
git add Models/TrustedDevice.cs Models/SmsOtp.cs Models/PublicUser.cs \
    DatabaseContexts/ApplicationDBContext.cs dwa_ver_val.csproj
git add Migrations/
git commit -m "feat(mfa): add TrustedDevice + SmsOtp models; MfaMethod on PublicUser; OtpNet + QRCoder packages"
```

---

## Task 2: ISmsGateway + LoggingSmsGateway Stub

**Files:**
- Create: `Services/Portal/Mfa/ISmsGateway.cs`
- Create: `Services/Portal/Mfa/LoggingSmsGateway.cs`

- [ ] **Step 1: Create `Services/Portal/Mfa/ISmsGateway.cs`**

```csharp
namespace dwa_ver_val.Services.Portal.Mfa;

public interface ISmsGateway
{
    Task<bool> SendAsync(string to, string body, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create `Services/Portal/Mfa/LoggingSmsGateway.cs`**

```csharp
using Microsoft.Extensions.Logging;

namespace dwa_ver_val.Services.Portal.Mfa;

public class LoggingSmsGateway : ISmsGateway
{
    private readonly ILogger<LoggingSmsGateway> _logger;

    public LoggingSmsGateway(ILogger<LoggingSmsGateway> logger)
        => _logger = logger;

    public Task<bool> SendAsync(string to, string body, CancellationToken ct = default)
    {
        _logger.LogInformation("[SMS STUB] To: {To} | Body: {Body}", to, body);
        return Task.FromResult(true);
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Services/Portal/Mfa/ISmsGateway.cs Services/Portal/Mfa/LoggingSmsGateway.cs
git commit -m "feat(mfa): add ISmsGateway interface + LoggingSmsGateway stub"
```

---

## Task 3: ITotpService + TotpService (TDD)

**Files:**
- Create: `Services/Portal/Mfa/ITotpService.cs`
- Create: `Services/Portal/Mfa/TotpService.cs`
- Create: `Tests/Services/Portal/Mfa/TotpServiceTests.cs`

- [ ] **Step 1: Create the interface**

Create `Services/Portal/Mfa/ITotpService.cs`:

```csharp
namespace dwa_ver_val.Services.Portal.Mfa;

public record TotpValidationResult(bool Valid, long? NewTimestamp);

public interface ITotpService
{
    string GenerateSecret();
    string GetQrCodeUri(string secret, string email);
    byte[] GetQrCodePng(string uri);
    TotpValidationResult Validate(string secret, string code, long? lastUsedTimestamp);
}
```

- [ ] **Step 2: Write failing tests**

Create `Tests/Services/Portal/Mfa/TotpServiceTests.cs`:

```csharp
using dwa_ver_val.Services.Portal.Mfa;
using OtpNet;
using Xunit;

namespace dwa_ver_val.Tests.Services.Portal.Mfa;

public class TotpServiceTests
{
    private static TotpService CreateSut() => new();

    [Fact]
    public void GenerateSecret_ReturnsNonEmptyBase32String()
    {
        var sut = CreateSut();
        var secret = sut.GenerateSecret();
        Assert.False(string.IsNullOrWhiteSpace(secret));
        // Base32 uses only A-Z and 2-7
        Assert.Matches("^[A-Z2-7]+=*$", secret);
    }

    [Fact]
    public void GetQrCodeUri_ContainsSecretEmailAndIssuer()
    {
        var sut = CreateSut();
        var secret = sut.GenerateSecret();
        var uri = sut.GetQrCodeUri(secret, "user@example.com");
        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains(secret, uri);
        Assert.Contains("user%40example.com", uri);
        Assert.Contains("issuer=DWA", uri);
    }

    [Fact]
    public void GetQrCodePng_ReturnsNonEmptyByteArray()
    {
        var sut = CreateSut();
        var secret = sut.GenerateSecret();
        var uri = sut.GetQrCodeUri(secret, "user@example.com");
        var png = sut.GetQrCodePng(uri);
        Assert.NotEmpty(png);
        // PNG magic bytes
        Assert.Equal(0x89, png[0]);
        Assert.Equal(0x50, png[1]); // 'P'
        Assert.Equal(0x4E, png[2]); // 'N'
        Assert.Equal(0x47, png[3]); // 'G'
    }

    [Fact]
    public void Validate_WithCurrentCode_ReturnsTrue()
    {
        var sut = CreateSut();
        var secret = sut.GenerateSecret();
        // Generate the current valid code using OtpNet directly
        var key = Base32Encoding.ToBytes(secret);
        var totp = new Totp(key);
        var currentCode = totp.ComputeTotp();

        var result = sut.Validate(secret, currentCode, lastUsedTimestamp: null);

        Assert.True(result.Valid);
        Assert.NotNull(result.NewTimestamp);
    }

    [Fact]
    public void Validate_WithWrongCode_ReturnsFalse()
    {
        var sut = CreateSut();
        var secret = sut.GenerateSecret();

        var result = sut.Validate(secret, "000000", lastUsedTimestamp: null);

        Assert.False(result.Valid);
        Assert.Null(result.NewTimestamp);
    }

    [Fact]
    public void Validate_ReplayAttack_SameTimestampRejected()
    {
        var sut = CreateSut();
        var secret = sut.GenerateSecret();
        var key = Base32Encoding.ToBytes(secret);
        var totp = new Totp(key);
        var code = totp.ComputeTotp();

        var first = sut.Validate(secret, code, lastUsedTimestamp: null);
        Assert.True(first.Valid);

        // Second call with the same code and the timestamp from the first call
        var second = sut.Validate(secret, code, lastUsedTimestamp: first.NewTimestamp);
        Assert.False(second.Valid);
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure (TotpService not yet created)**

```bash
dotnet test --filter "TotpServiceTests" 2>&1 | head -20
```

Expected: build error — `TotpService` not found.

- [ ] **Step 4: Implement `TotpService`**

Create `Services/Portal/Mfa/TotpService.cs`:

```csharp
using OtpNet;
using QRCoder;

namespace dwa_ver_val.Services.Portal.Mfa;

public class TotpService : ITotpService
{
    private const string Issuer = "DWA V&V System";

    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    public string GetQrCodeUri(string secret, string email)
    {
        var encodedEmail = Uri.EscapeDataString(email);
        var encodedIssuer = Uri.EscapeDataString(Issuer);
        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secret}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period=30";
    }

    public byte[] GetQrCodePng(string uri)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(qrData);
        return png.GetGraphic(20);
    }

    public TotpValidationResult Validate(string secret, string code, long? lastUsedTimestamp)
    {
        var key = Base32Encoding.ToBytes(secret);
        var totp = new Totp(key);
        var window = new VerificationWindow(previous: 1, future: 1);

        if (!totp.VerifyTotp(code, out long timeWindowUsed, window))
            return new TotpValidationResult(false, null);

        if (lastUsedTimestamp.HasValue && timeWindowUsed <= lastUsedTimestamp.Value)
            return new TotpValidationResult(false, null);

        return new TotpValidationResult(true, timeWindowUsed);
    }
}
```

- [ ] **Step 5: Run tests — expect green**

```bash
dotnet test --filter "TotpServiceTests"
```

Expected: all 6 tests pass.

- [ ] **Step 6: Commit**

```bash
git add Services/Portal/Mfa/ITotpService.cs Services/Portal/Mfa/TotpService.cs \
    Tests/Services/Portal/Mfa/TotpServiceTests.cs
git commit -m "feat(mfa): implement ITotpService + TotpService with TOTP generation and replay guard"
```

---

## Task 4: ISmsOtpService + SmsOtpService (TDD)

**Files:**
- Create: `Services/Portal/Mfa/ISmsOtpService.cs`
- Create: `Services/Portal/Mfa/SmsOtpService.cs`
- Create: `Tests/Services/Portal/Mfa/SmsOtpServiceTests.cs`

- [ ] **Step 1: Create the interface**

Create `Services/Portal/Mfa/ISmsOtpService.cs`:

```csharp
namespace dwa_ver_val.Services.Portal.Mfa;

public interface ISmsOtpService
{
    Task SendAsync(Guid publicUserId, CancellationToken ct = default);
    Task<bool> ValidateAsync(Guid publicUserId, string code, CancellationToken ct = default);
}
```

- [ ] **Step 2: Write failing tests**

Create `Tests/Services/Portal/Mfa/SmsOtpServiceTests.cs`:

```csharp
using dwa_ver_val.Services.Portal.Mfa;
using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Services.Portal.Mfa;

public class SmsOtpServiceTests
{
    private static (SmsOtpService sut, Mock<ISmsGateway> gateway, ApplicationDBContext db) CreateSut(
        Action<ApplicationDBContext>? seed = null)
    {
        var db = TestDbContextFactory.Create();
        seed?.Invoke(db);
        db.SaveChanges();
        var gateway = new Mock<ISmsGateway>();
        gateway.Setup(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(true);
        var sut = new SmsOtpService(db, gateway.Object);
        return (sut, gateway, db);
    }

    private static PublicUser ActiveUserWithPhone() =>
        new PublicUser
        {
            PublicUserId = Guid.NewGuid(),
            EmailAddress = "user@test.com",
            PasswordHash = "hash",
            FirstName = "A",
            LastName = "B",
            Status = "Active",
            EmailConfirmed = true,
            PhoneNumber = "+27821234567",
            RegistrationDate = DateTime.UtcNow
        };

    [Fact]
    public async Task SendAsync_CallsGateway_AndWritesHashedRow()
    {
        var user = ActiveUserWithPhone();
        var (sut, gateway, db) = CreateSut(d => d.PublicUsers.Add(user));

        await sut.SendAsync(user.PublicUserId);

        gateway.Verify(g => g.SendAsync("+27821234567", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        var row = await db.SmsOtps.SingleAsync();
        Assert.Equal(user.PublicUserId, row.PublicUserId);
        Assert.False(row.Used);
        Assert.True(row.ExpiresAt > DateTimeOffset.UtcNow);
        // CodeHash must not be the raw code (it's hashed)
        Assert.Equal(64, row.CodeHash.Length); // SHA-256 hex = 64 chars
    }

    [Fact]
    public async Task SendAsync_PrunesPriorRowsForSameUser()
    {
        var user = ActiveUserWithPhone();
        var (sut, _, db) = CreateSut(d =>
        {
            d.PublicUsers.Add(user);
            d.SmsOtps.Add(new SmsOtp
            {
                SmsOtpId = Guid.NewGuid(),
                PublicUserId = user.PublicUserId,
                CodeHash = "oldhash",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10), // already expired
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-15),
                Used = false
            });
        });

        await sut.SendAsync(user.PublicUserId);

        // Only the new row should remain
        var rows = await db.SmsOtps.ToListAsync();
        Assert.Single(rows);
        Assert.NotEqual("oldhash", rows[0].CodeHash);
    }

    [Fact]
    public async Task ValidateAsync_CorrectCode_ReturnsTrue_MarksUsed()
    {
        var user = ActiveUserWithPhone();
        var (sut, _, db) = CreateSut(d => d.PublicUsers.Add(user));

        await sut.SendAsync(user.PublicUserId);

        // Retrieve the hashed code, then reverse-engineer by sending a known code
        // Instead: send a fresh OTP and capture the raw code via the gateway mock
        // Re-seed with known hash:
        var knownCode = "123456";
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(knownCode));
        var hexHash = Convert.ToHexString(hash).ToLowerInvariant();

        db.SmsOtps.RemoveRange(db.SmsOtps);
        db.SmsOtps.Add(new SmsOtp
        {
            SmsOtpId = Guid.NewGuid(),
            PublicUserId = user.PublicUserId,
            CodeHash = hexHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            CreatedAt = DateTimeOffset.UtcNow,
            Used = false
        });
        await db.SaveChangesAsync();

        var valid = await sut.ValidateAsync(user.PublicUserId, knownCode);

        Assert.True(valid);
        var row = await db.SmsOtps.SingleAsync();
        Assert.True(row.Used);
    }

    [Fact]
    public async Task ValidateAsync_WrongCode_ReturnsFalse()
    {
        var user = ActiveUserWithPhone();
        var (sut, _, db) = CreateSut(d => d.PublicUsers.Add(user));

        var knownCode = "123456";
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(knownCode))).ToLowerInvariant();
        db.SmsOtps.Add(new SmsOtp
        {
            SmsOtpId = Guid.NewGuid(),
            PublicUserId = user.PublicUserId,
            CodeHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            CreatedAt = DateTimeOffset.UtcNow,
            Used = false
        });
        db.SaveChanges();

        var valid = await sut.ValidateAsync(user.PublicUserId, "999999");

        Assert.False(valid);
    }

    [Fact]
    public async Task ValidateAsync_ExpiredCode_ReturnsFalse()
    {
        var user = ActiveUserWithPhone();
        var (sut, _, db) = CreateSut(d => d.PublicUsers.Add(user));

        var code = "123456";
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
        db.SmsOtps.Add(new SmsOtp
        {
            SmsOtpId = Guid.NewGuid(),
            PublicUserId = user.PublicUserId,
            CodeHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1), // expired
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-6),
            Used = false
        });
        db.SaveChanges();

        Assert.False(await sut.ValidateAsync(user.PublicUserId, code));
    }

    [Fact]
    public async Task ValidateAsync_AlreadyUsedCode_ReturnsFalse()
    {
        var user = ActiveUserWithPhone();
        var (sut, _, db) = CreateSut(d => d.PublicUsers.Add(user));

        var code = "123456";
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
        db.SmsOtps.Add(new SmsOtp
        {
            SmsOtpId = Guid.NewGuid(),
            PublicUserId = user.PublicUserId,
            CodeHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            CreatedAt = DateTimeOffset.UtcNow,
            Used = true // already consumed
        });
        db.SaveChanges();

        Assert.False(await sut.ValidateAsync(user.PublicUserId, code));
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```bash
dotnet test --filter "SmsOtpServiceTests" 2>&1 | head -10
```

Expected: build error — `SmsOtpService` not found.

- [ ] **Step 4: Implement `SmsOtpService`**

Create `Services/Portal/Mfa/SmsOtpService.cs`:

```csharp
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
        => Random.Shared.Next(100000, 999999).ToString();

    private static string HashCode(string code)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
}
```

- [ ] **Step 5: Run tests — expect green**

```bash
dotnet test --filter "SmsOtpServiceTests"
```

Expected: all 5 tests pass.

- [ ] **Step 6: Commit**

```bash
git add Services/Portal/Mfa/ISmsOtpService.cs Services/Portal/Mfa/SmsOtpService.cs \
    Tests/Services/Portal/Mfa/SmsOtpServiceTests.cs
git commit -m "feat(mfa): implement ISmsOtpService + SmsOtpService with hash-based OTP and pruning"
```

---

## Task 5: IDeviceTrustService + DeviceTrustService (TDD)

**Files:**
- Create: `Services/Portal/Mfa/IDeviceTrustService.cs`
- Create: `Services/Portal/Mfa/DeviceTrustService.cs`
- Create: `Tests/Services/Portal/Mfa/DeviceTrustServiceTests.cs`

- [ ] **Step 1: Create the interface**

Create `Services/Portal/Mfa/IDeviceTrustService.cs`:

```csharp
namespace dwa_ver_val.Services.Portal.Mfa;

public interface IDeviceTrustService
{
    Task<bool> IsTrustedAsync(Guid publicUserId, string rawToken, CancellationToken ct = default);
    Task<string> TrustAsync(Guid publicUserId, string? userAgent, CancellationToken ct = default);
    Task RevokeAllAsync(Guid publicUserId, CancellationToken ct = default);
}
```

- [ ] **Step 2: Write failing tests**

Create `Tests/Services/Portal/Mfa/DeviceTrustServiceTests.cs`:

```csharp
using dwa_ver_val.Services.Portal.Mfa;
using dwa_ver_val.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace dwa_ver_val.Tests.Services.Portal.Mfa;

public class DeviceTrustServiceTests
{
    private static (DeviceTrustService sut, ApplicationDBContext db) CreateSut(
        Action<ApplicationDBContext>? seed = null)
    {
        var db = TestDbContextFactory.Create();
        seed?.Invoke(db);
        db.SaveChanges();
        return (new DeviceTrustService(db), db);
    }

    private static PublicUser AnyUser() => new PublicUser
    {
        PublicUserId = Guid.NewGuid(),
        EmailAddress = "x@x.com",
        PasswordHash = "h",
        FirstName = "X",
        LastName = "X",
        Status = "Active",
        EmailConfirmed = true,
        RegistrationDate = DateTime.UtcNow
    };

    [Fact]
    public async Task TrustAsync_InsertsHashedRow_ReturnsRawToken()
    {
        var user = AnyUser();
        var (sut, db) = CreateSut(d => d.PublicUsers.Add(user));

        var rawToken = await sut.TrustAsync(user.PublicUserId, "Mozilla/5.0");

        Assert.False(string.IsNullOrEmpty(rawToken));
        var row = await db.TrustedDevices.SingleAsync();
        Assert.Equal(user.PublicUserId, row.PublicUserId);
        Assert.Equal("Mozilla/5.0", row.UserAgent);
        Assert.True(row.ExpiresAt > DateTimeOffset.UtcNow.AddDays(6));
        // Stored token must be the hash, not the raw value
        Assert.NotEqual(rawToken, row.DeviceTokenHash);
        Assert.Equal(64, row.DeviceTokenHash.Length);
    }

    [Fact]
    public async Task IsTrustedAsync_ValidToken_ReturnsTrue()
    {
        var user = AnyUser();
        var (sut, _) = CreateSut(d => d.PublicUsers.Add(user));

        var rawToken = await sut.TrustAsync(user.PublicUserId, null);

        Assert.True(await sut.IsTrustedAsync(user.PublicUserId, rawToken));
    }

    [Fact]
    public async Task IsTrustedAsync_ExpiredToken_ReturnsFalse()
    {
        var user = AnyUser();
        var (sut, db) = CreateSut(d => d.PublicUsers.Add(user));

        var rawToken = await sut.TrustAsync(user.PublicUserId, null);

        // Manually expire the row
        var row = await db.TrustedDevices.SingleAsync();
        row.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        Assert.False(await sut.IsTrustedAsync(user.PublicUserId, rawToken));
    }

    [Fact]
    public async Task IsTrustedAsync_WrongUser_ReturnsFalse()
    {
        var user1 = AnyUser();
        var user2 = AnyUser();
        var (sut, db) = CreateSut(d => { d.PublicUsers.Add(user1); d.PublicUsers.Add(user2); });

        var rawToken = await sut.TrustAsync(user1.PublicUserId, null);

        Assert.False(await sut.IsTrustedAsync(user2.PublicUserId, rawToken));
    }

    [Fact]
    public async Task IsTrustedAsync_UnknownToken_ReturnsFalse()
    {
        var user = AnyUser();
        var (sut, _) = CreateSut(d => d.PublicUsers.Add(user));

        Assert.False(await sut.IsTrustedAsync(user.PublicUserId, "completely-unknown-token"));
    }

    [Fact]
    public async Task RevokeAllAsync_DeletesOnlyTargetUserRows()
    {
        var user1 = AnyUser();
        var user2 = AnyUser();
        var (sut, db) = CreateSut(d => { d.PublicUsers.Add(user1); d.PublicUsers.Add(user2); });

        await sut.TrustAsync(user1.PublicUserId, null);
        await sut.TrustAsync(user2.PublicUserId, null);

        await sut.RevokeAllAsync(user1.PublicUserId);

        var remaining = await db.TrustedDevices.ToListAsync();
        Assert.Single(remaining);
        Assert.Equal(user2.PublicUserId, remaining[0].PublicUserId);
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```bash
dotnet test --filter "DeviceTrustServiceTests" 2>&1 | head -10
```

Expected: build error — `DeviceTrustService` not found.

- [ ] **Step 4: Implement `DeviceTrustService`**

Create `Services/Portal/Mfa/DeviceTrustService.cs`:

```csharp
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
```

- [ ] **Step 5: Run tests — expect green**

```bash
dotnet test --filter "DeviceTrustServiceTests"
```

Expected: all 6 tests pass.

- [ ] **Step 6: Commit**

```bash
git add Services/Portal/Mfa/IDeviceTrustService.cs Services/Portal/Mfa/DeviceTrustService.cs \
    Tests/Services/Portal/Mfa/DeviceTrustServiceTests.cs
git commit -m "feat(mfa): implement IDeviceTrustService + DeviceTrustService with 7-day token trust"
```

---

## Task 6: Extend SignInService for MFA Flow

**Files:**
- Modify: `Services/Portal/Auth/IPublicUserSignInService.cs`
- Modify: `Services/Portal/Auth/PublicUserSignInService.cs`

- [ ] **Step 1: Extend `SignInResult` and add interface methods**

In `Services/Portal/Auth/IPublicUserSignInService.cs`, replace the file content with:

```csharp
public record SignInResult(bool Success, string? Error = null, Guid? PublicUserId = null, bool MfaEnabled = false);

public interface IPublicUserSignInService
{
    Task<SignInResult> SignInAsync(string email, string password, CancellationToken ct);
    Task IssuePartialSessionAsync(Guid publicUserId, CancellationToken ct);
    Task IssueFullSessionAsync(Guid publicUserId, CancellationToken ct);
    Task SignOutAsync(CancellationToken ct);
}
```

- [ ] **Step 2: Update `SignInAsync` in `PublicUserSignInService` to return `MfaEnabled`**

In `Services/Portal/Auth/PublicUserSignInService.cs`, find the final return statement at the bottom of `SignInAsync`:

```csharp
return new SignInResult(true, null, user.PublicUserId);
```

Replace with:

```csharp
return new SignInResult(true, null, user.PublicUserId, user.MfaEnabled);
```

- [ ] **Step 3: Add `IssuePartialSessionAsync` and `IssueFullSessionAsync` to `PublicUserSignInService`**

Add these two methods to `PublicUserSignInService` (after `SignOutAsync`):

```csharp
public async Task IssuePartialSessionAsync(Guid publicUserId, CancellationToken ct)
{
    var ctx = _httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("IssuePartialSessionAsync requires an active HttpContext.");
    var user = await _db.PublicUsers.FindAsync(new object[] { publicUserId }, ct)
        ?? throw new InvalidOperationException($"User {publicUserId} not found.");

    await ctx.SignOutAsync(PortalCookieOptions.SchemeName);

    var identity = new ClaimsIdentity(PortalCookieOptions.SchemeName);
    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, publicUserId.ToString()));
    identity.AddClaim(new Claim(ClaimTypes.Name, user.EmailAddress));
    identity.AddClaim(new Claim("MfaPending", "true"));

    await ctx.SignInAsync(PortalCookieOptions.SchemeName, new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10) });
}

public async Task IssueFullSessionAsync(Guid publicUserId, CancellationToken ct)
{
    var ctx = _httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("IssueFullSessionAsync requires an active HttpContext.");
    var user = await _db.PublicUsers.FindAsync(new object[] { publicUserId }, ct)
        ?? throw new InvalidOperationException($"User {publicUserId} not found.");

    await ctx.SignOutAsync(PortalCookieOptions.SchemeName);

    var identity = new ClaimsIdentity(PortalCookieOptions.SchemeName);
    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, publicUserId.ToString()));
    identity.AddClaim(new Claim(ClaimTypes.Name, user.EmailAddress));
    identity.AddClaim(new Claim(PortalPolicies.EmailConfirmedClaim, "true"));
    identity.AddClaim(new Claim(PortalPolicies.MfaEnrolledClaim, "true"));
    identity.AddClaim(new Claim(PortalPolicies.StatusClaim, user.Status));

    await ctx.SignInAsync(PortalCookieOptions.SchemeName, new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = false });
}
```

Ensure `using System.Security.Claims;` and `using Microsoft.AspNetCore.Authentication;` are present at the top of `PublicUserSignInService.cs`.

- [ ] **Step 4: Build**

```bash
dotnet build
```

Expected: 0 errors.

- [ ] **Step 5: Run full test suite**

```bash
dotnet test
```

Expected: 303 tests, all green. (`SignInResult` record gained a new optional field with a default — positional usages still compile.)

- [ ] **Step 6: Commit**

```bash
git add Services/Portal/Auth/IPublicUserSignInService.cs Services/Portal/Auth/PublicUserSignInService.cs
git commit -m "feat(mfa): extend SignInService with IssuePartialSessionAsync, IssueFullSessionAsync, MfaEnabled on SignInResult"
```

---

## Task 7: MFA View Models + MfaController Enrolment Actions (TDD)

**Files:**
- Create: `Areas/ExternalPortal/ViewModels/MfaViewModels.cs`
- Create: `Areas/ExternalPortal/Controllers/MfaController.cs`
- Create: `Tests/Areas/ExternalPortal/MfaControllerEnrolmentTests.cs`

- [ ] **Step 1: Create view models**

Create `Areas/ExternalPortal/ViewModels/MfaViewModels.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.Areas.ExternalPortal.ViewModels;

public class MfaSelectMethodViewModel
{
    [Required]
    public string MfaMethod { get; set; } = "";
}

public class MfaEnrolTotpViewModel
{
    public string QrCodeBase64 { get; set; } = "";
    [Required, StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = "";
}

public class MfaEnrolSmsViewModel
{
    [Required, Phone]
    public string PhoneNumber { get; set; } = "";
}

public class MfaVerifySmsEnrolmentViewModel
{
    [Required, StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = "";
}

public class MfaVerifyViewModel
{
    public string MfaMethod { get; set; } = "";
    [Required, StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = "";
    public bool TrustDevice { get; set; }
    public string? ReturnUrl { get; set; }
}
```

- [ ] **Step 2: Write failing enrolment tests**

Create `Tests/Areas/ExternalPortal/MfaControllerEnrolmentTests.cs`:

```csharp
using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.Controllers;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Services.Portal.Mfa;
using dwa_ver_val.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class MfaControllerEnrolmentTests
{
    private static Guid NewUserId() => Guid.NewGuid();

    private static (MfaController sut, ApplicationDBContext db) BuildController(
        Guid userId,
        ITotpService? totp = null,
        ISmsOtpService? smsOtp = null,
        IDeviceTrustService? deviceTrust = null,
        IPublicUserSignInService? signIn = null,
        Action<ApplicationDBContext>? seed = null)
    {
        var db = TestDbContextFactory.Create();
        seed?.Invoke(db);
        db.SaveChanges();

        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        identity.AddClaim(new Claim("MfaPending", "true"));

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        var controller = new MfaController(
            db,
            totp ?? Mock.Of<ITotpService>(),
            smsOtp ?? Mock.Of<ISmsOtpService>(),
            deviceTrust ?? Mock.Of<IDeviceTrustService>(),
            signIn ?? Mock.Of<IPublicUserSignInService>())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = tempData
        };
        return (controller, db);
    }

    private static PublicUser EnrolingUser(Guid userId) => new PublicUser
    {
        PublicUserId = userId,
        EmailAddress = "u@test.com",
        PasswordHash = "h",
        FirstName = "U",
        LastName = "U",
        Status = "Active",
        EmailConfirmed = true,
        MfaEnabled = false,
        PhoneNumber = "+27821234567",
        RegistrationDate = DateTime.UtcNow
    };

    [Fact]
    public async Task SelectMethod_Post_Totp_RedirectsToEnrolTotp()
    {
        var userId = NewUserId();
        var (sut, _) = BuildController(userId);

        var result = await sut.SelectMethod(new MfaSelectMethodViewModel { MfaMethod = "TOTP" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("EnrolTotp", redirect.ActionName);
    }

    [Fact]
    public async Task SelectMethod_Post_Sms_RedirectsToEnrolSms()
    {
        var userId = NewUserId();
        var (sut, _) = BuildController(userId);

        var result = await sut.SelectMethod(new MfaSelectMethodViewModel { MfaMethod = "SMS" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("EnrolSms", redirect.ActionName);
    }

    [Fact]
    public async Task EnrolTotp_Get_SavesSecretAndReturnsView()
    {
        var userId = NewUserId();
        var totpMock = new Mock<ITotpService>();
        totpMock.Setup(t => t.GenerateSecret()).Returns("TESTSECRET");
        totpMock.Setup(t => t.GetQrCodeUri("TESTSECRET", "u@test.com")).Returns("otpauth://totp/test");
        totpMock.Setup(t => t.GetQrCodePng("otpauth://totp/test")).Returns(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        var (sut, db) = BuildController(userId, totp: totpMock.Object,
            seed: d => d.PublicUsers.Add(EnrolingUser(userId)));

        var result = await sut.EnrolTotp(default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MfaEnrolTotpViewModel>(view.Model);
        Assert.False(string.IsNullOrEmpty(model.QrCodeBase64));
        var user = db.PublicUsers.Single();
        Assert.Equal("TESTSECRET", user.MfaSecret);
    }

    [Fact]
    public async Task EnrolTotp_Post_ValidCode_SetsMfaMethodAndRedirectsToDashboard()
    {
        var userId = NewUserId();
        var totpMock = new Mock<ITotpService>();
        totpMock.Setup(t => t.Validate("TESTSECRET", "123456", null))
                .Returns(new TotpValidationResult(true, 12345L));
        var signInMock = new Mock<IPublicUserSignInService>();
        signInMock.Setup(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

        var (sut, db) = BuildController(userId, totp: totpMock.Object, signIn: signInMock.Object,
            seed: d => d.PublicUsers.Add(new PublicUser
            {
                PublicUserId = userId,
                EmailAddress = "u@test.com",
                PasswordHash = "h",
                FirstName = "U",
                LastName = "U",
                Status = "Active",
                EmailConfirmed = true,
                MfaSecret = "TESTSECRET",
                RegistrationDate = DateTime.UtcNow
            }));

        var result = await sut.EnrolTotp(new MfaEnrolTotpViewModel { Code = "123456" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Dashboard", redirect.ControllerName);
        var user = db.PublicUsers.Single();
        Assert.True(user.MfaEnabled);
        Assert.Equal("TOTP", user.MfaMethod);
        signInMock.Verify(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnrolTotp_Post_InvalidCode_ReturnsViewWithError()
    {
        var userId = NewUserId();
        var totpMock = new Mock<ITotpService>();
        totpMock.Setup(t => t.Validate(It.IsAny<string>(), "000000", null))
                .Returns(new TotpValidationResult(false, null));

        var (sut, db) = BuildController(userId, totp: totpMock.Object,
            seed: d => d.PublicUsers.Add(new PublicUser
            {
                PublicUserId = userId,
                EmailAddress = "u@test.com",
                PasswordHash = "h",
                FirstName = "U",
                LastName = "U",
                Status = "Active",
                EmailConfirmed = true,
                MfaSecret = "TESTSECRET",
                RegistrationDate = DateTime.UtcNow
            }));

        var result = await sut.EnrolTotp(new MfaEnrolTotpViewModel { Code = "000000" }, default);

        Assert.IsType<ViewResult>(result);
        Assert.False(sut.ModelState.IsValid);
        var user = db.PublicUsers.Single();
        Assert.False(user.MfaEnabled);
    }

    [Fact]
    public async Task EnrolSms_Post_CallsSendAsync_RedirectsToVerify()
    {
        var userId = NewUserId();
        var smsMock = new Mock<ISmsOtpService>();
        smsMock.Setup(s => s.SendAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var (sut, _) = BuildController(userId, smsOtp: smsMock.Object,
            seed: d => d.PublicUsers.Add(EnrolingUser(userId)));

        var result = await sut.EnrolSms(new MfaEnrolSmsViewModel { PhoneNumber = "+27821234567" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("VerifySmsEnrolment", redirect.ActionName);
        smsMock.Verify(s => s.SendAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifySmsEnrolment_Post_ValidCode_SetsMfaMethodAndRedirects()
    {
        var userId = NewUserId();
        var smsMock = new Mock<ISmsOtpService>();
        smsMock.Setup(s => s.ValidateAsync(userId, "123456", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var signInMock = new Mock<IPublicUserSignInService>();
        signInMock.Setup(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var (sut, db) = BuildController(userId, smsOtp: smsMock.Object, signIn: signInMock.Object,
            seed: d => d.PublicUsers.Add(EnrolingUser(userId)));

        var result = await sut.VerifySmsEnrolment(new MfaVerifySmsEnrolmentViewModel { Code = "123456" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Dashboard", redirect.ControllerName);
        var user = db.PublicUsers.Single();
        Assert.True(user.MfaEnabled);
        Assert.Equal("SMS", user.MfaMethod);
        signInMock.Verify(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifySmsEnrolment_Post_InvalidCode_ReturnsViewWithError()
    {
        var userId = NewUserId();
        var smsMock = new Mock<ISmsOtpService>();
        smsMock.Setup(s => s.ValidateAsync(userId, "999999", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var (sut, db) = BuildController(userId, smsOtp: smsMock.Object,
            seed: d => d.PublicUsers.Add(EnrolingUser(userId)));

        var result = await sut.VerifySmsEnrolment(new MfaVerifySmsEnrolmentViewModel { Code = "999999" }, default);

        Assert.IsType<ViewResult>(result);
        Assert.False(sut.ModelState.IsValid);
        var user = db.PublicUsers.Single();
        Assert.False(user.MfaEnabled);
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```bash
dotnet test --filter "MfaControllerEnrolmentTests" 2>&1 | head -10
```

Expected: build error — `MfaController` not found.

- [ ] **Step 4: Implement `MfaController` enrolment actions**

Create `Areas/ExternalPortal/Controllers/MfaController.cs`:

```csharp
using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Services.Portal.Mfa;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dwa_ver_val.Areas.ExternalPortal.Controllers;

[Area("ExternalPortal")]
[Route("ExternalPortal/[controller]/[action]")]
[Authorize(Policy = PortalPolicies.PortalMfaPending)]
public class MfaController : Controller
{
    private readonly ApplicationDBContext _db;
    private readonly ITotpService _totp;
    private readonly ISmsOtpService _smsOtp;
    private readonly IDeviceTrustService _deviceTrust;
    private readonly IPublicUserSignInService _signIn;

    public MfaController(
        ApplicationDBContext db,
        ITotpService totp,
        ISmsOtpService smsOtp,
        IDeviceTrustService deviceTrust,
        IPublicUserSignInService signIn)
    {
        _db = db;
        _totp = totp;
        _smsOtp = smsOtp;
        _deviceTrust = deviceTrust;
        _signIn = signIn;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── SelectMethod ──

    [HttpGet]
    public IActionResult SelectMethod() => View(new MfaSelectMethodViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> SelectMethod(MfaSelectMethodViewModel vm, CancellationToken ct)
    {
        IActionResult result = vm.MfaMethod == "SMS"
            ? RedirectToAction(nameof(EnrolSms))
            : RedirectToAction(nameof(EnrolTotp));
        return Task.FromResult(result);
    }

    // ── Enrol TOTP ──

    [HttpGet]
    public async Task<IActionResult> EnrolTotp(CancellationToken ct)
    {
        var user = await _db.PublicUsers.FindAsync(new object[] { UserId }, ct);
        if (user is null) return NotFound();

        if (string.IsNullOrEmpty(user.MfaSecret))
        {
            user.MfaSecret = _totp.GenerateSecret();
            await _db.SaveChangesAsync(ct);
        }

        var uri = _totp.GetQrCodeUri(user.MfaSecret, user.EmailAddress);
        var png = _totp.GetQrCodePng(uri);
        return View(new MfaEnrolTotpViewModel { QrCodeBase64 = Convert.ToBase64String(png) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EnrolTotp(MfaEnrolTotpViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _db.PublicUsers.FindAsync(new object[] { UserId }, ct);
        if (user is null) return NotFound();

        var validation = _totp.Validate(user.MfaSecret!, vm.Code, user.LastUsedOtpTimestamp);
        if (!validation.Valid)
        {
            ModelState.AddModelError("", "Invalid code. Please try again.");
            var uri = _totp.GetQrCodeUri(user.MfaSecret!, user.EmailAddress);
            vm.QrCodeBase64 = Convert.ToBase64String(_totp.GetQrCodePng(uri));
            return View(vm);
        }

        user.MfaEnabled = true;
        user.MfaMethod = "TOTP";
        user.MfaEnrolledDate = DateTime.UtcNow;
        user.LastUsedOtpTimestamp = validation.NewTimestamp;
        await _db.SaveChangesAsync(ct);

        await _signIn.IssueFullSessionAsync(UserId, ct);
        return RedirectToAction("Index", "Dashboard", new { area = "ExternalPortal" });
    }

    // ── Enrol SMS ──

    [HttpGet]
    public async Task<IActionResult> EnrolSms(CancellationToken ct)
    {
        var user = await _db.PublicUsers.FindAsync(new object[] { UserId }, ct);
        if (user is null) return NotFound();
        return View(new MfaEnrolSmsViewModel { PhoneNumber = user.PhoneNumber ?? "" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EnrolSms(MfaEnrolSmsViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _db.PublicUsers.FindAsync(new object[] { UserId }, ct);
        if (user is null) return NotFound();

        if (user.PhoneNumber != vm.PhoneNumber)
        {
            user.PhoneNumber = vm.PhoneNumber;
            await _db.SaveChangesAsync(ct);
        }

        await _smsOtp.SendAsync(UserId, ct);
        return RedirectToAction(nameof(VerifySmsEnrolment));
    }

    // ── Verify SMS Enrolment ──

    [HttpGet]
    public IActionResult VerifySmsEnrolment() => View(new MfaVerifySmsEnrolmentViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifySmsEnrolment(MfaVerifySmsEnrolmentViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(vm);

        if (!await _smsOtp.ValidateAsync(UserId, vm.Code, ct))
        {
            ModelState.AddModelError("", "Invalid or expired code. Please try again.");
            return View(vm);
        }

        var user = await _db.PublicUsers.FindAsync(new object[] { UserId }, ct);
        if (user is null) return NotFound();

        user.MfaEnabled = true;
        user.MfaMethod = "SMS";
        user.MfaEnrolledDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _signIn.IssueFullSessionAsync(UserId, ct);
        return RedirectToAction("Index", "Dashboard", new { area = "ExternalPortal" });
    }

    // ── Verify (login flow) — placeholder for Task 8 ──

    [HttpGet]
    public IActionResult Verify(string? returnUrl = null)
        => View(new MfaVerifyViewModel { ReturnUrl = returnUrl });

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Verify(MfaVerifyViewModel vm, CancellationToken ct)
        => Task.FromResult<IActionResult>(View(vm));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> SendSmsCode(string? returnUrl, CancellationToken ct)
        => Task.FromResult<IActionResult>(RedirectToAction(nameof(Verify), new { returnUrl }));
}
```

- [ ] **Step 5: Run enrolment tests — expect green**

```bash
dotnet test --filter "MfaControllerEnrolmentTests"
```

Expected: all 8 tests pass.

- [ ] **Step 6: Run full suite to check for regressions**

```bash
dotnet test
```

Expected: all tests green (count increases by 8).

- [ ] **Step 7: Commit**

```bash
git add Areas/ExternalPortal/Controllers/MfaController.cs \
    Areas/ExternalPortal/ViewModels/MfaViewModels.cs \
    Tests/Areas/ExternalPortal/MfaControllerEnrolmentTests.cs
git commit -m "feat(mfa): MfaController enrolment actions — SelectMethod, EnrolTotp, EnrolSms, VerifySmsEnrolment"
```

---

## Task 8: MfaController Verification Actions (TDD)

**Files:**
- Modify: `Areas/ExternalPortal/Controllers/MfaController.cs`
- Create: `Tests/Areas/ExternalPortal/MfaControllerVerifyTests.cs`

- [ ] **Step 1: Write failing verify tests**

Create `Tests/Areas/ExternalPortal/MfaControllerVerifyTests.cs`:

```csharp
using System.Security.Claims;
using dwa_ver_val.Areas.ExternalPortal.Controllers;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Services.Portal.Mfa;
using dwa_ver_val.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class MfaControllerVerifyTests
{
    private static (MfaController sut, Mock<IResponseCookies> cookies) BuildController(
        Guid userId,
        ITotpService? totp = null,
        ISmsOtpService? smsOtp = null,
        IDeviceTrustService? deviceTrust = null,
        IPublicUserSignInService? signIn = null,
        Action<ApplicationDBContext>? seed = null)
    {
        var db = TestDbContextFactory.Create();
        seed?.Invoke(db);
        db.SaveChanges();

        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        identity.AddClaim(new Claim("MfaPending", "true"));

        var cookiesMock = new Mock<IResponseCookies>();
        var responseMock = new Mock<HttpResponse>();
        responseMock.Setup(r => r.Cookies).Returns(cookiesMock.Object);

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        // Override response to capture cookie writes
        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(c => c.User).Returns(new ClaimsPrincipal(identity));
        httpContextMock.Setup(c => c.Response).Returns(responseMock.Object);
        httpContextMock.Setup(c => c.Request).Returns(httpContext.Request);
        httpContextMock.Setup(c => c.RequestServices).Returns(httpContext.RequestServices);

        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        var controller = new MfaController(
            db,
            totp ?? Mock.Of<ITotpService>(),
            smsOtp ?? Mock.Of<ISmsOtpService>(),
            deviceTrust ?? Mock.Of<IDeviceTrustService>(),
            signIn ?? Mock.Of<IPublicUserSignInService>())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = tempData
        };
        return (controller, cookiesMock);
    }

    private static PublicUser TotpUser(Guid userId) => new PublicUser
    {
        PublicUserId = userId,
        EmailAddress = "u@test.com",
        PasswordHash = "h",
        FirstName = "U",
        LastName = "U",
        Status = "Active",
        EmailConfirmed = true,
        MfaEnabled = true,
        MfaMethod = "TOTP",
        MfaSecret = "TESTSECRET",
        RegistrationDate = DateTime.UtcNow
    };

    private static PublicUser SmsUser(Guid userId) => new PublicUser
    {
        PublicUserId = userId,
        EmailAddress = "u@test.com",
        PasswordHash = "h",
        FirstName = "U",
        LastName = "U",
        Status = "Active",
        EmailConfirmed = true,
        MfaEnabled = true,
        MfaMethod = "SMS",
        PhoneNumber = "+27821234567",
        RegistrationDate = DateTime.UtcNow
    };

    [Fact]
    public async Task Verify_Post_Totp_ValidCode_NoTrust_IssuesFullSession()
    {
        var userId = Guid.NewGuid();
        var totpMock = new Mock<ITotpService>();
        totpMock.Setup(t => t.Validate("TESTSECRET", "123456", null))
                .Returns(new TotpValidationResult(true, 12345L));
        var signInMock = new Mock<IPublicUserSignInService>();
        signInMock.Setup(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var (sut, cookies) = BuildController(userId, totp: totpMock.Object, signIn: signInMock.Object,
            seed: d => d.PublicUsers.Add(TotpUser(userId)));

        var vm = new MfaVerifyViewModel { Code = "123456", TrustDevice = false };
        var result = await sut.Verify(vm, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Dashboard", redirect.ControllerName);
        signInMock.Verify(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        cookies.Verify(c => c.Append("dwa_dtrust", It.IsAny<string>(), It.IsAny<CookieOptions>()), Times.Never);
    }

    [Fact]
    public async Task Verify_Post_Totp_ValidCode_WithTrust_SetsDwaDtrustCookie()
    {
        var userId = Guid.NewGuid();
        var totpMock = new Mock<ITotpService>();
        totpMock.Setup(t => t.Validate("TESTSECRET", "123456", null))
                .Returns(new TotpValidationResult(true, 12345L));
        var signInMock = new Mock<IPublicUserSignInService>();
        signInMock.Setup(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var trustMock = new Mock<IDeviceTrustService>();
        trustMock.Setup(d => d.TrustAsync(userId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync("raw-token-value");

        var (sut, cookies) = BuildController(userId, totp: totpMock.Object, signIn: signInMock.Object,
            deviceTrust: trustMock.Object, seed: d => d.PublicUsers.Add(TotpUser(userId)));

        var vm = new MfaVerifyViewModel { Code = "123456", TrustDevice = true };
        await sut.Verify(vm, default);

        trustMock.Verify(d => d.TrustAsync(userId, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        cookies.Verify(c => c.Append("dwa_dtrust", "raw-token-value", It.Is<CookieOptions>(o =>
            o.HttpOnly && o.Secure && o.SameSite == SameSiteMode.Strict)), Times.Once);
    }

    [Fact]
    public async Task Verify_Post_Totp_InvalidCode_ReturnsViewWithError()
    {
        var userId = Guid.NewGuid();
        var totpMock = new Mock<ITotpService>();
        totpMock.Setup(t => t.Validate(It.IsAny<string>(), "000000", null))
                .Returns(new TotpValidationResult(false, null));
        var signInMock = new Mock<IPublicUserSignInService>();

        var (sut, _) = BuildController(userId, totp: totpMock.Object, signIn: signInMock.Object,
            seed: d => d.PublicUsers.Add(TotpUser(userId)));

        var result = await sut.Verify(new MfaVerifyViewModel { Code = "000000" }, default);

        Assert.IsType<ViewResult>(result);
        Assert.False(sut.ModelState.IsValid);
        signInMock.Verify(s => s.IssueFullSessionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Verify_Post_Sms_ValidCode_IssuesFullSession()
    {
        var userId = Guid.NewGuid();
        var smsMock = new Mock<ISmsOtpService>();
        smsMock.Setup(s => s.ValidateAsync(userId, "123456", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var signInMock = new Mock<IPublicUserSignInService>();
        signInMock.Setup(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var (sut, _) = BuildController(userId, smsOtp: smsMock.Object, signIn: signInMock.Object,
            seed: d => d.PublicUsers.Add(SmsUser(userId)));

        var result = await sut.Verify(new MfaVerifyViewModel { Code = "123456" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        signInMock.Verify(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendSmsCode_Post_CallsSendAsync_RedirectsToVerify()
    {
        var userId = Guid.NewGuid();
        var smsMock = new Mock<ISmsOtpService>();
        smsMock.Setup(s => s.SendAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var (sut, _) = BuildController(userId, smsOtp: smsMock.Object);

        var result = await sut.SendSmsCode(null, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Verify", redirect.ActionName);
        smsMock.Verify(s => s.SendAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Run tests — expect failures (Verify and SendSmsCode are stubs)**

```bash
dotnet test --filter "MfaControllerVerifyTests"
```

Expected: several failures — `Verify` POST returns `View` stub, `SendSmsCode` doesn't call `SendAsync`.

- [ ] **Step 3: Replace the stub Verify and SendSmsCode actions in `MfaController`**

In `Areas/ExternalPortal/Controllers/MfaController.cs`, replace the three stub methods at the bottom with:

```csharp
// ── Verify (login flow) ──

[HttpGet]
public async Task<IActionResult> Verify(string? returnUrl = null, CancellationToken ct = default)
{
    var user = await _db.PublicUsers.FindAsync(new object[] { UserId }, ct);
    if (user is null) return NotFound();
    return View(new MfaVerifyViewModel { MfaMethod = user.MfaMethod ?? "", ReturnUrl = returnUrl });
}

[HttpPost, ValidateAntiForgeryToken]
public async Task<IActionResult> Verify(MfaVerifyViewModel vm, CancellationToken ct)
{
    if (!ModelState.IsValid) return View(vm);

    var user = await _db.PublicUsers.FindAsync(new object[] { UserId }, ct);
    if (user is null) return NotFound();

    bool valid = user.MfaMethod == "SMS"
        ? await _smsOtp.ValidateAsync(UserId, vm.Code, ct)
        : ValidateTotp(user, vm.Code, ct);

    if (!valid)
    {
        ModelState.AddModelError("", "Invalid or expired code. Please try again.");
        vm.MfaMethod = user.MfaMethod ?? "";
        return View(vm);
    }

    await _db.SaveChangesAsync(ct);
    await _signIn.IssueFullSessionAsync(UserId, ct);

    if (vm.TrustDevice)
    {
        var rawToken = await _deviceTrust.TrustAsync(UserId, Request.Headers.UserAgent.ToString(), ct);
        Response.Cookies.Append("dwa_dtrust", rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }

    if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
        return Redirect(vm.ReturnUrl);

    return RedirectToAction("Index", "Dashboard", new { area = "ExternalPortal" });
}

[HttpPost, ValidateAntiForgeryToken]
public async Task<IActionResult> SendSmsCode(string? returnUrl, CancellationToken ct)
{
    await _smsOtp.SendAsync(UserId, ct);
    return RedirectToAction(nameof(Verify), new { returnUrl });
}

private bool ValidateTotp(PublicUser user, string code, CancellationToken ct)
{
    var result = _totp.Validate(user.MfaSecret!, code, user.LastUsedOtpTimestamp);
    if (!result.Valid) return false;
    user.LastUsedOtpTimestamp = result.NewTimestamp;
    return true;
}
```

- [ ] **Step 4: Run verify tests — expect green**

```bash
dotnet test --filter "MfaControllerVerifyTests"
```

Expected: all 5 tests pass.

- [ ] **Step 5: Run full suite**

```bash
dotnet test
```

Expected: all green.

- [ ] **Step 6: Commit**

```bash
git add Areas/ExternalPortal/Controllers/MfaController.cs \
    Tests/Areas/ExternalPortal/MfaControllerVerifyTests.cs
git commit -m "feat(mfa): MfaController Verify and SendSmsCode actions with device trust cookie"
```

---

## Task 9: AccountController Login + ConfirmEmail Changes (TDD)

**Files:**
- Modify: `Areas/ExternalPortal/Controllers/AccountController.cs`
- Modify: `Tests/Areas/ExternalPortal/AccountControllerLoginTests.cs`

- [ ] **Step 1: Update `AccountControllerLoginTests` for MFA-aware login**

Replace the content of `Tests/Areas/ExternalPortal/AccountControllerLoginTests.cs` with:

```csharp
using dwa_ver_val.Areas.ExternalPortal.Controllers;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Services.Portal.Mfa;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class AccountControllerLoginTests
{
    private static AccountController BuildController(
        IPublicUserSignInService signIn,
        IDeviceTrustService? deviceTrust = null)
    {
        var controller = new AccountController(
            new Mock<IPublicUserRegistrationService>().Object,
            signIn,
            deviceTrust ?? Mock.Of<IDeviceTrustService>(),
            NullLogger<AccountController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    [Fact]
    public async Task Login_Post_MfaNotEnrolled_IssuesPartialSession_RedirectsToSelectMethod()
    {
        var userId = Guid.NewGuid();
        var signIn = new Mock<IPublicUserSignInService>();
        signIn.Setup(s => s.SignInAsync("u@e.test", "Goodpassword12!", It.IsAny<CancellationToken>()))
              .ReturnsAsync(new SignInResult(true, null, userId, MfaEnabled: false));
        signIn.Setup(s => s.IssuePartialSessionAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var controller = BuildController(signIn.Object);
        var result = await controller.Login(new LoginViewModel { Email = "u@e.test", Password = "Goodpassword12!" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("SelectMethod", redirect.ActionName);
        Assert.Equal("Mfa", redirect.ControllerName);
        signIn.Verify(s => s.IssuePartialSessionAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Login_Post_MfaEnrolled_DeviceNotTrusted_IssuesPartialSession_RedirectsToVerify()
    {
        var userId = Guid.NewGuid();
        var signIn = new Mock<IPublicUserSignInService>();
        signIn.Setup(s => s.SignInAsync("u@e.test", "Goodpassword12!", It.IsAny<CancellationToken>()))
              .ReturnsAsync(new SignInResult(true, null, userId, MfaEnabled: true));
        signIn.Setup(s => s.IssuePartialSessionAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var deviceTrust = new Mock<IDeviceTrustService>();
        deviceTrust.Setup(d => d.IsTrustedAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

        var controller = BuildController(signIn.Object, deviceTrust.Object);
        var result = await controller.Login(new LoginViewModel { Email = "u@e.test", Password = "Goodpassword12!" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Verify", redirect.ActionName);
        Assert.Equal("Mfa", redirect.ControllerName);
        signIn.Verify(s => s.IssuePartialSessionAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Login_Post_MfaEnrolled_DeviceTrusted_IssuesFullSession_RedirectsToDashboard()
    {
        var userId = Guid.NewGuid();
        var signIn = new Mock<IPublicUserSignInService>();
        signIn.Setup(s => s.SignInAsync("u@e.test", "Goodpassword12!", It.IsAny<CancellationToken>()))
              .ReturnsAsync(new SignInResult(true, null, userId, MfaEnabled: true));
        signIn.Setup(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var deviceTrust = new Mock<IDeviceTrustService>();
        deviceTrust.Setup(d => d.IsTrustedAsync(userId, "trusted-token", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Cookie"] = "dwa_dtrust=trusted-token";

        var controller = new AccountController(
            new Mock<IPublicUserRegistrationService>().Object,
            signIn.Object,
            deviceTrust.Object,
            NullLogger<AccountController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = await controller.Login(new LoginViewModel { Email = "u@e.test", Password = "Goodpassword12!" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Dashboard", redirect.ControllerName);
        signIn.Verify(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Login_Post_FailureFromService_ReturnsViewWithError()
    {
        var signIn = new Mock<IPublicUserSignInService>();
        signIn.Setup(s => s.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new SignInResult(false, "Login failed."));
        var controller = BuildController(signIn.Object);

        var result = await controller.Login(new LoginViewModel { Email = "u@e.test", Password = "wrong" }, default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[""]!.Errors, e => e.ErrorMessage == "Login failed.");
    }

    [Fact]
    public async Task Login_Post_MfaEnrolled_DeviceTrusted_WithReturnUrl_RedirectsToReturnUrl()
    {
        var userId = Guid.NewGuid();
        var signIn = new Mock<IPublicUserSignInService>();
        signIn.Setup(s => s.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new SignInResult(true, null, userId, MfaEnabled: true));
        signIn.Setup(s => s.IssueFullSessionAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var deviceTrust = new Mock<IDeviceTrustService>();
        deviceTrust.Setup(d => d.IsTrustedAsync(userId, "trusted-token", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Cookie"] = "dwa_dtrust=trusted-token";

        var controller = new AccountController(
            new Mock<IPublicUserRegistrationService>().Object,
            signIn.Object,
            deviceTrust.Object,
            NullLogger<AccountController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
        controller.Url = new Mock<IUrlHelper>().Object;
        Mock.Get(controller.Url).Setup(u => u.IsLocalUrl("/ExternalPortal/Dashboard/Index")).Returns(true);

        var result = await controller.Login(new LoginViewModel { Email = "u@e.test", Password = "Goodpassword12!", ReturnUrl = "/ExternalPortal/Dashboard/Index" }, default);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/ExternalPortal/Dashboard/Index", redirect.Url);
    }

    [Fact]
    public async Task Logout_Post_CallsServiceAndRedirectsToLogin()
    {
        var signIn = new Mock<IPublicUserSignInService>();
        signIn.Setup(s => s.SignOutAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var controller = BuildController(signIn.Object);

        var result = await controller.Logout(default);

        signIn.Verify(s => s.SignOutAsync(It.IsAny<CancellationToken>()), Times.Once);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure (AccountController constructor doesn't have IDeviceTrustService yet)**

```bash
dotnet test --filter "AccountControllerLoginTests" 2>&1 | head -10
```

Expected: build error — `AccountController` constructor doesn't match.

- [ ] **Step 3: Update `AccountController`**

Replace `Areas/ExternalPortal/Controllers/AccountController.cs` with:

```csharp
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Services.Portal.Mfa;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dwa_ver_val.Areas.ExternalPortal.Controllers;

[Area("ExternalPortal")]
[Route("ExternalPortal/[controller]/[action]")]
public class AccountController : Controller
{
    private readonly IPublicUserRegistrationService _registration;
    private readonly IPublicUserSignInService _signIn;
    private readonly IDeviceTrustService _deviceTrust;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IPublicUserRegistrationService registration,
        IPublicUserSignInService signIn,
        IDeviceTrustService deviceTrust,
        ILogger<AccountController> logger)
    {
        _registration = registration;
        _signIn = signIn;
        _deviceTrust = deviceTrust;
        _logger = logger;
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(vm);

        var result = await _registration.RegisterAsync(new RegistrationRequest(
            EmailAddress: vm.Email,
            Password: vm.Password,
            FirstName: vm.FirstName,
            LastName: vm.LastName,
            IdentityNumber: vm.IdentityNumber,
            PhoneNumber: vm.PhoneNumber,
            IsHDI: vm.IsHDI,
            HdiConsent: vm.HdiConsent,
            AcceptTerms: vm.AcceptTerms), ct);

        if (!result.Success)
        {
            foreach (var err in result.Errors) ModelState.AddModelError("", err);
            return View(vm);
        }

        var url = Url?.Action(nameof(ConfirmEmail), "Account", new { area = "ExternalPortal", token = result.ConfirmationToken });
        if (!string.IsNullOrEmpty(url)) TempData["DemoConfirmUrl"] = url;
        return RedirectToAction(nameof(RegisterConfirmation));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult RegisterConfirmation()
    {
        ViewData["DemoConfirmUrl"] = TempData["DemoConfirmUrl"];
        return View();
    }

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(string? token, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token))
        {
            ViewData["Success"] = false;
            ViewData["Error"] = "Missing confirmation token.";
            return View();
        }

        var result = await _registration.ConfirmEmailAsync(token, ct);
        if (!result.Success)
        {
            ViewData["Success"] = false;
            ViewData["Error"] = result.Errors.FirstOrDefault() ?? "The confirmation link is invalid.";
            return View();
        }

        // Email confirmed — if already enrolled, go to dashboard; otherwise start MFA enrolment.
        if (result.PublicUserId.HasValue)
        {
            await _signIn.IssuePartialSessionAsync(result.PublicUserId.Value, ct);
            return RedirectToAction("SelectMethod", "Mfa", new { area = "ExternalPortal" });
        }

        ViewData["Success"] = true;
        return View();
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(vm);

        var result = await _signIn.SignInAsync(vm.Email, vm.Password, ct);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error ?? "Login failed.");
            return View(vm);
        }

        var userId = result.PublicUserId!.Value;

        if (!result.MfaEnabled)
        {
            await _signIn.IssuePartialSessionAsync(userId, ct);
            return RedirectToAction("SelectMethod", "Mfa", new { area = "ExternalPortal" });
        }

        // Check device trust cookie
        var dtrustCookie = Request.Cookies["dwa_dtrust"];
        bool trusted = dtrustCookie is not null
            && await _deviceTrust.IsTrustedAsync(userId, dtrustCookie, ct);

        if (trusted)
        {
            await _signIn.IssueFullSessionAsync(userId, ct);
            if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return Redirect(vm.ReturnUrl);
            return RedirectToAction("Index", "Dashboard", new { area = "ExternalPortal" });
        }

        await _signIn.IssuePartialSessionAsync(userId, ct);
        return RedirectToAction("Verify", "Mfa", new { area = "ExternalPortal", returnUrl = vm.ReturnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _signIn.SignOutAsync(ct);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
```

- [ ] **Step 4: Run AccountController tests — expect green**

```bash
dotnet test --filter "AccountControllerLoginTests"
```

Expected: all 6 tests pass.

- [ ] **Step 5: Run full suite**

```bash
dotnet test
```

Expected: all green.

- [ ] **Step 6: Commit**

```bash
git add Areas/ExternalPortal/Controllers/AccountController.cs \
    Tests/Areas/ExternalPortal/AccountControllerLoginTests.cs
git commit -m "feat(mfa): AccountController Login + ConfirmEmail redirect through MFA flow; add IDeviceTrustService dependency"
```

---

## Task 10: Policy Update + DI Registration

**Files:**
- Modify: `Services/Portal/Auth/PortalPolicies.cs`
- Modify: `Program.cs`

- [ ] **Step 1: Add `MfaEnrolled=true` requirement to `PortalAuthenticated`**

In `Services/Portal/Auth/PortalPolicies.cs`, replace the `PortalAuthenticated` policy block:

```csharp
options.AddPolicy(PortalAuthenticated, p => p
    .AddAuthenticationSchemes(PortalCookieOptions.SchemeName)
    .RequireAuthenticatedUser()
    .RequireClaim(EmailConfirmedClaim, "true")
    .RequireClaim(StatusClaim, "Active")
    .RequireClaim(MfaEnrolledClaim, "true"));
```

Also update the comment above it from `// (Stage 2b will add MfaEnrolled=true.)` to `// Stage 2b: MfaEnrolled=true required for full portal access.`

- [ ] **Step 2: Register MFA services in `Program.cs`**

Add after the existing portal service registrations (after line with `IPublicUserSignInService`):

```csharp
builder.Services.AddScoped<dwa_ver_val.Services.Portal.Mfa.ITotpService, dwa_ver_val.Services.Portal.Mfa.TotpService>();
builder.Services.AddScoped<dwa_ver_val.Services.Portal.Mfa.ISmsOtpService, dwa_ver_val.Services.Portal.Mfa.SmsOtpService>();
builder.Services.AddScoped<dwa_ver_val.Services.Portal.Mfa.IDeviceTrustService, dwa_ver_val.Services.Portal.Mfa.DeviceTrustService>();
builder.Services.AddSingleton<dwa_ver_val.Services.Portal.Mfa.ISmsGateway, dwa_ver_val.Services.Portal.Mfa.LoggingSmsGateway>();
```

- [ ] **Step 3: Build**

```bash
dotnet build
```

Expected: 0 errors.

- [ ] **Step 4: Run full test suite**

```bash
dotnet test
```

Expected: all tests green.

- [ ] **Step 5: Commit**

```bash
git add Services/Portal/Auth/PortalPolicies.cs Program.cs
git commit -m "feat(mfa): add MfaEnrolled=true to PortalAuthenticated policy; register MFA services in DI"
```

---

## Task 11: Razor Views (Minimal Functional)

**Files:**
- Create: `Areas/ExternalPortal/Views/Mfa/SelectMethod.cshtml`
- Create: `Areas/ExternalPortal/Views/Mfa/EnrolTotp.cshtml`
- Create: `Areas/ExternalPortal/Views/Mfa/EnrolSms.cshtml`
- Create: `Areas/ExternalPortal/Views/Mfa/VerifySmsEnrolment.cshtml`
- Create: `Areas/ExternalPortal/Views/Mfa/Verify.cshtml`

- [ ] **Step 1: Create `Areas/ExternalPortal/Views/Mfa/SelectMethod.cshtml`**

```html
@model dwa_ver_val.Areas.ExternalPortal.ViewModels.MfaSelectMethodViewModel
@{ ViewData["Title"] = "Set Up Two-Factor Authentication"; }

<h2>Secure Your Account</h2>
<p>Choose your preferred method for two-factor authentication.</p>

<form asp-action="SelectMethod" method="post">
    @Html.AntiForgeryToken()
    <div>
        <label>
            <input type="radio" asp-for="MfaMethod" value="TOTP" />
            Authenticator App (Google Authenticator, Authy, etc.)
        </label>
    </div>
    <div>
        <label>
            <input type="radio" asp-for="MfaMethod" value="SMS" />
            SMS to your registered phone number
        </label>
    </div>
    <span asp-validation-for="MfaMethod" class="text-danger"></span>
    <button type="submit">Continue</button>
</form>
```

- [ ] **Step 2: Create `Areas/ExternalPortal/Views/Mfa/EnrolTotp.cshtml`**

```html
@model dwa_ver_val.Areas.ExternalPortal.ViewModels.MfaEnrolTotpViewModel
@{ ViewData["Title"] = "Set Up Authenticator App"; }

<h2>Scan QR Code</h2>
<p>Scan this QR code with your authenticator app, then enter the 6-digit code below.</p>

<img src="data:image/png;base64,@Model.QrCodeBase64" alt="TOTP QR Code" width="200" height="200" />

<form asp-action="EnrolTotp" method="post">
    @Html.AntiForgeryToken()
    <div>
        <label asp-for="Code">Verification Code</label>
        <input asp-for="Code" autocomplete="one-time-code" inputmode="numeric" maxlength="6" />
        <span asp-validation-for="Code" class="text-danger"></span>
    </div>
    <button type="submit">Verify and Enable</button>
</form>
```

- [ ] **Step 3: Create `Areas/ExternalPortal/Views/Mfa/EnrolSms.cshtml`**

```html
@model dwa_ver_val.Areas.ExternalPortal.ViewModels.MfaEnrolSmsViewModel
@{ ViewData["Title"] = "Set Up SMS Verification"; }

<h2>SMS Verification</h2>
<p>We will send a verification code to your phone number.</p>

<form asp-action="EnrolSms" method="post">
    @Html.AntiForgeryToken()
    <div>
        <label asp-for="PhoneNumber">Mobile Number</label>
        <input asp-for="PhoneNumber" type="tel" />
        <span asp-validation-for="PhoneNumber" class="text-danger"></span>
    </div>
    <button type="submit">Send Code</button>
</form>
```

- [ ] **Step 4: Create `Areas/ExternalPortal/Views/Mfa/VerifySmsEnrolment.cshtml`**

```html
@model dwa_ver_val.Areas.ExternalPortal.ViewModels.MfaVerifySmsEnrolmentViewModel
@{ ViewData["Title"] = "Enter Verification Code"; }

<h2>Enter the Code</h2>
<p>Enter the 6-digit code sent to your phone.</p>

<form asp-action="VerifySmsEnrolment" method="post">
    @Html.AntiForgeryToken()
    <div asp-validation-summary="All" class="text-danger"></div>
    <div>
        <label asp-for="Code">Code</label>
        <input asp-for="Code" autocomplete="one-time-code" inputmode="numeric" maxlength="6" />
        <span asp-validation-for="Code" class="text-danger"></span>
    </div>
    <button type="submit">Verify</button>
</form>
```

- [ ] **Step 5: Create `Areas/ExternalPortal/Views/Mfa/Verify.cshtml`**

```html
@model dwa_ver_val.Areas.ExternalPortal.ViewModels.MfaVerifyViewModel
@{ ViewData["Title"] = "Two-Factor Verification"; }

<h2>Two-Factor Verification</h2>

@if (Model.MfaMethod == "SMS")
{
    <p>Enter the 6-digit code sent to your registered phone number.</p>
    <form asp-action="SendSmsCode" method="post" style="display:inline">
        @Html.AntiForgeryToken()
        <input type="hidden" name="returnUrl" value="@Model.ReturnUrl" />
        <button type="submit">Send Code</button>
    </form>
}
else
{
    <p>Enter the 6-digit code from your authenticator app.</p>
}

<form asp-action="Verify" method="post">
    @Html.AntiForgeryToken()
    <div asp-validation-summary="All" class="text-danger"></div>
    <input type="hidden" asp-for="MfaMethod" />
    <input type="hidden" asp-for="ReturnUrl" />
    <div>
        <label asp-for="Code">Verification Code</label>
        <input asp-for="Code" autocomplete="one-time-code" inputmode="numeric" maxlength="6" />
        <span asp-validation-for="Code" class="text-danger"></span>
    </div>
    <div>
        <label>
            <input asp-for="TrustDevice" type="checkbox" />
            Trust this device for 7 days
        </label>
    </div>
    <button type="submit">Verify</button>
</form>
```

- [ ] **Step 6: Build**

```bash
dotnet build
```

Expected: 0 errors.

- [ ] **Step 7: Run full test suite**

```bash
dotnet test
```

Expected: all tests green.

- [ ] **Step 8: Commit**

```bash
git add Areas/ExternalPortal/Views/Mfa/
git commit -m "feat(mfa): add Razor views for SelectMethod, EnrolTotp, EnrolSms, VerifySmsEnrolment, Verify"
```

---

## Final Verification

- [ ] **Run full test suite one last time**

```bash
dotnet test
```

Expected: all tests green. Count should be 303 + 8 (enrolment) + 5 (verify) + 6 (login) + 6 (TOTP) + 5 (SmsOtp) + 6 (DeviceTrust) = approximately 339.

- [ ] **Confirm architecture boundary test still passes**

```bash
dotnet test --filter "PortalBoundaryTests"
```

Expected: all 3 architecture tests green (MfaController uses `ApplicationDBContext` directly — check if the boundary test needs updating).

> **Note:** If `ExternalPortalArea_MustNotReferenceApplicationDBContext` fails, update the whitelist in that test to allow `MfaController` (it needs direct DB access to load `PublicUser.MfaSecret` and update `MfaMethod`).
