# MFA for External Portal — Design Spec
**Date:** 2026-05-21
**Status:** Approved

## Summary

Add mandatory TOTP and SMS OTP multi-factor authentication to the external portal. MFA is required for all case data access. Users enrol during registration (after email confirmation). Verified devices are trusted for 7 days to reduce login fatigue.

---

## 1. Data Model

### `PublicUser` — one new field

| Field | Type | Purpose |
|---|---|---|
| `MfaMethod` | `string?` | `"TOTP"` \| `"SMS"` \| `null` (null = not yet enrolled) |

Existing fields already in use: `MfaEnabled`, `MfaSecret` (TOTP secret), `MfaEnrolledDate`, `LastUsedOtpTimestamp` (TOTP replay guard), `PhoneNumber` (pre-filled for SMS enrolment).

### New: `TrustedDevice`

| Field | Type | Notes |
|---|---|---|
| `TrustedDeviceId` | `Guid` | PK |
| `PublicUserId` | `Guid` | FK → `PublicUser` |
| `DeviceTokenHash` | `string` | SHA-256 of raw cookie token — raw token never persisted |
| `ExpiresAt` | `DateTimeOffset` | 7 days from creation |
| `CreatedAt` | `DateTimeOffset` | |
| `UserAgent` | `string?` | For future "manage devices" display |

### New: `SmsOtp`

| Field | Type | Notes |
|---|---|---|
| `SmsOtpId` | `Guid` | PK |
| `PublicUserId` | `Guid` | FK → `PublicUser` |
| `CodeHash` | `string` | SHA-256 of the 6-digit code — raw code never persisted |
| `ExpiresAt` | `DateTimeOffset` | 5 minutes from creation |
| `Used` | `bool` | Marked true on successful validation |
| `CreatedAt` | `DateTimeOffset` | |

On each new OTP request, prior unused/expired rows for the same user are pruned.

### New NuGet packages

- `OtpNet` — TOTP generation and validation (RFC 6238)
- `QRCoder` — renders the `otpauth://` URI as a PNG for the enrolment screen

---

## 2. Services

All live in `Services/Portal/Mfa/`.

### `ISmsGateway` / `LoggingSmsGateway`

Provider-swap seam. Ships with a `LoggingSmsGateway` stub that logs the OTP to the application logger. Replaced by a real provider (BulkSMS, Twilio, etc.) by changing a single DI registration in `Program.cs`.

```
SendAsync(to: string, body: string) → Task<bool>
```

### `ITotpService` / `TotpService`

Pure TOTP logic — no DB access.

```
GenerateSecret()                       → string (base32)
GetQrCodeUri(secret, email)            → string (otpauth:// URI)
GetQrCodePng(uri)                      → byte[] (PNG rendered by QRCoder)
Validate(secret, code, lastTimestamp)  → (bool valid, long? newTimestamp)
```

`Validate` checks current window ±1 step for clock drift. Returns the new timestamp to be saved on `PublicUser.LastUsedOtpTimestamp` to block replay.

### `ISmsOtpService` / `SmsOtpService`

SMS OTP lifecycle. Owns DB writes and gateway calls.

```
SendAsync(publicUserId)           → Task  (generates code, hashes, writes SmsOtp, calls gateway, prunes old rows)
ValidateAsync(publicUserId, code) → Task<bool>  (hashes code, finds unexpired unused row, marks Used=true)
```

### `IDeviceTrustService` / `DeviceTrustService`

Device trust cookie lifecycle.

```
IsTrustedAsync(publicUserId, rawToken) → Task<bool>  (hash token, check DB for unexpired row)
TrustAsync(publicUserId, userAgent)    → Task<string> (generate raw token, insert hashed row, return raw token)
RevokeAllAsync(publicUserId)           → Task         (delete all rows for user)
```

---

## 3. Auth Flow

### Enrolment (once, after email confirmation)

```
Register → confirmation email sent
  → user clicks link → AccountController.ConfirmEmail sets EmailConfirmed=true
  → redirect to MfaController.SelectMethod
      → user picks TOTP or SMS

TOTP path:
  MfaController.EnrolTotp GET  → display QR code (PNG from ITotpService)
  MfaController.EnrolTotp POST → validate submitted code
                               → on success: set MfaMethod="TOTP", MfaEnabled=true, MfaEnrolledDate=now
                               → redirect to dashboard

SMS path:
  MfaController.EnrolSms GET          → display pre-filled phone number, "Send code" button
  MfaController.EnrolSms POST (send)  → ISmsOtpService.SendAsync → redirect to verify form
  MfaController.VerifySmsEnrolment GET  → code entry form
  MfaController.VerifySmsEnrolment POST → validate code
                                        → on success: set MfaMethod="SMS", MfaEnabled=true, MfaEnrolledDate=now
                                        → redirect to dashboard
```

After setting `EmailConfirmed=true`, `ConfirmEmail` writes the `PortalMfaPending` partial-auth cookie (same scheme used during login-based MFA). This gates all enrolment actions under `[Authorize(Policy = PortalPolicies.PortalMfaPending)]` — no separate enrolment-specific policy needed.

If the user revisits the confirmation link after enrolment, `ConfirmEmail` detects `MfaEnabled=true` and redirects straight to dashboard without writing the cookie.

### Login (every subsequent visit)

```
AccountController.Login POST (credentials valid)
  → IDeviceTrustService.IsTrustedAsync(userId, dwa_dtrust cookie)
      → trusted: issue full session cookie → dashboard  [MFA skipped]
      → not trusted:
          → write partial-auth cookie (scheme: PortalMfaPending, claims: PublicUserId + MfaPending=true)
          → redirect to MfaController.Verify

MfaController.Verify GET
  → show TOTP entry form (if MfaMethod=TOTP)
  → show SMS entry form + "Send code" button (if MfaMethod=SMS)

MfaController.Verify POST
  → validate code (ITotpService or ISmsOtpService)
  → on failure: return view with error
  → on success:
      → delete partial-auth cookie
      → issue full session cookie (MfaEnrolled=true claim)
      → if "trust this device" checked:
          IDeviceTrustService.TrustAsync → set dwa_dtrust cookie (HttpOnly, Secure, SameSite=Strict, 7 days)
      → redirect to dashboard / returnUrl

MfaController.SendSmsCode POST  [PortalMfaPending guard]
  → ISmsOtpService.SendAsync
  → redirect to MfaController.Verify GET
```

### Policy gating

`PortalPolicies.Portal` (applied to Case, Dashboard, Document, Objection, PropertyClaim, Response controllers) gains a `RequireClaim("MfaEnrolled", "true")` requirement. No changes to those controllers.

---

## 4. `MfaController` Action Summary

| Action | Method | Auth guard | Purpose |
|---|---|---|---|
| `SelectMethod` | GET | `PortalMfaPending` | Choose TOTP or SMS |
| `SelectMethod` | POST | `PortalMfaPending` | Save choice, redirect to enrol |
| `EnrolTotp` | GET | `PortalMfaPending` | Show QR code |
| `EnrolTotp` | POST | `PortalMfaPending` | Validate code, complete enrolment |
| `EnrolSms` | GET | `PortalMfaPending` | Show phone (editable if blank), "Send code" |
| `EnrolSms` | POST | `PortalMfaPending` | Save phone if updated, send OTP, redirect to verify |
| `VerifySmsEnrolment` | GET | `PortalMfaPending` | Code entry form |
| `VerifySmsEnrolment` | POST | `PortalMfaPending` | Validate code, complete enrolment |
| `Verify` | GET | `PortalMfaPending` | TOTP or SMS code entry |
| `Verify` | POST | `PortalMfaPending` | Validate; issue session; optional device trust |
| `SendSmsCode` | POST | `PortalMfaPending` | Send new SMS OTP during login verification |

Cookie names: `dwa_dtrust` (device trust, 7-day persistent), partial-auth uses existing `PortalMfaPending` scheme.

---

## 5. Testing Strategy

### `TotpServiceTests`
- `GenerateSecret` returns valid base32 string
- `GetQrCodeUri` formats `otpauth://` URI with issuer, email, secret
- `Validate` returns true for current window, false for wrong code
- Replay guard: same timestamp rejected on second call
- Clock drift: code from ±1 step validates

### `SmsOtpServiceTests`
- `SendAsync` calls gateway once, writes one row, prunes prior rows for same user
- `ValidateAsync` returns true for matching unexpired unused code, marks `Used=true`
- `ValidateAsync` returns false for expired, used, or wrong code

### `DeviceTrustServiceTests`
- `IsTrustedAsync` true for valid unexpired token, false for expired/wrong user/unknown
- `TrustAsync` inserts hashed row, returns raw token
- `RevokeAllAsync` deletes only target user's rows

### `MfaControllerEnrolmentTests`
- `SelectMethod` POST TOTP → redirects to `EnrolTotp`
- `SelectMethod` POST SMS → redirects to `EnrolSms`
- `EnrolTotp` POST valid code → sets `MfaMethod=TOTP`, redirects dashboard
- `EnrolTotp` POST wrong code → view with error, method not set
- `EnrolSms` POST → calls `SendAsync`, redirects to verify form
- `VerifySmsEnrolment` POST valid → sets `MfaMethod=SMS`, redirects dashboard
- `VerifySmsEnrolment` POST wrong → view with error, method not set

### `MfaControllerVerifyTests`
- Trusted device → login bypasses MFA (tested in `AccountController` login tests)
- Verify POST TOTP valid + trust checked → full session issued, `TrustAsync` called, cookie set
- Verify POST TOTP valid, trust unchecked → full session issued, no device cookie
- Verify POST wrong code → view with error, no session issued
- `SendSmsCode` POST → calls `SendAsync`, redirects to `Verify` GET

---

## 6. Out of Scope (this iteration)

- Recovery codes (deferred — add in a later sprint)
- Real SMS provider integration (ships with `LoggingSmsGateway` stub)
- "Manage devices" UI for revoking trusted devices
- MFA method change after enrolment
