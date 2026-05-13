# External Portal Shell — Design Spec

**Date:** 2026-05-03
**Status:** Draft for review
**Rollout plan items:** Task 9.1 (full) + 9.2 (full) + 9.4 (full) + 9.3 (minimum slice — title-deed upload only, for property-claim evidence)
**Branch:** `demo/azure-deploy`
**Journal:** `docs/superpowers/tasks/2026-05-03-external-portal-shell/journal.md`

---

## 1. Purpose & scope

Add a public-facing self-service portal at `/Areas/ExternalPortal/` so registered water users / landowners can view the V&V case status of properties they own, see the workflow history, and download statutory letters addressed to them. This is the "External User Portal" capability described in `CLAUDE.md` and Tasks 9.1, 9.2, 9.4 of `Project rollout plan.xlsx`.

The portal is **read-only** with respect to case data. Public users cannot transition workflow states, modify property data, or sign letters. Their only write operations are: their own profile, requests to link properties they own, and uploads of supporting evidence (title deed PDFs).

### 1.1 In scope

- New MVC Area `Areas/ExternalPortal/` with separate layout, controllers, views.
- `PublicUser` self-registration with email confirmation.
- Forced TOTP MFA enrolment on first login (`Otp.NET` + QR code + 10 hashed recovery codes).
- Login / logout / password reset under a new cookie scheme `PublicPortalScheme`.
- Property linking with two evidence paths into one DWS approval queue:
  - **Auto-suggest by ID match** against `PropertyOwner.IdentityDocumentNumber` — suggestions only, never auto-grants access.
  - **Manual claim** by SG code with mandatory title-deed PDF upload.
- DWS staff approval queue (new internal page, `Validator+` role) showing pending claims with confidence flag and evidence preview.
- "My Cases" dashboard scoped strictly through `IPublicUserPropertyAccessor`.
- Case detail page with timeline (workflow transitions + letter issuances) and PDF download for letters where the user is the addressee.
- In-app notification list (bell + page) plus email send (`IEmailSender` interface only — `LoggingEmailSender` writes to `ILogger` for now).
- Account lockout on `PublicUser` (independent of ASP.NET Identity's lockout).
- `IFileStorage` abstraction with `LocalDiskFileStorage` writing under `{ContentRootPath}/portal-uploads/` (NOT `wwwroot`).
- Rate limiting on all anonymous auth endpoints.
- Audit-log entries for every portal auth event.

### 1.2 Out of scope (explicit, with deferral target)

| Item | Deferred to |
|---|---|
| SMS OTP as MFA factor | Task 2.6 (Key Vault + SMS provider procurement) |
| Real email provider (SendGrid / Azure Communication Services / DWS SMTP relay) | Task 2.6 |
| Azure Blob Storage upload root | Task 2.4 |
| Virus scanning on uploads | Future security hardening sprint |
| Comment / letter response submission by user | Task 9.3 |
| Objection lodging UI + DWS objection report | Task 5.4 — *and per client clarification 2026-05-03, that whole feature is reduced to "lodge + documents + DWS report only", no internal adjudication workflow* |
| Two-way letter response upload | Task 9.3 |
| `IdentityNumber` encryption at rest | Task 10.3 (POPIA review) — guarded by fail-fast Production check, see §5.6 |
| Conversion of remaining status-string fields (`PublicUser.Status`, `LetterIssuance.ResponseStatus`, `Document.VirusScanStatus`) to enums + value converters | Backlog — only the two new ones (`PublicUserProperty.Status`, `PublicUserProperty.EvidenceType`) are converted in this slice |
| Temporal tables for `PublicUser` / `PublicUserRecoveryCode` | Future POPIA hardening — flagged as a future option, not implemented now |

---

## 2. Architecture

### 2.1 Solution shape (delta)

```
Areas/
  ExternalPortal/
    Controllers/
      AccountController.cs            (Register, ConfirmEmail, Login, Logout, EnrolMfa, Mfa, ForgotPassword, ResetPassword)
      DashboardController.cs          (Index = My Cases)
      CaseController.cs               (Detail with timeline; LetterPdf streaming)
      PropertyClaimController.cs      (Suggestions, Manual, Submit, Status, MyEvidenceDocument)
      NotificationsController.cs      (Index, MarkRead, BellCount)
    Views/
      Shared/_Layout.cshtml           (separate layout — DWS palette but distinct nav)
      Shared/_ViewStart.cshtml        (forces the area layout)
      Account/* Dashboard/* Case/* PropertyClaim/* Notifications/*
    ViewModels/
      (one per controller action group)
    Conventions/
      PortalAuthorizationConvention.cs  (IApplicationModelConvention — applies [Authorize(scheme,policy)] to every controller in the area)

Controllers/Admin/
  PropertyClaimReviewController.cs    (new internal page; [Authorize(Policy=DwsPolicies.RequireValidator)])

Services/
  Portal/
    Auth/
      PublicUserRegistrationService.cs
      PublicUserSignInService.cs
      PublicUserMfaService.cs
      PublicUserRecoveryCodeService.cs
      IPublicUserPropertyAccessor.cs / PublicUserPropertyAccessor.cs
      PortalPolicies.cs               (mirror of DwsPolicies — single Configure(AuthorizationOptions))
      PortalCookieEvents.cs           (CookieAuthenticationEvents subclass for status revalidation)
    Cases/
      CaseTimelineService.cs
      PropertyClaimService.cs         (auto-match + manual flows; portal side)
    Notifications/
      PortalNotificationService.cs    (handles WorkflowAdvancedEvent etc.)
  Admin/
    PropertyClaimReviewService.cs     (DWS-side approve/reject)
  Infrastructure/
    Email/
      IEmailSender.cs
      LoggingEmailSender.cs
    Storage/
      IFileStorage.cs
      LocalDiskFileStorage.cs
```

**Note** — `IEmailSender` and `IFileStorage` live under `Services/Infrastructure/`, not `Services/Portal/`, because they are cross-cutting capabilities the internal portal will eventually need too (ELU certificate emails, mapbook uploads).

### 2.2 Authentication boundary

Two cookie schemes coexist in one ASP.NET Core 10 app:

| Scheme | For | Cookie name | Cookie path | Default ExpireTimeSpan |
|---|---|---|---|---|
| `Identity.Application` (existing) | Internal `ApplicationUser` (DWS staff) | (Identity default) | `/` | (Identity default) |
| `PublicPortalScheme` (new) | Standalone `PublicUser` (water users / landowners) | `.dwa.PortalAuth` | `/ExternalPortal` | 30 min sliding |

Hardening on the new cookie:

- `Cookie.Path = "/ExternalPortal"` — never travels to internal routes.
- `Cookie.Name = ".dwa.PortalAuth"` — distinct from Identity's cookie.
- `Cookie.SecurePolicy = CookieSecurePolicy.Always`.
- `Cookie.HttpOnly = true`.
- `Cookie.SameSite = SameSiteMode.Lax`.
- `SlidingExpiration = true`, `ExpireTimeSpan = TimeSpan.FromMinutes(30)`.
- `LoginPath = "/ExternalPortal/Account/Login"`, `LogoutPath = "/ExternalPortal/Account/Logout"`, `AccessDeniedPath = "/ExternalPortal/Account/AccessDenied"`.

`AddIdentity<>` already sets `DefaultScheme`, `DefaultSignInScheme`, `DefaultChallengeScheme` to `Identity.Application`. The new scheme MUST NOT override those — it is added as an additional scheme via:

```csharp
builder.Services.AddAuthentication() // no default override
    .AddCookie("PublicPortalScheme", PortalCookieOptions.Configure);
```

`PortalAuthorizationConvention` (`IApplicationModelConvention`) automatically applies `[Authorize(AuthenticationSchemes = "PublicPortalScheme", Policy = nameof(PortalPolicies.PortalAuthenticated))]` to every controller in the `ExternalPortal` area, except those whose action is decorated with `[AllowAnonymous]` (Register, ConfirmEmail, Login, ForgotPassword, ResetPassword).

`SignOutAsync` for the portal must call `HttpContext.SignOutAsync("PublicPortalScheme")` directly — `SignInManager.SignOutAsync()` does NOT touch the new scheme.

### 2.3 Authorization policies

| Policy | Requirement |
|---|---|
| `PortalPolicies.PortalAuthenticated` | Authenticated under `PublicPortalScheme` AND `EmailConfirmed=true` claim AND `MfaEnrolled=true` claim AND `Status="Active"` claim |
| `PortalPolicies.PortalRegistrationComplete` | Authenticated under `PublicPortalScheme` AND `EmailConfirmed=true` claim (used ONLY by the MFA enrolment page so a user can complete enrolment) |
| `PortalPolicies.PortalMfaPending` | Authenticated under `PublicPortalScheme` AND `MfaPending=true` claim (used ONLY by the MFA challenge page during a login flow that hasn't completed MFA yet) |
| `DwsPolicies.RequireValidator` (existing, reused) | Internal `Validator+` for the Property Claim Review page |

Single `AddAuthorization()` call in `Program.cs` merging both policy sets:

```csharp
builder.Services.AddAuthorization(options =>
{
    DwsPolicies.Configure(options);
    PortalPolicies.Configure(options);
});
```

`PortalCookieEvents.OnValidatePrincipal` re-checks `PublicUser.Status` from DB on every sliding refresh (≤ once every 30 min) — if user has been suspended, the principal is rejected and they are signed out.

### 2.4 Cross-portal touchpoints

1. **`Program.cs`** — register portal cookie scheme, register portal services, register rate limiter, wire area route, register `IEmailSender` + `IFileStorage`, register `PortalAuthorizationConvention`, register `PortalNotificationService` as MediatR handler.
2. **`DwsClaimsTransformation`** — early-return when `principal.Identity.AuthenticationType != IdentityConstants.ApplicationScheme`, otherwise it would hit the DB on every portal request looking for an `ApplicationUser`.
3. **`WorkflowService`** (existing) — already raises a `WorkflowAdvancedEvent` (or equivalent) after any state transition. New `PortalNotificationService` is added as a handler — for each `PublicUser` with an `Approved` link to the property, insert a `Notification` row and call `IEmailSender.SendAsync`.
4. **`LetterService`** (existing) — when a letter is issued and the recipient is a `PublicUser`, populate the new `LetterIssuance.RecipientPublicUserId` column (legacy rows remain NULL).
5. **Cascade-override loop in `ApplicationDBContext.cs`** — the existing `foreach` that overrides delete behaviour to `Restrict` must be modified to honour an `CascadeFkExemptions` set so `PublicUserRecoveryCode → PublicUser` keeps `Cascade`.

### 2.5 Row-level scoping spine

Every public-portal data query passes through `IPublicUserPropertyAccessor`. Two methods:

```csharp
Task<IReadOnlySet<Guid>> GetApprovedPropertyIdsAsync(Guid publicUserId, CancellationToken ct);
Task AssertHasAccessToFileMasterAsync(Guid publicUserId, Guid fileMasterId, CancellationToken ct);   // throws NotFoundException
```

There is **no other code path** from `Areas/ExternalPortal/` controllers to `Property` / `FileMaster` / `LetterIssuance` / `Document` / `Notification` data. Unauthorised access attempts return **404, not 403**, so we don't leak record existence.

A NetArchTest assertion enforces this: types under `Areas.ExternalPortal.*` cannot reference `UserManager<ApplicationUser>`, `SignInManager<ApplicationUser>`, or `ApplicationDbContext` directly — they go through services in `Services/Portal/*`.

---

## 3. Data model deltas

### 3.1 New columns on `PublicUser`

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `MfaSecret` | `nvarchar(256)` | yes | Base32 TOTP secret, **DataProtection-wrapped** before storage. Purpose string `"PublicUserMfaSecret:v1"`. |
| `MfaEnrolledDate` | `datetime2(0)` | yes | Set at enrolment. |
| `LastLoginDate` | `datetime2(0)` | yes | Updated on successful sign-in. |
| `FailedLoginAttempts` | `int NOT NULL` | no | `HasDefaultValue(0)`. |
| `LockoutUntil` | `datetime2(0)` | yes | Set when `FailedLoginAttempts >= 5`. |
| `LastUsedOtpTimestamp` | `bigint` | yes | Unix-epoch seconds of the last accepted TOTP window — for replay prevention. |
| `HdiConsentGivenDate` | `datetime2(0)` | yes | POPIA Section 26 consent for processing race/gender. **Required** if `IsHDI = true`. |

Indexes:

- `HasIndex(u => u.EmailAddress).IsUnique()` — currently missing; required for fast lookup and to prevent duplicate registration.
- `HasIndex(u => u.IdentityNumber).HasFilter("[IdentityNumber] IS NOT NULL")` — for auto-match.

Check constraints:

- `CK_PublicUsers_HdiConsent`: `IsHDI = 0 OR HdiConsentGivenDate IS NOT NULL`.

### 3.2 New columns on `PublicUserProperty`

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `EvidenceType` | `nvarchar(20) NOT NULL` | no | Stored as string via EF value converter from enum `PropertyClaimEvidenceType { IdMatch, TitleDeedUpload }`. |
| `EvidenceDocumentId` | `uniqueidentifier` | yes | FK → `Documents(DocumentId)`. **Configured before** the cascade-override loop with `OnDelete(DeleteBehavior.SetNull)`. |
| `RequestedDate` | `datetime2(0) NOT NULL` | no | `HasDefaultValueSql("GETUTCDATE()")`. |
| `RejectionReason` | `nvarchar(1000)` | yes | Set when `Status = "Rejected"`. |

`Status` field is also converted from `string` to `PropertyClaimStatus { Pending, Approved, Rejected }` via value converter (existing column type stays `nvarchar(20)`).

Indexes + constraints:

- Filtered unique: `IX_PublicUserProperties_UserId_Property_Active UNIQUE (PublicUserId, PropertyId) WHERE Status <> 'Rejected'` — prevents duplicate active claims.
- Filtered: `IX_PublicUserProperties_Pending (RequestedDate) WHERE Status = 'Pending'` — DWS approval queue.
- Composite covering: `IX_PublicUserProperties_UserId_Status (PublicUserId, Status) INCLUDE (PropertyId)` — My Cases dashboard.
- `CK_PublicUserProperties_EvidenceDocumentId`: `EvidenceType <> 'TitleDeedUpload' OR EvidenceDocumentId IS NOT NULL`.

### 3.3 New columns on `LetterIssuance`

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `RecipientPublicUserId` | `uniqueidentifier` | yes | FK → `PublicUsers(PublicUserId)`. NULL on legacy rows; populated when a letter is issued through the new flow. Letter PDF download authorization keys off this column. |

Index: `IX_LetterIssuances_FileMasterId_IssuedDate (FileMasterId, IssuedDate)` — timeline assembly.

### 3.4 New columns on `Notification`, `PropertyOwner`

No new columns. Just indexes.

- `IX_Notifications_PublicUserId_Unread (PublicUserId) WHERE IsRead = 0` — bell badge.
- `IX_PropertyOwners_IdentityDocumentNumber (IdentityDocumentNumber) WHERE IdentityDocumentNumber IS NOT NULL` — auto-match.

### 3.5 New entity `PublicUserRecoveryCode`

```
Id              uniqueidentifier        PK
PublicUserId    uniqueidentifier        FK → PublicUsers(PublicUserId), Cascade (exempted from override loop)
CodeHash        nvarchar(128)           PasswordHasher-hashed.
Used            bit NOT NULL            Default 0. ConcurrencyToken for atomic redemption.
UsedDate        datetime2(0)            NULL until used.
CreatedDate     datetime2(0) NOT NULL   HasDefaultValueSql("GETUTCDATE()").
ExpiresDate     datetime2(0)            NULL = no expiry; reserved for future policy.
```

Index: `IX_PublicUserRecoveryCodes_PublicUserId_Unused (PublicUserId) WHERE Used = 0`.

Redemption uses `ExecuteUpdateAsync` with `WHERE Id = @id AND Used = 0`; success = rows-affected = 1.

### 3.6 Cascade-override loop adjustment

`ApplicationDBContext.cs` currently has a `foreach` near line 637 that overrides every relationship's delete behaviour to `Restrict`. Modify to:

```csharp
private static readonly HashSet<string> CascadeFkExemptions = new(StringComparer.Ordinal)
{
    "FK_PublicUserRecoveryCodes_PublicUsers_PublicUserId"
};

foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
{
    if (CascadeFkExemptions.Contains(fk.GetConstraintName())) continue;
    fk.DeleteBehavior = DeleteBehavior.Restrict;
}
```

The `PublicUserRecoveryCode` FK is configured with `Cascade` BEFORE the loop, then exempted. This is the only surgical change to the existing cascade discipline.

### 3.7 Migration

Single migration: `ExternalPortalShellPortalAuthAndClaims`. Generated, then **manually reviewed** before apply for:

- `defaultValue: 0` on `FailedLoginAttempts` `AddColumn` operation.
- Filtered-index syntax (`filter: "[Status] <> 'Rejected'"`).
- Check constraint syntax in `migrationBuilder.AddCheckConstraint(...)`.

`Down()` is straightforward: `DropColumn`, `DropTable`, `DropIndex`, `DropCheckConstraint`. Default constraints on `FailedLoginAttempts` are auto-named by EF and dropped implicitly by `DropColumn` on SQL Server 2022.

---

## 4. User flows

The implementation plan for each stage will reference the relevant flows. Key flows summarised below.

### 4.1 Registration → email confirm
Anonymous form → `PublicUser` created with `Status=Pending`, `EmailConfirmed=false`, `HdiConsentGivenDate` set if applicable → DataProtection token (`PortalEmailConfirm:v1`) emailed → confirm link flips `EmailConfirmed=true`, `Status=Active`. Rate-limited: 3 / hour / IP.

### 4.2 First login → forced TOTP enrol
Password verified → cookie issued with `MfaEnrolled=false` + `EmailConfirmed=true` claims (only `PortalRegistrationComplete` policy satisfied; only `/Account/EnrolMfa` reachable) → enrolment generates 32-byte TOTP secret (DataProtection-wrapped) + 10 PasswordHasher-hashed recovery codes → user verifies code → cookie re-issued with full claims → dashboard.

### 4.3 Subsequent login (MFA challenge)
Password OK → 5-min cookie with `MfaPending=true` claim only (`/Account/Mfa` reachable) → 6-digit code verified (window ± 1, replay-prevented via `LastUsedOtpTimestamp`) → full cookie issued → dashboard.

### 4.4 Property claim — auto-suggest
Service queries `PropertyOwner` JOIN `PropertyOwnership` JOIN `Property` WHERE `IdentityDocumentNumber = currentUser.IdentityNumber` → presents un-claimed matches → user ticks → `PublicUserProperty` rows created with `EvidenceType=IdMatch`, `Status=Pending`. **Never auto-grants access.**

### 4.5 Property claim — manual
Search by `Property.SgCode` → upload PDF (≤10 MB, magic-byte sniffed for `application/pdf`) → `IFileStorage.SaveAsync` writes to `{ContentRootPath}/portal-uploads/{yyyy/MM}/{guid}.pdf` → `Document` row with `DocumentType="TitleDeedClaim"`, `VirusScanStatus="Pending"`, SHA-256 hash → `PublicUserProperty` with `EvidenceType=TitleDeedUpload`, `EvidenceDocumentId=...`.

### 4.6 DWS approval queue (internal)
`/Admin/PropertyClaimReview` → filter by status, sort by evidence type → approve / reject with reason → audit-logged → `PortalNotificationService.PublishClaimApproved/Rejected` fires.

### 4.7 My Cases dashboard
Compiled query through `IPublicUserPropertyAccessor.GetApprovedPropertyIdsAsync` → `OUTER APPLY (SELECT TOP 1 ... ORDER BY FileCreatedDate DESC)` to avoid cartesian on properties with multiple FileMasters → DTO projection.

### 4.8 Case detail + timeline
`AssertHasAccessToFileMasterAsync` first → `CaseTimelineService` runs two compiled queries (workflow transitions + letter issuances) and merges in-memory ordered by date desc.

### 4.9 Letter PDF download
Single combined `EXISTS` query enforces both gates atomically: property is in user's approved set AND `LetterIssuance.RecipientPublicUserId = currentUserId`. On false: 404, audit-logged. On true: stream from `IFileStorage`.

### 4.10 Notification fan-out
`WorkflowService` raises domain event → `PortalNotificationService` handler fans out to every `PublicUser` with `Approved` link → `Notification` row inserted, `IEmailSender.SendAsync` invoked, `EmailSent`/`EmailSentDate` set on success.

### 4.11 Lockout
After 5 failed attempts: generic "Login failed" (no enumeration), `LockoutUntil = now + 15min`. Mild escalation: `lockoutMinutes = 15 * min(5, lockoutCountToday)`. Manual unlock via internal portal "Manage Public Users" page (Validator+).

---

## 5. Error handling, security boundaries, hardening

### 5.1 Exception handling

- New `PortalExceptionHandler : IExceptionHandler` registered alongside the existing default handler. For requests under `/ExternalPortal/`:
  - `NotFoundException` → 404 with portal error page.
  - Unhandled `Exception` → 500 with portal error page (no stack trace in production).
  - JSON requests get `application/problem+json` via `AddProblemDetails()`.
- Existing `app.UseExceptionHandler("/Home/Error")` is replaced by the chained `IExceptionHandler` registration so portal exceptions don't route to the internal error page.

### 5.2 Rate limiting

`AddRateLimiter` in `Program.cs` (built-in `Microsoft.AspNetCore.RateLimiting`):

- **Policy `portal-auth-strict`**: sliding window, 5 requests / 15 min / partition-by IP+username for `/Account/Login`, `/Account/Mfa/Verify`.
- **Policy `portal-auth-moderate`**: sliding window, 3 / hour / IP for `/Account/Register`, `/Account/ForgotPassword`.
- **Policy `portal-write-default`**: 30 / minute / public user for all authenticated POSTs.

Wired with `app.UseRateLimiter()` between `UseRouting()` and `UseAuthentication()`.

### 5.3 CSRF

Antiforgery is global (`AddControllersWithViews()`). All portal POSTs use `[ValidateAntiForgeryToken]`. Distinct antiforgery cookie name (`.dwa.AntiForgery`) — fine to share between schemes since the antiforgery service uses its own cookie. Integration test asserts a portal POST without token returns 400.

### 5.4 File serving

- `LocalDiskFileStorage` root: `Path.Combine(env.ContentRootPath, "portal-uploads")`. **Never under `wwwroot/`.**
- Portal file downloads (title deeds for users to view their own evidence; letter PDFs) go through `[Authorize]` controller actions (`CaseController.LetterPdf`, `PropertyClaimController.MyEvidenceDocument`) that re-check property access before streaming.
- DWS staff downloads of evidence PDFs (`PropertyClaimReviewController.EvidenceDocument`) require `DwsPolicies.RequireValidator`.
- `Content-Disposition` always set with explicit filename (no inline rendering of user-uploaded PDFs to limit XSS surface).

### 5.5 Auth event audit logging

Every portal auth event writes to `AuditLogs` via existing `IAuditService`:

- Register, ConfirmEmail, Login (success / failure), Lockout-applied, Logout, MFA-enrol, MFA-verify (success / failure), Recovery-code-used, Recovery-code-regenerated, Password-reset-requested, Password-reset-completed, Claim-requested, Claim-approved, Claim-rejected, Letter-pdf-downloaded, Letter-pdf-download-denied (the 404 case).

### 5.6 POPIA guard rails

- **`IdentityNumber` plaintext fail-fast**: `Program.cs` startup, only when `app.Environment.IsProduction()`:

  ```csharp
  if (!builder.Configuration.GetValue<bool>("Portal:AllowPlaintextIdentityNumber"))
  {
      throw new InvalidOperationException(
          "PublicUser.IdentityNumber is stored unencrypted. Set Portal:AllowPlaintextIdentityNumber=true to acknowledge for non-production data, or wire IDataProtectionProvider encryption (Task 10.3).");
  }
  ```

- **`HdiConsentGivenDate` enforced** by `CHECK` constraint and registration form gate. UI checkbox text must specifically name HDI / race / gender, not be bundled in T&Cs.
- **`LoggingEmailSender` PII warning**: startup logs `WARN` if `LoggingEmailSender` is in use AND `IsProduction()` — must be replaced before production data lands.
- **DataProtection key persistence**: file-based by default in dev; flagged in spec as a Task 2.6 prerequisite (Key Vault + `PersistKeysToAzureBlobStorage` + `ProtectKeysWithAzureKeyVault`) before any production deployment, otherwise email-confirm and password-reset tokens regenerate on App Service restart.

### 5.7 NetArchTest fence

A `Tests/Architecture/PortalBoundaryTests.cs` test class enforces:

- Types in `Areas.ExternalPortal.*` cannot depend on `UserManager<ApplicationUser>`, `SignInManager<ApplicationUser>`, or `ApplicationDbContext`.
- Types in `Services/Portal/*` cannot depend on `UserManager<ApplicationUser>`, `SignInManager<ApplicationUser>`.

The test fails the build if violated.

---

## 6. Testing strategy

### 6.1 Unit tests

| Service | Cases |
|---|---|
| `PublicUserRegistrationService` | Email uniqueness, password strength, ID Luhn, HDI consent gating, token generation/validation, email dispatch invocation |
| `PublicUserSignInService` | Successful login, wrong password increments counter, 5th failure sets lockout, lockout escalation, generic-error invariant (no enumeration), `LastLoginDate` update on success |
| `PublicUserMfaService` | Enrolment generates 32-byte secret, DataProtection round-trip, QR URL format, ±1 window verification, replay rejected via `LastUsedOtpTimestamp` |
| `PublicUserRecoveryCodeService` | 10 codes generated, hashed with PasswordHasher, atomic redemption (`ExecuteUpdateAsync` with `WHERE Used=0`), regenerate invalidates old codes, audit log entry |
| `PropertyClaimService` (auto-match path) | Returns properties matching current user's `IdentityDocumentNumber`, excludes already-claimed, returns empty when ID number is NULL |
| `PropertyClaimService` (manual path) | Document magic-byte check, file-size limit, evidence document linked correctly, claim row created with correct EvidenceType |
| `PropertyClaimReviewService` (DWS side) | Approve sets ApprovedByUserId/Date, reject requires reason, status transition fires notification |
| `CaseTimelineService` | Workflow + letter rows merged correctly, ordered by date desc, deduplication where applicable |
| `PublicUserPropertyAccessor` | Returns only `Status=Approved` rows, returns empty for unknown user, `AssertHasAccessToFileMasterAsync` throws `NotFoundException` for non-linked FileMaster |
| `PortalNotificationService` | Subscribes to `WorkflowAdvancedEvent`, fans out to every approved-linked user, sets `EmailSent` flag on `IEmailSender` success |

### 6.2 Integration tests (`WebApplicationFactory` + shared SQL fixture)

Set up a SQL fixture (Docker SQL Server) with seed data: 1 internal Validator, 2 public users, 3 properties (1 owned by user A via PropertyOwner ID match, 1 manual-claimable, 1 owned by neither).

| Test | Asserts |
|---|---|
| Happy path: register → confirm → login → enrol MFA → claim by ID → DWS approves → see case in dashboard → open case → download letter | All steps succeed; final letter response is HTTP 200 with PDF body |
| Login wrong password 5 times | 5th attempt = locked out; correct password 14 min later still rejected; correct password 16 min later succeeds |
| Cross-scheme leakage: portal cookie sent to `/Admin/*` | Internal scheme rejects portal cookie; redirects to internal login |
| Cross-scheme leakage: internal cookie sent to `/ExternalPortal/Dashboard` | Portal scheme rejects; redirects to portal login |
| Anonymous POST to `/ExternalPortal/Account/Login` without antiforgery token | 400 |
| Authenticated user A tries to fetch FileMaster ID belonging to user B's property | 404 (not 403) |
| Authenticated user A tries to download letter PDF where they are not the addressee | 404 (not 403); `AuditLogs` has a `Letter-pdf-download-denied` entry |
| Workflow advance triggers notification | After `WorkflowService.AdvanceAsync`, `Notifications` table has a new row for user A and `IEmailSender.SendAsync` was called |
| Rate limit on `/Account/Login` | 6th request inside 15-min window returns 429 |

### 6.3 Architecture tests

`Tests/Architecture/PortalBoundaryTests.cs` — NetArchTest assertions per §5.7.

### 6.4 Manual UI smoke (per stage gate)

Each stage merges with a manual smoke checklist in the PR description: register → confirm → enrol MFA → log out → log in with MFA → claim a property → switch to internal account → approve the claim → switch back to portal → see the property → open the case → download the letter PDF.

---

## 7. Staged delivery

The work is split into five mergeable stages so each is review-able and deployable independently. Each stage has its own implementation plan file under `docs/superpowers/plans/`, its own PR, and its own tests.

| Stage | Scope | Mergeable outcome |
|---|---|---|
| **Stage 1 — Foundations** | Schema delta + migration + Program.cs auth wiring (PublicPortalScheme cookie + DwsClaimsTransformation fix + AddRateLimiter + IExceptionHandler + single AddAuthorization) + `IEmailSender`/`LoggingEmailSender` + `IFileStorage`/`LocalDiskFileStorage` + `IPublicUserPropertyAccessor` + NetArchTest fence + integration test SQL fixture | Build green, tests green, no UI yet. Schema in place, auth boundary wired and tested, infrastructure abstractions ready. |
| **Stage 2 — Registration & MFA** | `Areas/ExternalPortal/` shell + layout + `AccountController` (Register, ConfirmEmail, Login, Logout, EnrolMfa, Mfa, ForgotPassword, ResetPassword) + `PortalAuthorizationConvention` + lockout + recovery codes + audit logging | A user can register, confirm email, enrol MFA, log in, log out, reset password. Lands on an empty dashboard. |
| **Stage 3 — Property linking & DWS approval** | `PropertyClaimController` (Suggestions, Manual, Submit, Status) + internal `PropertyClaimReviewController` + DWS approval queue UI + evidence document upload + authenticated evidence-document download | A user can request a property link via auto-suggest or manual upload. DWS staff can review and approve/reject. |
| **Stage 4 — Dashboard, case detail & timeline** | `DashboardController` + `CaseController` + `CaseTimelineService` + letter PDF download with auth gate | A user with an approved property link sees their case in the dashboard, opens the case detail page, sees the timeline, and can download letters addressed to them. |
| **Stage 5 — Notifications** | `PortalNotificationService` + `WorkflowAdvancedEvent` subscription + `NotificationsController` + bell badge + `IEmailSender` invocation on workflow advance, letter issuance, claim approval/rejection | Every relevant case event creates an in-app notification and triggers an email send (logged in dev). End of feature. |

After each stage, the rollout-plan row gets marked Status / Date / Notes and committed alongside the code (per the user's auto-memory feedback `feedback_rollout_plan.md`).

---

## 8. Open items / future work

- **Real `IEmailSender` implementation** (SendGrid / Azure Communication Services / DWS SMTP relay) — Task 2.6 dependency.
- **Azure Blob Storage for `IFileStorage`** — Task 2.4.
- **Virus scanning** on uploads (ClamAV or Microsoft Defender for Storage) — future.
- **`IdentityNumber` encryption at rest** — Task 10.3.
- **DataProtection key persistence to Key Vault** — Task 2.6.
- **SMS OTP as MFA factor** — Task 2.6 + provider procurement.
- **Comment / letter response submission by user** — Task 9.3 follow-up.
- **Objection lodging UI + DWS objection report** — Task 5.4 (reduced scope: lodge + documents + DWS report only).
- **Status-string → enum migration for the remaining fields** — backlog.
- **Temporal tables for `PublicUser` / `PublicUserRecoveryCode`** — future POPIA hardening.
- **`PropertyClaimReviewController` UI on a "Manage Public Users" page** for manual lockout unlock — to be added in Stage 2 or 3 depending on scheduling.

---

## 9. Critique trail

Three specialist agents reviewed Sections 1 + 2 of this design before it was finalised; the critique trail and synthesis is recorded in the task journal:

`docs/superpowers/tasks/2026-05-03-external-portal-shell/journal.md`

All `BLOCKING` and `HIGH` severity items raised by the three agents have been folded into Sections 2-7 of this spec.
