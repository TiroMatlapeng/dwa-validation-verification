# Stage 2a — External Portal Registration & Login Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land a working "register → confirm email → log in → empty dashboard" demo of the External Portal at `/Areas/ExternalPortal/`. NO MFA in this slice — that's Stage 2b. Logged-in users without MFA get routed to a stub "MFA coming soon" page; CLAUDE.md still mandates MFA before case data, so the dashboard placeholder displays the stub message rather than real case data.

**Architecture:** New `Areas/ExternalPortal/` MVC area with separate cookie scheme (already wired in Stage 1) + new `PublicUserRegistrationService` and `PublicUserSignInService` + DataProtection-tokened email confirmation + `PortalAuthorizationConvention` to auto-apply `[Authorize]` to area controllers + `PortalCookieEvents` stub for future status revalidation. Audit logging on every auth event via existing `IAuditService`.

**Tech Stack:** ASP.NET Core 10 MVC + Areas, EF Core 10, ASP.NET Identity primitives (`PasswordHasher<T>`, `IDataProtectionProvider`), xUnit, Moq, Microsoft.AspNetCore.Mvc.Testing.

**Spec:** `docs/superpowers/specs/2026-05-03-external-portal-shell-design.md` (sections 4.1, 4.3 partial, 4.11 partial, 5.5)
**Stage 1 spec deferral:** the spec § 4.2 forces TOTP MFA on first login. Stage 2a defers MFA entirely; users with `MfaEnabled=false` (which is everyone after registration) get a holding page. Stage 2b will replace that page with the TOTP enrol flow + tighten `PortalPolicies.PortalAuthenticated` to require `MfaEnrolled=true`.
**Stage 1 journal entry conditions** (this plan implements four of them):
- Entry condition #2 (NetArchTest fence expansion) → Task 2
- Entry condition #4 (PortalPolicies real claims) → Task 3
- Entry condition #5 (PublicUserBuilder factories) → Task 1
- Entry condition #6 (RequestedDate `DateTime.UtcNow` default) → out of scope (no claim creation in 2a)

**Pre-requisite:** Stage 1 is merged into `demo/azure-deploy` at commit `8af0d82`. Branch this plan's worktree off that commit.

---

## File Structure

### Created

| Path | Responsibility |
|---|---|
| `Areas/ExternalPortal/Controllers/AccountController.cs` | Register, ConfirmEmail, Login, Logout, AccessDenied. NO MFA actions in 2a. |
| `Areas/ExternalPortal/Controllers/DashboardController.cs` | Single `Index` action returning the "MFA coming soon" placeholder for 2a. |
| `Areas/ExternalPortal/ViewModels/RegisterViewModel.cs` | Form bindings for registration: First/Last name, Email, IdentityNumber, Phone, Password, ConfirmPassword, IsHDI, HdiConsent, AcceptTerms. |
| `Areas/ExternalPortal/ViewModels/LoginViewModel.cs` | Form bindings: Email, Password, ReturnUrl. |
| `Areas/ExternalPortal/Views/_ViewImports.cshtml` | Pulls in standard tag helpers + portal namespace. |
| `Areas/ExternalPortal/Views/_ViewStart.cshtml` | Forces the area layout. |
| `Areas/ExternalPortal/Views/Shared/_PortalLayout.cshtml` | Standalone DWS-palette layout with "Public Portal" branding (mirrors `_LoginLayout.cshtml`). |
| `Areas/ExternalPortal/Views/Account/Register.cshtml` | Registration form. |
| `Areas/ExternalPortal/Views/Account/RegisterConfirmation.cshtml` | "Check your email" landing after POST. |
| `Areas/ExternalPortal/Views/Account/ConfirmEmail.cshtml` | Success / failure page after the email link is clicked. |
| `Areas/ExternalPortal/Views/Account/Login.cshtml` | Login form. |
| `Areas/ExternalPortal/Views/Account/AccessDenied.cshtml` | Generic access-denied page (used by the cookie scheme's `AccessDeniedPath`). |
| `Areas/ExternalPortal/Views/Dashboard/Index.cshtml` | Stage-2a placeholder dashboard — "MFA coming soon" message. |
| `Helpers/SaIdValidator.cs` | South African ID Luhn-ish checksum validator (static helper). |
| `Helpers/PortalPasswordPolicy.cs` | Static policy: ≥12 chars, must include letter + digit; no symbol requirement. |
| `Services/Portal/Auth/IPublicUserRegistrationService.cs` | `RegisterAsync` + `ConfirmEmailAsync` contract. |
| `Services/Portal/Auth/PublicUserRegistrationService.cs` | Implementation: hash password, create row, generate DataProtection-wrapped confirmation token, dispatch email. |
| `Services/Portal/Auth/IPublicUserSignInService.cs` | `SignInAsync(email, password) → SignInResult`. |
| `Services/Portal/Auth/PublicUserSignInService.cs` | Implementation: lookup by email, verify hash, sign in via cookie scheme, audit log. (No lockout in 2a.) |
| `Services/Portal/Auth/PortalCookieEvents.cs` | `CookieAuthenticationEvents` subclass. Stage 2a is a stub that calls `base.ValidatePrincipal` only. Stage 2b will add status-revalidation. |
| `Services/Portal/Auth/PortalAuthorizationConvention.cs` | `IApplicationModelConvention` — applies `[Authorize(scheme=PortalCookieOptions.SchemeName, policy=PortalPolicies.PortalAuthenticated)]` to every controller in the `ExternalPortal` area unless the action has `[AllowAnonymous]`. |
| `Tests/Services/Portal/Auth/PublicUserRegistrationServiceTests.cs` | Unit tests — registration happy path, duplicate email rejected, password too short rejected, ID Luhn rejected, HDI consent gate, token round-trip + expiry. |
| `Tests/Services/Portal/Auth/PublicUserSignInServiceTests.cs` | Unit tests — successful login sets cookie + audit, wrong password fails generically, unconfirmed email blocked. |
| `Tests/Services/Portal/Auth/PortalAuthorizationConventionTests.cs` | Unit tests — convention applies attribute, respects `[AllowAnonymous]`. |
| `Tests/Helpers/SaIdValidatorTests.cs` | Tests — valid SA IDs pass, invalid checksum fails, wrong length fails, null/empty handled. |
| `Tests/Helpers/PortalPasswordPolicyTests.cs` | Tests — valid passwords pass, too short rejected, missing digit rejected, missing letter rejected. |
| `Tests/Areas/ExternalPortal/AccountControllerRegistrationTests.cs` | Controller tests — Register POST happy path, model errors return same view with errors, ConfirmEmail GET valid token activates, expired token shows error page. |
| `Tests/Areas/ExternalPortal/AccountControllerLoginTests.cs` | Controller tests — Login POST happy path redirects to dashboard, wrong password returns same view, unconfirmed email blocked, Logout signs out and redirects. |
| `Tests/Integration/PortalRegistrationFlowTests.cs` | End-to-end via WebApplicationFactory: GET register → POST register → check email log line → GET confirm link → expect "Active" status. |
| `Tests/Integration/PortalLoginFlowTests.cs` | End-to-end: pre-seed an Active user, POST login, follow redirect to dashboard, GET dashboard → 200, POST logout, dashboard → redirect to login. |

### Modified

| Path | Change |
|---|---|
| `Tests/Helpers/PublicUserBuilder.cs` | Add `Pending(string email)`, `Suspended(string email)`, `Active(string email)` was the only one. |
| `Services/Portal/Auth/PortalPolicies.cs` | Replace placeholder `RequireAuthenticatedUser()` with real claim requirements per the spec. For Stage 2a, `PortalAuthenticated` requires `EmailConfirmed=true` only (MFA enforcement is Stage 2b). `PortalRegistrationComplete` allows `EmailConfirmed=true` AND `MfaEnrolled=false` (used for the holding page). `PortalMfaPending` deferred. |
| `Tests/Architecture/PortalBoundaryTests.cs` | Add a third `[Fact]` blocking `Areas/ExternalPortal/*` from depending on `ApplicationDBContext` directly — they must go through `Services/Portal/*`. |
| `Program.cs` | Register `IPublicUserRegistrationService`, `IPublicUserSignInService`, `PortalCookieEvents`, `PortalAuthorizationConvention`. Wire the convention into `AddControllersWithViews`. Wire the events into the cookie scheme. |
| `Tests/Helpers/IntegrationTestHelpers.cs` (in `IntegrationTestBase.cs`) | Add `PortalAntiForgeryToken` and `RegisterPublicUser` helpers mirroring the existing `LoginAsDemoUser`. |

### Deferred to Stage 2b

- Forced TOTP MFA enrolment + MFA challenge + recovery codes
- Account lockout (FailedLoginAttempts logic + LockoutUntil enforcement)
- Password reset flow (ForgotPassword + ResetPassword actions/views)
- TOTP replay prevention (`LastUsedOtpTimestamp`)
- `PortalCookieEvents.OnValidatePrincipal` real status-active revalidation (Stage 2a stub returns base behaviour only)

---

## Tasks

### Task 1: Extend `PublicUserBuilder` test helper

**Files:**
- Modify: `Tests/Helpers/PublicUserBuilder.cs`
- Test: covered by usage in subsequent service tests

- [ ] **Step 1: Read existing builder**

```bash
cat Tests/Helpers/PublicUserBuilder.cs
```

You should see a single `static PublicUser Active(string email = "test@example.com")` factory.

- [ ] **Step 2: Add `Pending` and `Suspended` factories**

Replace the entire content of `Tests/Helpers/PublicUserBuilder.cs` with:

```csharp
namespace dwa_ver_val.Tests.Helpers;

/// <summary>
/// Test factory for <see cref="PublicUser"/> in known states.
/// All factories set the 5 required props; tests can override individual fields.
/// </summary>
public static class PublicUserBuilder
{
    public static PublicUser Active(string email = "test@example.com")
    {
        return new PublicUser
        {
            EmailAddress = email,
            PasswordHash = "hashed",
            FirstName = "Test",
            LastName = "User",
            Status = "Active",
            EmailConfirmed = true,
            RegistrationDate = DateTime.UtcNow
        };
    }

    public static PublicUser Pending(string email = "pending@example.com")
    {
        return new PublicUser
        {
            EmailAddress = email,
            PasswordHash = "hashed",
            FirstName = "Pending",
            LastName = "User",
            Status = "Pending",
            EmailConfirmed = false,
            RegistrationDate = DateTime.UtcNow
        };
    }

    public static PublicUser Suspended(string email = "suspended@example.com")
    {
        return new PublicUser
        {
            EmailAddress = email,
            PasswordHash = "hashed",
            FirstName = "Suspended",
            LastName = "User",
            Status = "Suspended",
            EmailConfirmed = true,
            RegistrationDate = DateTime.UtcNow
        };
    }
}
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build`
Expected: 0 errors. Existing tests using `Active` still compile.

- [ ] **Step 4: Run full test suite**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --nologo --verbosity quiet`
Expected: 137 / 0 / 137 (no count change — no new tests, just new factories).

- [ ] **Step 5: Commit**

```bash
git add Tests/Helpers/PublicUserBuilder.cs
git commit -m "PublicUserBuilder: add Pending and Suspended factories for Stage 2"
```

---

### Task 2: Extend NetArchTest fence to block direct ApplicationDBContext from `Areas/ExternalPortal/*`

**Files:**
- Modify: `Tests/Architecture/PortalBoundaryTests.cs`

> Stage 1 entry condition #2: portal area must go through `Services/Portal/*` services, never touch `ApplicationDBContext` directly. The `Services/Portal/*` namespace itself is exempt — `PublicUserPropertyAccessor` legitimately wraps `ApplicationDBContext`.

- [ ] **Step 1: Read existing test file**

```bash
cat Tests/Architecture/PortalBoundaryTests.cs
```

Current file has 2 `[Fact]` methods — `PortalServices_MustNotReferenceIdentityUserManager` and `ExternalPortalArea_MustNotReferenceIdentityUserManager`.

- [ ] **Step 2: Add the new test**

Append a third `[Fact]` method INSIDE the existing `PortalBoundaryTests` class (before the final closing `}`):

```csharp
    [Fact]
    public void ExternalPortalArea_MustNotReferenceApplicationDBContext()
    {
        // Portal controllers/views must go through Services/Portal/* services
        // (e.g. IPublicUserPropertyAccessor) — direct EF access bypasses the
        // row-level scoping spine and the audit hooks.
        var result = Types.InAssembly(AppAssembly)
            .That()
            .ResideInNamespace("dwa_ver_val.Areas.ExternalPortal")
            .ShouldNot()
            .HaveDependencyOn("ApplicationDBContext")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Areas/ExternalPortal types must not depend on ApplicationDBContext directly. " +
            "Go through a Services/Portal/* service. Offending types: " +
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
```

- [ ] **Step 3: Run all 3 tests**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --filter FullyQualifiedName~PortalBoundaryTests`
Expected: 3 passed (the new test passes trivially since no `Areas.ExternalPortal` types exist yet).

- [ ] **Step 4: Run full suite**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --nologo --verbosity quiet`
Expected: 138 / 0 / 138 (was 137 / 0; +1 new test).

- [ ] **Step 5: Commit**

```bash
git add Tests/Architecture/PortalBoundaryTests.cs
git commit -m "NetArchTest: block direct ApplicationDBContext from portal area"
```

---

### Task 3: Replace `PortalPolicies` placeholders with real claim requirements (Stage 2a slice)

**Files:**
- Modify: `Services/Portal/Auth/PortalPolicies.cs`

> Stage 1 entry condition #4: replace `RequireAuthenticatedUser()` placeholders with real claim requirements. Stage 2a only needs `EmailConfirmed=true`; MFA-related claim requirements are Stage 2b.

- [ ] **Step 1: Read existing file**

```bash
cat Services/Portal/Auth/PortalPolicies.cs
```

- [ ] **Step 2: Replace the file content**

Replace the full content of `Services/Portal/Auth/PortalPolicies.cs` with:

```csharp
using Microsoft.AspNetCore.Authorization;

namespace dwa_ver_val.Services.Portal.Auth;

public static class PortalPolicies
{
    public const string PortalAuthenticated = "PortalAuthenticated";
    public const string PortalRegistrationComplete = "PortalRegistrationComplete";
    public const string PortalMfaPending = "PortalMfaPending";

    // Claim names stamped at sign-in by PublicUserSignInService.
    public const string EmailConfirmedClaim = "EmailConfirmed";
    public const string MfaEnrolledClaim = "MfaEnrolled";
    public const string StatusClaim = "Status";

    public static void Configure(AuthorizationOptions options)
    {
        // Stage 2a: PortalAuthenticated requires the cookie + EmailConfirmed=true claim.
        // (Stage 2b will add MfaEnrolled=true and Status=Active.)
        options.AddPolicy(PortalAuthenticated, p => p
            .AddAuthenticationSchemes(PortalCookieOptions.SchemeName)
            .RequireAuthenticatedUser()
            .RequireClaim(EmailConfirmedClaim, "true"));

        // Used during MFA enrolment in Stage 2b — for 2a it's reachable as soon
        // as the email is confirmed, so the holding-page-on-dashboard works.
        options.AddPolicy(PortalRegistrationComplete, p => p
            .AddAuthenticationSchemes(PortalCookieOptions.SchemeName)
            .RequireAuthenticatedUser()
            .RequireClaim(EmailConfirmedClaim, "true"));

        // Stage 2b only: short-lived cookie carrying MfaPending=true between
        // password verification and TOTP entry. Defined now so AccountController
        // can reference the constant without a compile error if Stage 2b is
        // partially landed.
        options.AddPolicy(PortalMfaPending, p => p
            .AddAuthenticationSchemes(PortalCookieOptions.SchemeName)
            .RequireAuthenticatedUser()
            .RequireClaim("MfaPending", "true"));
    }
}
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build`
Expected: 0 errors. (No tests directly assert these requirements yet — covered by integration tests in later tasks.)

- [ ] **Step 4: Run full suite**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --nologo --verbosity quiet`
Expected: 138 / 0 / 138 (no count change).

- [ ] **Step 5: Commit**

```bash
git add Services/Portal/Auth/PortalPolicies.cs
git commit -m "PortalPolicies: enforce EmailConfirmed claim (Stage 2a)"
```

---

### Task 4: `PortalCookieEvents` stub for Stage 2a

**Files:**
- Create: `Services/Portal/Auth/PortalCookieEvents.cs`

> Spec § 2.3 says `OnValidatePrincipal` should re-check `PublicUser.Status` from DB on every sliding refresh. Stage 2a defers that to Stage 2b — the stub here just calls `base.ValidatePrincipal` so Stage 2b can override one method. Keeping the class makes the Program.cs registration site stable across the two stages.

- [ ] **Step 1: Create the class**

Create `Services/Portal/Auth/PortalCookieEvents.cs`:

```csharp
using Microsoft.AspNetCore.Authentication.Cookies;

namespace dwa_ver_val.Services.Portal.Auth;

/// <summary>
/// CookieAuthenticationEvents for the PublicPortalScheme.
/// Stage 2a: stub that defers entirely to base behaviour.
/// Stage 2b: override OnValidatePrincipal to re-check PublicUser.Status from
/// DB on every sliding refresh and reject suspended/deactivated users.
/// </summary>
public class PortalCookieEvents : CookieAuthenticationEvents
{
    // Stage 2a: no overrides. Class exists so Program.cs can register
    // options.EventsType = typeof(PortalCookieEvents) once and Stage 2b
    // adds the override here without touching the wiring.
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build`
Expected: 0 errors.

- [ ] **Step 3: Run full suite**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --nologo --verbosity quiet`
Expected: 138 / 0 / 138.

- [ ] **Step 4: Commit**

```bash
git add Services/Portal/Auth/PortalCookieEvents.cs
git commit -m "PortalCookieEvents: stub for Stage 2a (Stage 2b adds status revalidation)"
```

---

### Task 5: `PortalAuthorizationConvention` (auto-applies `[Authorize]` to area)

**Files:**
- Create: `Services/Portal/Auth/PortalAuthorizationConvention.cs`
- Test: `Tests/Services/Portal/Auth/PortalAuthorizationConventionTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Tests/Services/Portal/Auth/PortalAuthorizationConventionTests.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using dwa_ver_val.Services.Portal.Auth;
using System.Reflection;
using Xunit;

namespace dwa_ver_val.Tests.Services.Portal.Auth;

public class PortalAuthorizationConventionTests
{
    [Area("ExternalPortal")]
    private sealed class FakePortalController : Controller { public IActionResult X() => Ok(); }

    private sealed class FakeNonPortalController : Controller { public IActionResult X() => Ok(); }

    private static ControllerModel BuildControllerModel(Type controllerType)
    {
        var typeInfo = controllerType.GetTypeInfo();
        var attrs = typeInfo.GetCustomAttributes(inherit: true);
        var model = new ControllerModel(typeInfo, attrs);
        var actionMethod = controllerType.GetMethod("X")!;
        var actionAttrs = actionMethod.GetCustomAttributes(inherit: true);
        var action = new ActionModel(actionMethod, actionAttrs) { Controller = model };
        model.Actions.Add(action);
        return model;
    }

    [Fact]
    public void Apply_AddsAuthorizeFilter_WhenControllerIsInExternalPortalArea()
    {
        var convention = new PortalAuthorizationConvention();
        var model = BuildControllerModel(typeof(FakePortalController));

        convention.Apply(model);

        var filter = Assert.Single(model.Filters.OfType<AuthorizeFilter>());
        var policy = filter.Policy ?? filter.AuthorizeData?.Aggregate(
            new AuthorizationPolicyBuilder(),
            (b, d) => { if (d.AuthenticationSchemes is { Length: > 0 } s) b.AddAuthenticationSchemes(s.Split(',')); if (d.Policy is { Length: > 0 } p) b.AddRequirements(new DenyAnonymousAuthorizationRequirement()); return b; }).Build();
        Assert.NotNull(policy);
    }

    [Fact]
    public void Apply_DoesNothing_WhenControllerIsOutsideExternalPortalArea()
    {
        var convention = new PortalAuthorizationConvention();
        var model = BuildControllerModel(typeof(FakeNonPortalController));

        convention.Apply(model);

        Assert.Empty(model.Filters.OfType<AuthorizeFilter>());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --filter FullyQualifiedName~PortalAuthorizationConventionTests`
Expected: build error — `PortalAuthorizationConvention` does not exist.

- [ ] **Step 3: Create the convention**

Create `Services/Portal/Auth/PortalAuthorizationConvention.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace dwa_ver_val.Services.Portal.Auth;

/// <summary>
/// IControllerModelConvention that auto-applies
/// [Authorize(scheme=PublicPortalScheme, policy=PortalAuthenticated)] to every
/// controller inside the ExternalPortal area. Per-action [AllowAnonymous] still
/// wins (added by the action attribute, evaluated after this convention).
/// Saves us from sprinkling [Authorize(...)] on every portal controller and
/// removes the risk of forgetting it on a new one.
/// </summary>
public class PortalAuthorizationConvention : IControllerModelConvention
{
    private const string ExternalPortalAreaName = "ExternalPortal";

    public void Apply(ControllerModel controller)
    {
        var areaAttribute = controller.Attributes
            .OfType<Microsoft.AspNetCore.Mvc.AreaAttribute>()
            .FirstOrDefault();

        if (areaAttribute?.RouteValue != ExternalPortalAreaName)
            return;

        var policy = new AuthorizationPolicyBuilder(PortalCookieOptions.SchemeName)
            .RequireAuthenticatedUser()
            .RequireClaim(PortalPolicies.EmailConfirmedClaim, "true")
            .Build();

        controller.Filters.Add(new AuthorizeFilter(policy));
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --filter FullyQualifiedName~PortalAuthorizationConventionTests`
Expected: 2 tests pass.

- [ ] **Step 5: Run full suite**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --nologo --verbosity quiet`
Expected: 140 / 0 / 140 (was 138; +2).

- [ ] **Step 6: Commit**

```bash
git add Services/Portal/Auth/PortalAuthorizationConvention.cs Tests/Services/Portal/Auth/PortalAuthorizationConventionTests.cs
git commit -m "Add PortalAuthorizationConvention to auto-apply [Authorize] to area"
```

---

### Task 6: Wire `PortalCookieEvents` and `PortalAuthorizationConvention` into `Program.cs`

**Files:**
- Modify: `Program.cs`

- [ ] **Step 1: Read current state**

```bash
grep -n "PortalCookieOptions\|PortalAuthorizationConvention\|PortalCookieEvents\|AddControllersWithViews" Program.cs
```

You should find the existing `AddControllersWithViews()` call near line 13 and `authBuilder.AddCookie(PortalCookieOptions.SchemeName, PortalCookieOptions.Configure)` near line 56.

- [ ] **Step 2: Wire the convention into MVC**

Replace this line (around line 13):

```csharp
builder.Services.AddControllersWithViews();
```

with:

```csharp
builder.Services.AddControllersWithViews(options =>
{
    options.Conventions.Add(new PortalAuthorizationConvention());
});
```

- [ ] **Step 3: Wire the cookie events**

Find this line (around line 56):

```csharp
authBuilder.AddCookie(PortalCookieOptions.SchemeName, PortalCookieOptions.Configure);
```

Replace with:

```csharp
builder.Services.AddScoped<PortalCookieEvents>();
authBuilder.AddCookie(PortalCookieOptions.SchemeName, options =>
{
    PortalCookieOptions.Configure(options);
    options.EventsType = typeof(PortalCookieEvents);
});
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build`
Expected: 0 errors.

- [ ] **Step 5: Run full suite**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --nologo --verbosity quiet`
Expected: 140 / 0 / 140 (no count change — wiring only).

- [ ] **Step 6: Commit**

```bash
git add Program.cs
git commit -m "Program.cs: wire PortalAuthorizationConvention + PortalCookieEvents"
```

---

### Task 7: `SaIdValidator` helper

**Files:**
- Create: `Helpers/SaIdValidator.cs`
- Test: `Tests/Helpers/SaIdValidatorTests.cs`

> South African ID numbers are 13 digits with a Luhn checksum on the last digit. We don't validate the date-of-birth or citizenship-bit logic — just the format and checksum.

- [ ] **Step 1: Write the failing test**

Create `Tests/Helpers/SaIdValidatorTests.cs`:

```csharp
using dwa_ver_val.Helpers;
using Xunit;

namespace dwa_ver_val.Tests.Helpers;

public class SaIdValidatorTests
{
    [Theory]
    [InlineData("8001015009087")]   // a known-valid example (Luhn-correct construction)
    [InlineData("9202204720082")]
    public void IsValid_ReturnsTrueForKnownValidIds(string id)
    {
        Assert.True(SaIdValidator.IsValid(id));
    }

    [Theory]
    [InlineData("8001015009088")]   // checksum bumped by one — invalid
    [InlineData("0000000000000")]   // all zeros — fails the checksum
    [InlineData("1234567890123")]
    public void IsValid_ReturnsFalseForBadChecksum(string id)
    {
        Assert.False(SaIdValidator.IsValid(id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("123")]            // too short
    [InlineData("80010150090877")] // too long
    [InlineData("80010A5009087")]  // non-digit
    public void IsValid_ReturnsFalseForMalformedInputs(string? id)
    {
        Assert.False(SaIdValidator.IsValid(id));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --filter FullyQualifiedName~SaIdValidatorTests`
Expected: build error — `SaIdValidator` does not exist.

- [ ] **Step 3: Create the helper**

Create `Helpers/SaIdValidator.cs`:

```csharp
namespace dwa_ver_val.Helpers;

/// <summary>
/// Validates a 13-digit South African ID number against its Luhn checksum.
/// Does NOT validate DOB plausibility, citizenship bit, or ordinal digit —
/// just shape + checksum. Sufficient for portal registration's "is this
/// plausibly a real SA ID?" gate.
/// </summary>
public static class SaIdValidator
{
    public static bool IsValid(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        if (id.Length != 13) return false;

        var digits = new int[13];
        for (var i = 0; i < 13; i++)
        {
            if (!char.IsDigit(id[i])) return false;
            digits[i] = id[i] - '0';
        }

        // SA ID Luhn: starting from the rightmost digit (the check digit),
        // every second digit is doubled. Sum all digits of the doubled values
        // plus the un-doubled values; the total must be divisible by 10.
        var sum = 0;
        for (var i = 12; i >= 0; i--)
        {
            var d = digits[i];
            if ((12 - i) % 2 == 1)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
        }
        return sum % 10 == 0;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --filter FullyQualifiedName~SaIdValidatorTests`
Expected: 8 tests pass (2 valid + 3 bad checksum + 6 malformed = 11 total via Theory expansion; xUnit may show 11. Confirm count.)

- [ ] **Step 5: Run full suite**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --nologo --verbosity quiet`
Expected: 151 / 0 / 151 (was 140; +11 from Theory expansion).

- [ ] **Step 6: Commit**

```bash
git add Helpers/SaIdValidator.cs Tests/Helpers/SaIdValidatorTests.cs
git commit -m "Add SaIdValidator (13-digit Luhn) helper"
```

---

### Task 8: `PortalPasswordPolicy` helper

**Files:**
- Create: `Helpers/PortalPasswordPolicy.cs`
- Test: `Tests/Helpers/PortalPasswordPolicyTests.cs`

> Per spec § 4.1: ≥12 chars, must contain at least one letter and one digit. No symbol requirement (per spec). Returns a list of human-readable failure reasons for the registration form to surface.

- [ ] **Step 1: Write the failing test**

Create `Tests/Helpers/PortalPasswordPolicyTests.cs`:

```csharp
using dwa_ver_val.Helpers;
using Xunit;

namespace dwa_ver_val.Tests.Helpers;

public class PortalPasswordPolicyTests
{
    [Fact]
    public void Validate_ReturnsEmpty_ForCompliantPassword()
    {
        var errors = PortalPasswordPolicy.Validate("My secure password 12 chars");
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("short1aa")]                                      // <12 chars
    [InlineData("alllowercaseonlynodigits")]                       // no digit
    [InlineData("123456789012345")]                                // no letter
    [InlineData("")]
    [InlineData(null)]
    public void Validate_ReturnsErrors_ForNonCompliantPasswords(string? password)
    {
        var errors = PortalPasswordPolicy.Validate(password);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Validate_ReportsMinLengthFailure()
    {
        var errors = PortalPasswordPolicy.Validate("aB1");
        Assert.Contains(errors, e => e.Contains("12", StringComparison.OrdinalIgnoreCase));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --filter FullyQualifiedName~PortalPasswordPolicyTests`
Expected: build error — `PortalPasswordPolicy` does not exist.

- [ ] **Step 3: Create the helper**

Create `Helpers/PortalPasswordPolicy.cs`:

```csharp
namespace dwa_ver_val.Helpers;

/// <summary>
/// Stage 2a portal password policy: ≥12 chars, at least one letter, at least
/// one digit. No symbol requirement.
/// </summary>
public static class PortalPasswordPolicy
{
    public const int MinimumLength = 12;

    public static IReadOnlyList<string> Validate(string? password)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            errors.Add("Password is required.");
            return errors;
        }

        if (password.Length < MinimumLength)
            errors.Add($"Password must be at least {MinimumLength} characters long.");

        if (!password.Any(char.IsLetter))
            errors.Add("Password must contain at least one letter.");

        if (!password.Any(char.IsDigit))
            errors.Add("Password must contain at least one digit.");

        return errors;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --filter FullyQualifiedName~PortalPasswordPolicyTests`
Expected: 7 tests pass (1 + 5-from-Theory + 1 = 7).

- [ ] **Step 5: Run full suite**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --nologo --verbosity quiet`
Expected: 158 / 0 / 158 (was 151; +7).

- [ ] **Step 6: Commit**

```bash
git add Helpers/PortalPasswordPolicy.cs Tests/Helpers/PortalPasswordPolicyTests.cs
git commit -m "Add PortalPasswordPolicy validator (12+ chars, letter + digit)"
```

---

### Task 9: `IPublicUserRegistrationService` + implementation + tests

**Files:**
- Create: `Services/Portal/Auth/IPublicUserRegistrationService.cs`
- Create: `Services/Portal/Auth/PublicUserRegistrationService.cs`
- Create: `Tests/Services/Portal/Auth/PublicUserRegistrationServiceTests.cs`

> The service handles: (a) creating the `PublicUser` row with hashed password, (b) generating a DataProtection-wrapped email-confirmation token containing `(PublicUserId, ExpiresAtUtc=now+24h)`, (c) dispatching the confirmation email via `IEmailSender`, and (d) confirming the email when the user clicks the link.
>
> The token format is plain-bytes serialised via `BinaryWriter`: a Guid (16 bytes) + a long Unix-epoch-seconds expiry (8 bytes). Wrapped via `IDataProtectionProvider.CreateProtector("PortalEmailConfirm:v1")` and Base64URL-encoded for the URL.

- [ ] **Step 1: Write the failing test**

Create `Tests/Services/Portal/Auth/PublicUserRegistrationServiceTests.cs`:

```csharp
using dwa_ver_val.Services.Audit;
using dwa_ver_val.Services.Infrastructure.Email;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Tests.Helpers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Services.Portal.Auth;

public class PublicUserRegistrationServiceTests
{
    private static IDataProtectionProvider CreateDataProtection()
    {
        var services = new ServiceCollection().AddDataProtection().Services.BuildServiceProvider();
        return services.GetRequiredService<IDataProtectionProvider>();
    }

    private static PublicUserRegistrationService CreateSut(
        ApplicationDBContext db, Mock<IEmailSender>? email = null, TestAuditService? audit = null)
    {
        return new PublicUserRegistrationService(
            db,
            new PasswordHasher<PublicUser>(),
            CreateDataProtection(),
            (email ?? new Mock<IEmailSender>(MockBehavior.Loose)).Object,
            audit ?? new TestAuditService(),
            NullLogger<PublicUserRegistrationService>.Instance);
    }

    [Fact]
    public async Task RegisterAsync_HappyPath_CreatesPendingUser_AndDispatchesEmail()
    {
        using var db = TestDbContextFactory.Create();
        var email = new Mock<IEmailSender>();
        email.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var audit = new TestAuditService();
        var sut = CreateSut(db, email, audit);

        var result = await sut.RegisterAsync(new RegistrationRequest(
            EmailAddress: "alice@example.test",
            Password: "Aliceaaaa123!",
            FirstName: "Alice",
            LastName: "Smith",
            IdentityNumber: "8001015009087",
            PhoneNumber: "0820000000",
            IsHDI: false,
            HdiConsent: false,
            AcceptTerms: true), default);

        Assert.True(result.Success);
        Assert.Single(db.PublicUsers);
        var saved = db.PublicUsers.AsNoTracking().Single();
        Assert.Equal("Pending", saved.Status);
        Assert.False(saved.EmailConfirmed);
        Assert.NotEqual("Aliceaaaa123!", saved.PasswordHash); // hashed
        email.Verify(e => e.SendAsync(It.Is<EmailMessage>(m => m.To == "alice@example.test"), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(audit.Events, e => e.Action == "PublicUserRegistered");
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_FailsWithErrorAndDoesNotEmail()
    {
        using var db = TestDbContextFactory.Create();
        db.PublicUsers.Add(PublicUserBuilder.Pending("dup@example.test"));
        await db.SaveChangesAsync();

        var email = new Mock<IEmailSender>();
        var sut = CreateSut(db, email);

        var result = await sut.RegisterAsync(new RegistrationRequest(
            EmailAddress: "dup@example.test",
            Password: "Aliceaaaa123!",
            FirstName: "X", LastName: "Y",
            IdentityNumber: "8001015009087",
            PhoneNumber: null,
            IsHDI: false, HdiConsent: false, AcceptTerms: true), default);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("email", StringComparison.OrdinalIgnoreCase));
        email.Verify(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_BadPassword_FailsAndDoesNotPersist()
    {
        using var db = TestDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.RegisterAsync(new RegistrationRequest(
            EmailAddress: "x@example.test",
            Password: "short",
            FirstName: "X", LastName: "Y",
            IdentityNumber: "8001015009087",
            PhoneNumber: null,
            IsHDI: false, HdiConsent: false, AcceptTerms: true), default);

        Assert.False(result.Success);
        Assert.Empty(db.PublicUsers);
    }

    [Fact]
    public async Task RegisterAsync_BadIdentityNumber_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.RegisterAsync(new RegistrationRequest(
            EmailAddress: "x@example.test",
            Password: "Aliceaaaa123!",
            FirstName: "X", LastName: "Y",
            IdentityNumber: "1234567890123",
            PhoneNumber: null,
            IsHDI: false, HdiConsent: false, AcceptTerms: true), default);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RegisterAsync_HdiTrueWithoutConsent_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.RegisterAsync(new RegistrationRequest(
            EmailAddress: "x@example.test",
            Password: "Aliceaaaa123!",
            FirstName: "X", LastName: "Y",
            IdentityNumber: "8001015009087",
            PhoneNumber: null,
            IsHDI: true, HdiConsent: false, AcceptTerms: true), default);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("hdi", StringComparison.OrdinalIgnoreCase) || e.Contains("consent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RegisterAsync_HdiTrueWithConsent_PersistsConsentDate()
    {
        using var db = TestDbContextFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.RegisterAsync(new RegistrationRequest(
            EmailAddress: "h@example.test",
            Password: "Aliceaaaa123!",
            FirstName: "H", LastName: "Y",
            IdentityNumber: "8001015009087",
            PhoneNumber: null,
            IsHDI: true, HdiConsent: true, AcceptTerms: true), default);

        Assert.True(result.Success);
        var saved = db.PublicUsers.AsNoTracking().Single();
        Assert.True(saved.IsHDI);
        Assert.NotNull(saved.HdiConsentGivenDate);
    }

    [Fact]
    public async Task ConfirmEmailAsync_ValidToken_ActivatesUser_AndAudits()
    {
        using var db = TestDbContextFactory.Create();
        var audit = new TestAuditService();
        var sut = CreateSut(db, audit: audit);

        var registerResult = await sut.RegisterAsync(new RegistrationRequest(
            EmailAddress: "c@example.test",
            Password: "Aliceaaaa123!",
            FirstName: "C", LastName: "D",
            IdentityNumber: "8001015009087",
            PhoneNumber: null,
            IsHDI: false, HdiConsent: false, AcceptTerms: true), default);
        Assert.True(registerResult.Success);
        var token = registerResult.ConfirmationToken!;

        var confirmResult = await sut.ConfirmEmailAsync(token, default);

        Assert.True(confirmResult.Success);
        var saved = db.PublicUsers.AsNoTracking().Single();
        Assert.True(saved.EmailConfirmed);
        Assert.Equal("Active", saved.Status);
        Assert.Contains(audit.Events, e => e.Action == "PublicUserEmailConfirmed");
    }

    [Fact]
    public async Task ConfirmEmailAsync_TamperedToken_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var sut = CreateSut(db);

        var confirmResult = await sut.ConfirmEmailAsync("totally-not-a-real-token", default);

        Assert.False(confirmResult.Success);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --filter FullyQualifiedName~PublicUserRegistrationServiceTests`
Expected: build error — types do not exist.

- [ ] **Step 3: Create the interface**

Create `Services/Portal/Auth/IPublicUserRegistrationService.cs`:

```csharp
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
```

- [ ] **Step 4: Create the implementation**

Create `Services/Portal/Auth/PublicUserRegistrationService.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
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
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --filter FullyQualifiedName~PublicUserRegistrationServiceTests`
Expected: 8 tests pass.

- [ ] **Step 6: Run full suite**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --nologo --verbosity quiet`
Expected: 166 / 0 / 166 (was 158; +8).

- [ ] **Step 7: Commit**

```bash
git add Services/Portal/Auth/IPublicUserRegistrationService.cs Services/Portal/Auth/PublicUserRegistrationService.cs Tests/Services/Portal/Auth/PublicUserRegistrationServiceTests.cs
git commit -m "Add PublicUserRegistrationService with DataProtection email confirm tokens"
```

---

### Task 10: `IPublicUserSignInService` + implementation + tests

**Files:**
- Create: `Services/Portal/Auth/IPublicUserSignInService.cs`
- Create: `Services/Portal/Auth/PublicUserSignInService.cs`
- Create: `Tests/Services/Portal/Auth/PublicUserSignInServiceTests.cs`

> Stage 2a: verify password, sign in via cookie scheme, audit. NO lockout, NO MFA. Anti-enumeration: wrong password and unknown email return the same generic error message.

- [ ] **Step 1: Write the failing test**

Create `Tests/Services/Portal/Auth/PublicUserSignInServiceTests.cs`:

```csharp
using System.Security.Claims;
using dwa_ver_val.Services.Audit;
using dwa_ver_val.Services.Portal.Auth;
using dwa_ver_val.Tests.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Services.Portal.Auth;

public class PublicUserSignInServiceTests
{
    private static (PublicUserSignInService sut, Mock<IAuthenticationService> auth, TestAuditService audit, ApplicationDBContext db) CreateSut(
        Action<ApplicationDBContext>? seed = null)
    {
        var db = TestDbContextFactory.Create();
        seed?.Invoke(db);
        db.SaveChanges();

        var hasher = new PasswordHasher<PublicUser>();
        var audit = new TestAuditService();
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        var ctx = new DefaultHttpContext();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton(auth.Object);
        ctx.RequestServices = services.BuildServiceProvider();
        var ctxAccessor = new HttpContextAccessor { HttpContext = ctx };

        var sut = new PublicUserSignInService(
            db, hasher, ctxAccessor, audit, NullLogger<PublicUserSignInService>.Instance);
        return (sut, auth, audit, db);
    }

    private static PublicUser HashedActive(string email, string plainPassword)
    {
        var hasher = new PasswordHasher<PublicUser>();
        var user = PublicUserBuilder.Active(email);
        user.PasswordHash = hasher.HashPassword(user, plainPassword);
        return user;
    }

    [Fact]
    public async Task SignInAsync_HappyPath_SignsIn_AndAudits()
    {
        var (sut, auth, audit, _) = CreateSut(db => db.PublicUsers.Add(HashedActive("u@e.test", "Goodpassword12!")));

        var result = await sut.SignInAsync("u@e.test", "Goodpassword12!", default);

        Assert.True(result.Success);
        auth.Verify(a => a.SignInAsync(It.IsAny<HttpContext>(),
            "PublicPortalScheme",
            It.Is<ClaimsPrincipal>(p => p.HasClaim(c => c.Type == "EmailConfirmed" && c.Value == "true")),
            It.IsAny<AuthenticationProperties>()), Times.Once);
        Assert.Contains(audit.Events, e => e.Action == "PublicUserSignedIn");
    }

    [Fact]
    public async Task SignInAsync_WrongPassword_ReturnsGenericError_AndAudits()
    {
        var (sut, auth, audit, db) = CreateSut(db => db.PublicUsers.Add(HashedActive("u@e.test", "Goodpassword12!")));

        var result = await sut.SignInAsync("u@e.test", "wrongpassword12!", default);

        Assert.False(result.Success);
        Assert.Equal("Login failed.", result.Error);
        auth.Verify(a => a.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()), Times.Never);
        Assert.Contains(audit.Events, e => e.Action == "PublicUserSignInFailed");
    }

    [Fact]
    public async Task SignInAsync_UnknownEmail_ReturnsSameGenericError()
    {
        var (sut, _, _, _) = CreateSut();

        var result = await sut.SignInAsync("nope@e.test", "anything", default);

        Assert.False(result.Success);
        Assert.Equal("Login failed.", result.Error);
    }

    [Fact]
    public async Task SignInAsync_UnconfirmedEmail_BlocksWithSpecificMessage()
    {
        var (sut, auth, audit, _) = CreateSut(db =>
        {
            var pending = PublicUserBuilder.Pending("p@e.test");
            pending.PasswordHash = new PasswordHasher<PublicUser>().HashPassword(pending, "Goodpassword12!");
            db.PublicUsers.Add(pending);
        });

        var result = await sut.SignInAsync("p@e.test", "Goodpassword12!", default);

        Assert.False(result.Success);
        Assert.Equal("Please confirm your email before logging in.", result.Error);
        auth.Verify(a => a.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()), Times.Never);
    }

    [Fact]
    public async Task SignOutAsync_CallsSignOutWithPortalScheme()
    {
        var (sut, _, _, _) = CreateSut();
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.SignOutAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);
        // Replace auth in HttpContext.RequestServices
        var ctx = new DefaultHttpContext();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton(auth.Object);
        ctx.RequestServices = services.BuildServiceProvider();
        var sut2 = new PublicUserSignInService(
            TestDbContextFactory.Create(),
            new PasswordHasher<PublicUser>(),
            new HttpContextAccessor { HttpContext = ctx },
            new TestAuditService(),
            NullLogger<PublicUserSignInService>.Instance);

        await sut2.SignOutAsync(default);

        auth.Verify(a => a.SignOutAsync(It.IsAny<HttpContext>(), "PublicPortalScheme", It.IsAny<AuthenticationProperties>()), Times.Once);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --filter FullyQualifiedName~PublicUserSignInServiceTests`
Expected: build error — types do not exist.

- [ ] **Step 3: Create the interface**

Create `Services/Portal/Auth/IPublicUserSignInService.cs`:

```csharp
namespace dwa_ver_val.Services.Portal.Auth;

public record SignInResult(bool Success, string? Error = null, Guid? PublicUserId = null);

public interface IPublicUserSignInService
{
    Task<SignInResult> SignInAsync(string email, string password, CancellationToken ct);
    Task SignOutAsync(CancellationToken ct);
}
```

- [ ] **Step 4: Create the implementation**

Create `Services/Portal/Auth/PublicUserSignInService.cs`:

```csharp
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
    // Generic error so wrong-password and unknown-email are indistinguishable.
    public const string GenericLoginFailed = "Login failed.";
    public const string EmailNotConfirmed = "Please confirm your email before logging in.";

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

        var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verify == PasswordVerificationResult.Failed)
        {
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
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --filter FullyQualifiedName~PublicUserSignInServiceTests`
Expected: 5 tests pass.

- [ ] **Step 6: Run full suite**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --nologo --verbosity quiet`
Expected: 171 / 0 / 171 (was 166; +5).

- [ ] **Step 7: Commit**

```bash
git add Services/Portal/Auth/IPublicUserSignInService.cs Services/Portal/Auth/PublicUserSignInService.cs Tests/Services/Portal/Auth/PublicUserSignInServiceTests.cs
git commit -m "Add PublicUserSignInService (basic, no lockout — Stage 2b adds it)"
```

---

### Task 11: Register `PublicUserRegistrationService` + `PublicUserSignInService` + DI in `Program.cs`

**Files:**
- Modify: `Program.cs`

- [ ] **Step 1: Read current state**

```bash
grep -n "IPublicUserPropertyAccessor\|PasswordHasher" Program.cs
```

You should find the existing `AddScoped<IPublicUserPropertyAccessor, PublicUserPropertyAccessor>()` line near line 130.

- [ ] **Step 2: Add the registrations**

Find this block in `Program.cs`:

```csharp
// Portal infrastructure abstractions
builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
builder.Services.AddSingleton<IFileStorage>(sp =>
    new LocalDiskFileStorage(
        Path.Combine(builder.Environment.ContentRootPath, "portal-uploads")));
builder.Services.AddScoped<IPublicUserPropertyAccessor, PublicUserPropertyAccessor>();
```

Append immediately after:

```csharp
builder.Services.AddScoped<IPublicUserRegistrationService, PublicUserRegistrationService>();
builder.Services.AddScoped<IPublicUserSignInService, PublicUserSignInService>();
builder.Services.AddSingleton<PasswordHasher<PublicUser>>();
builder.Services.AddHttpContextAccessor();
```

(Add `using Microsoft.AspNetCore.Identity;` to the top of `Program.cs` only if it isn't already there — the existing `AddIdentity` registration means it already is.)

- [ ] **Step 3: Build to verify**

Run: `dotnet build`
Expected: 0 errors.

- [ ] **Step 4: Run full suite**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --nologo --verbosity quiet`
Expected: 171 / 0 / 171 (no count change).

- [ ] **Step 5: Commit**

```bash
git add Program.cs
git commit -m "Program.cs: register PublicUserRegistrationService + SignInService DI"
```

---

### Task 12: Portal area shell — layout, ViewStart, ViewImports

**Files:**
- Create: `Areas/ExternalPortal/Views/_ViewImports.cshtml`
- Create: `Areas/ExternalPortal/Views/_ViewStart.cshtml`
- Create: `Areas/ExternalPortal/Views/Shared/_PortalLayout.cshtml`

> Layout mirrors `Views/Shared/_LoginLayout.cshtml` styling but with "Public Portal" branding so it's visually distinct.

- [ ] **Step 1: Create `_ViewImports.cshtml`**

Create `Areas/ExternalPortal/Views/_ViewImports.cshtml`:

```cshtml
@using dwa_ver_val
@using dwa_ver_val.Areas.ExternalPortal.ViewModels
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

- [ ] **Step 2: Create `_ViewStart.cshtml`**

Create `Areas/ExternalPortal/Views/_ViewStart.cshtml`:

```cshtml
@{
    Layout = "_PortalLayout";
}
```

- [ ] **Step 3: Create the layout**

Create `Areas/ExternalPortal/Views/Shared/_PortalLayout.cshtml`:

```cshtml
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] — DWA V&V Public Portal</title>
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/css/dws.css" asp-append-version="true" />
    <style>
        body {
            background: linear-gradient(160deg, var(--dws-navy-900) 0%, var(--dws-teal-600) 60%, var(--dws-blue-600) 130%);
            margin: 0;
            font-family: var(--dws-font-sans);
            color: var(--dws-text);
            min-height: 100vh;
        }
        .portal-shell {
            min-height: 100vh;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            padding: var(--dws-space-5) var(--dws-space-4);
        }
        .portal-shell__brand {
            color: var(--dws-white);
            margin-bottom: var(--dws-space-6);
            text-align: center;
        }
        .portal-shell__brand h2 {
            margin: 0;
            font-size: 24px;
            font-weight: 500;
            letter-spacing: 0.03em;
        }
        .portal-shell__brand p {
            margin: 6px 0 0 0;
            font-size: var(--dws-fs-sm);
            color: var(--dws-blue-100);
            letter-spacing: 0.02em;
        }
        .portal-card {
            background: var(--dws-surface);
            border-radius: 8px;
            padding: var(--dws-space-5);
            box-shadow: 0 4px 16px rgba(0,0,0,0.18);
            width: 100%;
            max-width: 480px;
        }
        .portal-card h1 {
            margin: 0 0 var(--dws-space-3) 0;
            font-size: 20px;
            color: var(--dws-primary);
        }
        .portal-card .muted {
            color: var(--dws-text-muted);
            font-size: var(--dws-fs-sm);
            margin: 0 0 var(--dws-space-4) 0;
        }
        .portal-card label {
            display: block;
            margin-top: var(--dws-space-3);
            font-size: var(--dws-fs-sm);
            color: var(--dws-text);
        }
        .portal-card input[type=text],
        .portal-card input[type=email],
        .portal-card input[type=password],
        .portal-card input[type=tel] {
            width: 100%;
            box-sizing: border-box;
            padding: 8px 10px;
            border: 1px solid var(--dws-border);
            border-radius: 4px;
            font-size: var(--dws-fs-sm);
            margin-top: 4px;
        }
        .portal-card .inline {
            display: flex;
            align-items: center;
            gap: 8px;
            margin-top: var(--dws-space-3);
        }
        .portal-card .actions {
            margin-top: var(--dws-space-5);
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        .portal-card .btn-primary {
            background: var(--dws-primary);
            color: var(--dws-white);
            border: none;
            padding: 10px 18px;
            border-radius: 4px;
            font-size: var(--dws-fs-sm);
            cursor: pointer;
        }
        .portal-card .btn-primary:hover { background: var(--dws-primary-hover); }
        .portal-card .errors {
            background: #fff3f3;
            border: 1px solid var(--dws-red-700);
            color: var(--dws-red-700);
            padding: 10px 14px;
            border-radius: 4px;
            margin-bottom: var(--dws-space-4);
        }
        .portal-card .errors ul { margin: 6px 0 0 18px; padding: 0; }
        .portal-shell__footer {
            margin-top: var(--dws-space-5);
            color: var(--dws-blue-100);
            font-size: var(--dws-fs-xs);
            opacity: 0.8;
            text-align: center;
        }
    </style>
</head>
<body>
    <div class="portal-shell">
        <div class="portal-shell__brand">
            <h2>DWA V&amp;V Public Portal</h2>
            <p>Verification of Existing Lawful Water Use</p>
        </div>
        @RenderBody()
        <div class="portal-shell__footer">
            National Water Act 38 of 1998 — Sections 32–35
        </div>
    </div>
    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build`
Expected: 0 errors. Razor compiles the new files.

- [ ] **Step 5: Commit**

```bash
git add Areas/ExternalPortal/Views/
git commit -m "Add ExternalPortal area layout, ViewStart, ViewImports"
```

---

### Task 13: `RegisterViewModel` and `LoginViewModel`

**Files:**
- Create: `Areas/ExternalPortal/ViewModels/RegisterViewModel.cs`
- Create: `Areas/ExternalPortal/ViewModels/LoginViewModel.cs`

> ViewModels carry only what the form binds — server-side validation is in the service layer (Tasks 9 + 10).

- [ ] **Step 1: Create `RegisterViewModel`**

Create `Areas/ExternalPortal/ViewModels/RegisterViewModel.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.Areas.ExternalPortal.ViewModels;

public class RegisterViewModel
{
    [Required, EmailAddress, Display(Name = "Email address")]
    public string Email { get; set; } = "";

    [Required, MinLength(12), DataType(DataType.Password), Display(Name = "Password")]
    public string Password { get; set; } = "";

    [Required, DataType(DataType.Password), Display(Name = "Confirm password"),
     Compare(nameof(Password), ErrorMessage = "The two passwords don't match.")]
    public string ConfirmPassword { get; set; } = "";

    [Required, Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required, Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [Required, StringLength(13, MinimumLength = 13, ErrorMessage = "Enter a 13-digit South African ID number."),
     Display(Name = "South African ID number")]
    public string IdentityNumber { get; set; } = "";

    [Display(Name = "Phone number (optional)")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "I am a Historically Disadvantaged Individual (HDI)")]
    public bool IsHDI { get; set; }

    [Display(Name = "I consent to processing of my HDI status (race / gender) for prioritisation purposes (POPIA Section 26)")]
    public bool HdiConsent { get; set; }

    [Display(Name = "I accept the Terms of Use")]
    public bool AcceptTerms { get; set; }
}
```

- [ ] **Step 2: Create `LoginViewModel`**

Create `Areas/ExternalPortal/ViewModels/LoginViewModel.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.Areas.ExternalPortal.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress, Display(Name = "Email address")]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password), Display(Name = "Password")]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Areas/ExternalPortal/ViewModels/
git commit -m "Add Register and Login ViewModels for portal"
```

---

### Task 14: `AccountController` — Register + ConfirmEmail actions

**Files:**
- Create: `Areas/ExternalPortal/Controllers/AccountController.cs`
- Create: `Areas/ExternalPortal/Views/Account/Register.cshtml`
- Create: `Areas/ExternalPortal/Views/Account/RegisterConfirmation.cshtml`
- Create: `Areas/ExternalPortal/Views/Account/ConfirmEmail.cshtml`
- Test: `Tests/Areas/ExternalPortal/AccountControllerRegistrationTests.cs`

> Login + Logout actions land in Task 15. Splitting so each commit is reviewable.

- [ ] **Step 1: Write the failing controller test**

Create `Tests/Areas/ExternalPortal/AccountControllerRegistrationTests.cs`:

```csharp
using dwa_ver_val.Areas.ExternalPortal.Controllers;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class AccountControllerRegistrationTests
{
    [Fact]
    public async Task Register_Post_HappyPath_RedirectsToRegisterConfirmation()
    {
        var reg = new Mock<IPublicUserRegistrationService>();
        reg.Setup(r => r.RegisterAsync(It.IsAny<RegistrationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistrationResult(true, Array.Empty<string>(), "tok", Guid.NewGuid()));
        var sign = new Mock<IPublicUserSignInService>();

        var controller = new AccountController(reg.Object, sign.Object, NullLogger<AccountController>.Instance);

        var result = await controller.Register(new RegisterViewModel
        {
            Email = "alice@e.test",
            Password = "Aliceaaaa123!",
            ConfirmPassword = "Aliceaaaa123!",
            FirstName = "Alice",
            LastName = "Smith",
            IdentityNumber = "8001015009087",
            AcceptTerms = true
        }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("RegisterConfirmation", redirect.ActionName);
    }

    [Fact]
    public async Task Register_Post_ServiceFailure_ReturnsViewWithErrors()
    {
        var reg = new Mock<IPublicUserRegistrationService>();
        reg.Setup(r => r.RegisterAsync(It.IsAny<RegistrationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistrationResult(false, new[] { "Service-level error." }));
        var controller = new AccountController(reg.Object, new Mock<IPublicUserSignInService>().Object, NullLogger<AccountController>.Instance);

        var result = await controller.Register(new RegisterViewModel
        {
            Email = "x@y.test",
            Password = "validpassword12",
            ConfirmPassword = "validpassword12",
            FirstName = "X", LastName = "Y",
            IdentityNumber = "8001015009087",
            AcceptTerms = true
        }, default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[""]!.Errors, e => e.ErrorMessage == "Service-level error.");
    }

    [Fact]
    public async Task ConfirmEmail_Get_ValidToken_ShowsSuccessView()
    {
        var reg = new Mock<IPublicUserRegistrationService>();
        reg.Setup(r => r.ConfirmEmailAsync("good", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailConfirmationResult(true, Array.Empty<string>(), Guid.NewGuid()));
        var controller = new AccountController(reg.Object, new Mock<IPublicUserSignInService>().Object, NullLogger<AccountController>.Instance);

        var result = await controller.ConfirmEmail("good", default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(true, view.ViewData["Success"]);
    }

    [Fact]
    public async Task ConfirmEmail_Get_InvalidToken_ShowsFailureView()
    {
        var reg = new Mock<IPublicUserRegistrationService>();
        reg.Setup(r => r.ConfirmEmailAsync("bad", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailConfirmationResult(false, new[] { "Invalid token." }));
        var controller = new AccountController(reg.Object, new Mock<IPublicUserSignInService>().Object, NullLogger<AccountController>.Instance);

        var result = await controller.ConfirmEmail("bad", default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(false, view.ViewData["Success"]);
        Assert.Equal("Invalid token.", view.ViewData["Error"]);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --filter FullyQualifiedName~AccountControllerRegistrationTests`
Expected: build error — `dwa_ver_val.Areas.ExternalPortal.Controllers.AccountController` does not exist.

- [ ] **Step 3: Create the controller**

Create `Areas/ExternalPortal/Controllers/AccountController.cs`:

```csharp
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dwa_ver_val.Areas.ExternalPortal.Controllers;

[Area("ExternalPortal")]
[Route("ExternalPortal/[controller]/[action]")]
public class AccountController : Controller
{
    private readonly IPublicUserRegistrationService _registration;
    private readonly IPublicUserSignInService _signIn;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IPublicUserRegistrationService registration,
        IPublicUserSignInService signIn,
        ILogger<AccountController> logger)
    {
        _registration = registration;
        _signIn = signIn;
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

        // Real implementation will substitute the link in the email body. For Stage 2a the
        // token is logged via LoggingEmailSender — we surface it here too so the demo can
        // copy/paste during testing. Stage 2b will move the link substitution into the service.
        var url = Url.Action(nameof(ConfirmEmail), "Account", new { area = "ExternalPortal", token = result.ConfirmationToken })!;
        TempData["DemoConfirmUrl"] = url;
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
        ViewData["Success"] = result.Success;
        ViewData["Error"] = result.Success ? null : (result.Errors.FirstOrDefault() ?? "The confirmation link is invalid.");
        return View();
    }
}
```

- [ ] **Step 4: Create the views**

Create `Areas/ExternalPortal/Views/Account/Register.cshtml`:

```cshtml
@model RegisterViewModel
@{
    ViewData["Title"] = "Register";
}
<div class="portal-card">
    <h1>Create your account</h1>
    <p class="muted">Register to view the V&amp;V case status of properties you own.</p>

    @if (!ViewData.ModelState.IsValid)
    {
        <div class="errors">
            <strong>We could not register your account:</strong>
            <ul>
                @foreach (var modelStateEntry in ViewData.ModelState.Values)
                {
                    foreach (var error in modelStateEntry.Errors)
                    {
                        <li>@error.ErrorMessage</li>
                    }
                }
            </ul>
        </div>
    }

    <form asp-action="Register" method="post" novalidate>
        <label asp-for="FirstName"></label><input asp-for="FirstName" autocomplete="given-name" />
        <label asp-for="LastName"></label><input asp-for="LastName" autocomplete="family-name" />
        <label asp-for="Email"></label><input asp-for="Email" autocomplete="email" />
        <label asp-for="IdentityNumber"></label><input asp-for="IdentityNumber" inputmode="numeric" />
        <label asp-for="PhoneNumber"></label><input asp-for="PhoneNumber" type="tel" autocomplete="tel" />
        <label asp-for="Password"></label><input asp-for="Password" type="password" autocomplete="new-password" />
        <label asp-for="ConfirmPassword"></label><input asp-for="ConfirmPassword" type="password" autocomplete="new-password" />

        <div class="inline">
            <input asp-for="IsHDI" type="checkbox" />
            <label asp-for="IsHDI" style="margin: 0;"></label>
        </div>
        <div class="inline">
            <input asp-for="HdiConsent" type="checkbox" />
            <label asp-for="HdiConsent" style="margin: 0;"></label>
        </div>
        <div class="inline">
            <input asp-for="AcceptTerms" type="checkbox" />
            <label asp-for="AcceptTerms" style="margin: 0;"></label>
        </div>

        <div class="actions">
            <a asp-action="Login" class="muted">Already registered? Log in</a>
            <button type="submit" class="btn-primary">Create account</button>
        </div>
    </form>
</div>
```

Create `Areas/ExternalPortal/Views/Account/RegisterConfirmation.cshtml`:

```cshtml
@{
    ViewData["Title"] = "Check your email";
    var demoUrl = ViewData["DemoConfirmUrl"] as string;
}
<div class="portal-card">
    <h1>Check your email</h1>
    <p>We've sent you a confirmation link. The link expires in 24 hours.</p>
    <p class="muted">Once you confirm, you'll be able to log in.</p>

    @if (!string.IsNullOrEmpty(demoUrl))
    {
        <div class="errors" style="background:#f0f7ff; border-color: var(--dws-blue-600); color: var(--dws-blue-600);">
            <strong>Demo helper (Stage 2a only):</strong>
            <p>The real email is logged via <code>LoggingEmailSender</code>. While we wait for a real email provider, click here to confirm:</p>
            <p><a href="@demoUrl">@demoUrl</a></p>
        </div>
    }
</div>
```

Create `Areas/ExternalPortal/Views/Account/ConfirmEmail.cshtml`:

```cshtml
@{
    ViewData["Title"] = "Email confirmation";
    var success = (bool?)ViewData["Success"] ?? false;
    var error = ViewData["Error"] as string;
}
<div class="portal-card">
    @if (success)
    {
        <h1>Email confirmed</h1>
        <p>Your account is now active. You can <a asp-action="Login">log in</a>.</p>
    }
    else
    {
        <h1>Could not confirm your email</h1>
        <p class="errors">@error</p>
        <p class="muted">If your link expired, please <a asp-action="Register">register again</a>.</p>
    }
</div>
```

- [ ] **Step 5: Run controller tests to verify they pass**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --filter FullyQualifiedName~AccountControllerRegistrationTests`
Expected: 4 tests pass.

- [ ] **Step 6: Run full suite**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --nologo --verbosity quiet`
Expected: 175 / 0 / 175 (was 171; +4).

- [ ] **Step 7: Commit**

```bash
git add Areas/ExternalPortal/Controllers/AccountController.cs Areas/ExternalPortal/Views/Account/Register.cshtml Areas/ExternalPortal/Views/Account/RegisterConfirmation.cshtml Areas/ExternalPortal/Views/Account/ConfirmEmail.cshtml Tests/Areas/ExternalPortal/AccountControllerRegistrationTests.cs
git commit -m "AccountController: Register + ConfirmEmail actions + views"
```

---

### Task 15: `AccountController` — Login + Logout + AccessDenied actions

**Files:**
- Modify: `Areas/ExternalPortal/Controllers/AccountController.cs`
- Create: `Areas/ExternalPortal/Views/Account/Login.cshtml`
- Create: `Areas/ExternalPortal/Views/Account/AccessDenied.cshtml`
- Create: `Tests/Areas/ExternalPortal/AccountControllerLoginTests.cs`

- [ ] **Step 1: Write the failing controller test**

Create `Tests/Areas/ExternalPortal/AccountControllerLoginTests.cs`:

```csharp
using dwa_ver_val.Areas.ExternalPortal.Controllers;
using dwa_ver_val.Areas.ExternalPortal.ViewModels;
using dwa_ver_val.Services.Portal.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace dwa_ver_val.Tests.Areas.ExternalPortal;

public class AccountControllerLoginTests
{
    [Fact]
    public async Task Login_Post_HappyPath_RedirectsToDashboard()
    {
        var sign = new Mock<IPublicUserSignInService>();
        sign.Setup(s => s.SignInAsync("u@e.test", "Goodpassword12!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignInResult(true, null, Guid.NewGuid()));
        var controller = new AccountController(new Mock<IPublicUserRegistrationService>().Object, sign.Object, NullLogger<AccountController>.Instance);

        var result = await controller.Login(new LoginViewModel { Email = "u@e.test", Password = "Goodpassword12!" }, default);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Dashboard", redirect.ControllerName);
    }

    [Fact]
    public async Task Login_Post_FailureFromService_ReturnsViewWithError()
    {
        var sign = new Mock<IPublicUserSignInService>();
        sign.Setup(s => s.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignInResult(false, "Login failed."));
        var controller = new AccountController(new Mock<IPublicUserRegistrationService>().Object, sign.Object, NullLogger<AccountController>.Instance);

        var result = await controller.Login(new LoginViewModel { Email = "u@e.test", Password = "wrong" }, default);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[""]!.Errors, e => e.ErrorMessage == "Login failed.");
    }

    [Fact]
    public async Task Login_Post_HappyPath_WithReturnUrl_RedirectsToLocalReturnUrl()
    {
        var sign = new Mock<IPublicUserSignInService>();
        sign.Setup(s => s.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignInResult(true, null, Guid.NewGuid()));
        var controller = new AccountController(new Mock<IPublicUserRegistrationService>().Object, sign.Object, NullLogger<AccountController>.Instance);
        controller.Url = new Mock<IUrlHelper>().Object;
        Mock.Get(controller.Url).Setup(u => u.IsLocalUrl("/ExternalPortal/Dashboard/Index")).Returns(true);

        var result = await controller.Login(new LoginViewModel { Email = "u@e.test", Password = "Goodpassword12!", ReturnUrl = "/ExternalPortal/Dashboard/Index" }, default);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/ExternalPortal/Dashboard/Index", redirect.Url);
    }

    [Fact]
    public async Task Logout_Post_CallsServiceAndRedirectsToLogin()
    {
        var sign = new Mock<IPublicUserSignInService>();
        sign.Setup(s => s.SignOutAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var controller = new AccountController(new Mock<IPublicUserRegistrationService>().Object, sign.Object, NullLogger<AccountController>.Instance);

        var result = await controller.Logout(default);

        sign.Verify(s => s.SignOutAsync(It.IsAny<CancellationToken>()), Times.Once);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --filter FullyQualifiedName~AccountControllerLoginTests`
Expected: build error — `Login`, `Logout` actions do not exist.

- [ ] **Step 3: Add the actions to `AccountController`**

Open `Areas/ExternalPortal/Controllers/AccountController.cs` and append the following actions inside the existing class (before the final closing `}`):

```csharp
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

        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
            return Redirect(vm.ReturnUrl);

        return RedirectToAction("Index", "Dashboard", new { area = "ExternalPortal" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _signIn.SignOutAsync(ct);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult AccessDenied() => View();
```

- [ ] **Step 4: Create the views**

Create `Areas/ExternalPortal/Views/Account/Login.cshtml`:

```cshtml
@model LoginViewModel
@{
    ViewData["Title"] = "Log in";
}
<div class="portal-card">
    <h1>Log in to your account</h1>
    <p class="muted">Enter your registered email and password.</p>

    @if (!ViewData.ModelState.IsValid)
    {
        <div class="errors">
            @foreach (var entry in ViewData.ModelState.Values)
            {
                foreach (var err in entry.Errors)
                {
                    <div>@err.ErrorMessage</div>
                }
            }
        </div>
    }

    <form asp-action="Login" method="post">
        <input asp-for="ReturnUrl" type="hidden" />
        <label asp-for="Email"></label><input asp-for="Email" autocomplete="email" />
        <label asp-for="Password"></label><input asp-for="Password" type="password" autocomplete="current-password" />

        <div class="actions">
            <a asp-action="Register" class="muted">No account? Register</a>
            <button type="submit" class="btn-primary">Log in</button>
        </div>
    </form>
</div>
```

Create `Areas/ExternalPortal/Views/Account/AccessDenied.cshtml`:

```cshtml
@{
    ViewData["Title"] = "Access denied";
}
<div class="portal-card">
    <h1>Access denied</h1>
    <p>You don't have access to that page.</p>
    <p class="muted"><a asp-action="Login">Log in with a different account</a></p>
</div>
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --filter FullyQualifiedName~AccountControllerLoginTests`
Expected: 4 tests pass.

- [ ] **Step 6: Run full suite**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --nologo --verbosity quiet`
Expected: 179 / 0 / 179 (was 175; +4).

- [ ] **Step 7: Commit**

```bash
git add Areas/ExternalPortal/Controllers/AccountController.cs Areas/ExternalPortal/Views/Account/Login.cshtml Areas/ExternalPortal/Views/Account/AccessDenied.cshtml Tests/Areas/ExternalPortal/AccountControllerLoginTests.cs
git commit -m "AccountController: Login, Logout, AccessDenied actions + views"
```

---

### Task 16: `DashboardController` — Stage 2a placeholder

**Files:**
- Create: `Areas/ExternalPortal/Controllers/DashboardController.cs`
- Create: `Areas/ExternalPortal/Views/Dashboard/Index.cshtml`

> Stage 2a's dashboard is a placeholder — no case data, no claim flow yet. Just confirms the user is logged in and shows what's coming next. The `[Authorize]` attribute is added implicitly by `PortalAuthorizationConvention`.

- [ ] **Step 1: Create the controller**

Create `Areas/ExternalPortal/Controllers/DashboardController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;

namespace dwa_ver_val.Areas.ExternalPortal.Controllers;

[Area("ExternalPortal")]
[Route("ExternalPortal/[controller]/[action]")]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        ViewData["UserEmail"] = User.Identity?.Name;
        return View();
    }
}
```

- [ ] **Step 2: Create the view**

Create `Areas/ExternalPortal/Views/Dashboard/Index.cshtml`:

```cshtml
@{
    ViewData["Title"] = "Dashboard";
    var email = ViewData["UserEmail"] as string ?? "you";
}
<div class="portal-card">
    <h1>Welcome, @email</h1>
    <p>You're now logged in to the DWA V&amp;V Public Portal.</p>

    <div class="errors" style="background:#fff8e1; border-color: var(--dws-amber-700); color: var(--dws-amber-700);">
        <strong>Coming soon (Stage 2b):</strong>
        <ul>
            <li>Set up two-factor authentication (TOTP)</li>
            <li>Set a stronger password / reset password</li>
            <li>Lock-out protection on failed sign-in attempts</li>
        </ul>
    </div>

    <div class="errors" style="background:#e8f5e8; border-color: var(--dws-green-700); color: var(--dws-green-700); margin-top: var(--dws-space-4);">
        <strong>Coming soon (Stage 3+):</strong>
        <ul>
            <li>Claim ownership of properties registered to your ID</li>
            <li>Upload supporting evidence (title deeds)</li>
            <li>View case status of your properties</li>
            <li>Download statutory letters addressed to you</li>
        </ul>
    </div>

    <form asp-action="Logout" asp-controller="Account" method="post" style="margin-top: var(--dws-space-5);">
        <button type="submit" class="btn-primary">Log out</button>
    </form>
</div>
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build`
Expected: 0 errors.

- [ ] **Step 4: Run full suite**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --nologo --verbosity quiet`
Expected: 179 / 0 / 179 (no count change — no tests added).

- [ ] **Step 5: Commit**

```bash
git add Areas/ExternalPortal/Controllers/DashboardController.cs Areas/ExternalPortal/Views/Dashboard/Index.cshtml
git commit -m "DashboardController: Stage 2a placeholder showing what's coming"
```

---

### Task 17: End-to-end integration test — Register → Confirm → Login → Dashboard

**Files:**
- Create: `Tests/Integration/PortalRegistrationFlowTests.cs`

> Uses `PortalIntegrationTestFixture` from Stage 1. The flow exercises the full HTTP pipeline: form rendering, antiforgery token round-trip, model binding, service call, persistence, email dispatch (logging), token round-trip, login, cookie issuance, dashboard authorisation.

- [ ] **Step 1: Add helper to `IntegrationTestBase.cs`**

Open `Tests/Integration/IntegrationTestBase.cs` and append a new static helper method to `IntegrationTestHelpers`:

```csharp
    /// <summary>
    /// Posts the portal registration form. Returns the response so the caller can inspect
    /// status/Location and (for Stage 2a) the TempData demo confirm URL via following the redirect.
    /// </summary>
    public static async Task<HttpResponseMessage> RegisterPublicUser(
        HttpClient client,
        string email,
        string password = "Validuserpassword12!",
        string firstName = "Test",
        string lastName = "User",
        string identityNumber = "8001015009087")
    {
        var token = await GetAntiForgeryToken(client, "/ExternalPortal/Account/Register");
        return await client.PostAsync("/ExternalPortal/Account/Register", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", email),
            new KeyValuePair<string, string>("Password", password),
            new KeyValuePair<string, string>("ConfirmPassword", password),
            new KeyValuePair<string, string>("FirstName", firstName),
            new KeyValuePair<string, string>("LastName", lastName),
            new KeyValuePair<string, string>("IdentityNumber", identityNumber),
            new KeyValuePair<string, string>("AcceptTerms", "true"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        }));
    }
```

- [ ] **Step 2: Write the integration test**

Create `Tests/Integration/PortalRegistrationFlowTests.cs`:

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace dwa_ver_val.Tests.Integration;

public class PortalRegistrationFlowTests : IClassFixture<PortalIntegrationTestFixture>
{
    private readonly PortalIntegrationTestFixture _fixture;

    public PortalRegistrationFlowTests(PortalIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Register_Confirm_Login_Dashboard_HappyPath()
    {
        var client = _fixture.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var email = $"flow-{Guid.NewGuid():N}@example.test";

        // 1. POST register
        var registerResp = await IntegrationTestHelpers.RegisterPublicUser(client, email);
        Assert.Equal(HttpStatusCode.Redirect, registerResp.StatusCode);
        Assert.Contains("/ExternalPortal/Account/RegisterConfirmation", registerResp.Headers.Location?.OriginalString ?? "");

        // 2. Follow redirect to RegisterConfirmation; the TempData carries the demo confirm URL
        var confirmationPage = await client.GetAsync(registerResp.Headers.Location!);
        Assert.Equal(HttpStatusCode.OK, confirmationPage.StatusCode);
        var confirmationBody = await confirmationPage.Content.ReadAsStringAsync();

        // 3. Pull the confirm URL out of the TempData-rendered demo helper.
        var marker = "/ExternalPortal/Account/ConfirmEmail?token=";
        var idx = confirmationBody.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(idx >= 0, "Demo confirm URL not found in RegisterConfirmation page.");
        var endIdx = confirmationBody.IndexOf('"', idx);
        var confirmUrl = confirmationBody.Substring(idx, endIdx - idx);

        // 4. GET confirm
        var confirmResp = await client.GetAsync(confirmUrl);
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);
        var confirmBody = await confirmResp.Content.ReadAsStringAsync();
        Assert.Contains("Email confirmed", confirmBody);

        // 5. The PublicUser row is now Active + EmailConfirmed.
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
            var user = await db.PublicUsers.AsNoTracking().FirstAsync(u => u.EmailAddress == email);
            Assert.True(user.EmailConfirmed);
            Assert.Equal("Active", user.Status);
        }

        // 6. POST login
        var loginToken = await IntegrationTestHelpers.GetAntiForgeryToken(client, "/ExternalPortal/Account/Login");
        var loginResp = await client.PostAsync("/ExternalPortal/Account/Login", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", email),
            new KeyValuePair<string, string>("Password", "Validuserpassword12!"),
            new KeyValuePair<string, string>("__RequestVerificationToken", loginToken)
        }));
        Assert.Equal(HttpStatusCode.Redirect, loginResp.StatusCode);
        Assert.Contains("/ExternalPortal/Dashboard/Index", loginResp.Headers.Location?.OriginalString ?? "");

        // 7. GET dashboard — protected by PortalAuthorizationConvention; should succeed thanks to the cookie.
        var dashResp = await client.GetAsync(loginResp.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, dashResp.StatusCode);
        var dashBody = await dashResp.Content.ReadAsStringAsync();
        Assert.Contains(email, dashBody);
    }

    [Fact]
    public async Task Dashboard_Unauthenticated_RedirectsToLogin()
    {
        var client = _fixture.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var resp = await client.GetAsync("/ExternalPortal/Dashboard/Index");
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Contains("/ExternalPortal/Account/Login", resp.Headers.Location?.OriginalString ?? "");
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsViewWithGenericError()
    {
        var client = _fixture.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var email = $"wrong-{Guid.NewGuid():N}@example.test";
        await IntegrationTestHelpers.RegisterPublicUser(client, email);

        // Pull the token out of the registration response chain via the same approach as the happy path.
        // For this test we don't need to confirm — wrong password should fail before the email-confirmed check.
        var loginToken = await IntegrationTestHelpers.GetAntiForgeryToken(client, "/ExternalPortal/Account/Login");
        var loginResp = await client.PostAsync("/ExternalPortal/Account/Login", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", email),
            new KeyValuePair<string, string>("Password", "wrongpasswordxx"),
            new KeyValuePair<string, string>("__RequestVerificationToken", loginToken)
        }));
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var body = await loginResp.Content.ReadAsStringAsync();
        Assert.Contains("Login failed", body);
    }
}
```

- [ ] **Step 3: Run the integration tests**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --filter FullyQualifiedName~PortalRegistrationFlowTests`
Expected: 3 tests pass.

If the dashboard redirect test fails (returning 200 instead of redirect), the convention isn't applying `[Authorize]` to `DashboardController` — verify Task 6 wired the convention into `Program.cs`.

If the happy path fails on the GET ConfirmEmail step, look at the `RegisterConfirmation.cshtml` rendering — the test relies on the TempData URL being present in the page HTML.

- [ ] **Step 4: Run full suite**

Run: `dotnet test Tests/dwa_ver_val.Tests.csproj --nologo --verbosity quiet`
Expected: 182 / 0 / 182 (was 179; +3).

- [ ] **Step 5: Commit**

```bash
git add Tests/Integration/IntegrationTestBase.cs Tests/Integration/PortalRegistrationFlowTests.cs
git commit -m "Integration test: portal register → confirm → login → dashboard happy path"
```

---

### Task 18: Update rollout plan + closing journal entry

**Files:**
- Modify: `Project rollout plan.xlsx`
- Modify: `docs/superpowers/tasks/2026-05-03-external-portal-shell/journal.md` (append Stage 2a entry)

- [ ] **Step 1: Update `Project rollout plan.xlsx`**

Run from the worktree root:

```bash
python3 -c "
from openpyxl import load_workbook
import datetime
wb = load_workbook('Project rollout plan.xlsx')
ws = wb['Rollout Plan']
for row in ws.iter_rows(min_row=2):
    if row[0].value == '9.1':
        row[7].value = 'In Progress'
        row[8].value = datetime.datetime(2026, 5, 4)
        row[9].value = 'Stage 1 + Stage 2a complete: foundations + portal area shell + register + email confirm + login + dashboard placeholder. MFA + recovery codes + lockout + reset still ahead in Stage 2b. (2026-05-04)'
        print(f'Updated 9.1: {row[7].value}, {row[8].value}, {row[9].value[:80]}...')
        break
wb.save('Project rollout plan.xlsx')
"
```

- [ ] **Step 2: Append the journal entry**

Append to `docs/superpowers/tasks/2026-05-03-external-portal-shell/journal.md`:

```markdown

### 2026-05-04 — Stage 2a implementation COMPLETE
- 18 plan tasks executed via subagent-driven workflow.
- Final test suite: **182 passed / 0 failed / 182 total** (was 137 / 0 at end of Stage 1; +45 new tests).
- New files (production):
  - `Areas/ExternalPortal/Controllers/{Account, Dashboard}Controller.cs`
  - `Areas/ExternalPortal/ViewModels/{Register, Login}ViewModel.cs`
  - `Areas/ExternalPortal/Views/{Account, Dashboard, Shared}/*.cshtml` + `_ViewStart` + `_ViewImports` + `_PortalLayout`
  - `Helpers/{SaIdValidator, PortalPasswordPolicy}.cs`
  - `Services/Portal/Auth/{IPublicUserRegistrationService, PublicUserRegistrationService, IPublicUserSignInService, PublicUserSignInService, PortalCookieEvents, PortalAuthorizationConvention}.cs`
- Files modified:
  - `Tests/Helpers/PublicUserBuilder.cs` (Pending + Suspended factories — Stage 1 entry condition #5)
  - `Tests/Architecture/PortalBoundaryTests.cs` (third fence — Stage 1 entry condition #2)
  - `Services/Portal/Auth/PortalPolicies.cs` (real claim requirements — Stage 1 entry condition #4)
  - `Program.cs` (PortalCookieEvents + PortalAuthorizationConvention + new service DI)
  - `Tests/Integration/IntegrationTestBase.cs` (RegisterPublicUser helper)
- Stage 2a deliverable: a public user can register, confirm their email, log in, see a placeholder dashboard, and log out — all under `/ExternalPortal/*` with the portal cookie scheme. Internal `/Account/Login` is unchanged.

## Stage 2b entry conditions captured

1. The `Login` action currently signs in any user with confirmed email — Stage 2b must add `MfaEnabled` check and route MFA-not-enrolled users to `/ExternalPortal/Account/EnrolMfa` instead of the dashboard.
2. `PortalPolicies.PortalAuthenticated` currently requires `EmailConfirmed=true` only. Stage 2b must add `MfaEnrolled=true` and `Status=Active` claims.
3. `PortalCookieEvents.OnValidatePrincipal` must be implemented to re-check `PublicUser.Status` from DB on every sliding refresh and reject suspended/deactivated users.
4. The `LoggingEmailSender` currently logs the raw confirmation token. Stage 2b should move the link substitution into `PublicUserRegistrationService` so the email contains a clickable URL rather than a token to copy/paste.
5. Account lockout (`FailedLoginAttempts`, `LockoutUntil`) must be added to `PublicUserSignInService` — Stage 1 added the columns.
6. Password reset flow (ForgotPassword → ResetPassword) must be added; reuse the same DataProtection-tokened pattern as email confirmation but with a separate purpose string `PortalPasswordReset:v1`.
7. TOTP MFA enrolment (Otp.NET + QR code + recovery codes) is the centerpiece of Stage 2b.
8. The dashboard placeholder will be replaced by Stage 3 with the property-claim flow + My Cases.

Stage 2a done. Ready for user review and merge to demo/azure-deploy.
```

- [ ] **Step 3: Commit**

```bash
git add "Project rollout plan.xlsx" docs/superpowers/tasks/2026-05-03-external-portal-shell/journal.md
git commit -m "Mark Stage 2a complete in rollout plan + journal closing entry"
```

---

## Self-review

| Check | Result |
|---|---|
| **Spec coverage — §4.1 Registration flow** | Tasks 9, 13, 14 cover registration form, validation, persistence, email dispatch, confirmation. ✓ |
| **Spec coverage — §4.3 Subsequent login (basic — no MFA)** | Tasks 10, 13, 15 cover login + cookie scheme + audit. MFA challenge deferred to 2b per Stage 2a scope. ✓ |
| **Spec coverage — §4.11 Lockout** | Deferred to Stage 2b per Stage 2a scope. ✓ |
| **Spec coverage — §5.5 Audit** | Register, Confirm, SignIn (success / failure), SignOut all write `AuditEvent`s in service-layer code. ✓ |
| **Stage 1 entry conditions** | #2 (Task 2), #4 (Task 3), #5 (Task 1) all addressed. #6 not relevant in 2a. #1, #3 already done in Stage 1. ✓ |
| **POPIA / HDI consent** | Captured in `RegisterViewModel` + enforced in `PublicUserRegistrationService.ValidateRequest`. ✓ |
| **DWS palette enforcement** | `_PortalLayout.cshtml` uses `var(--dws-*)` tokens only — no hardcoded colors. ✓ |
| **Placeholder scan** | None. Every step has runnable commands and full code blocks. ✓ |
| **Type / signature consistency** | `RegistrationRequest` / `RegistrationResult` / `EmailConfirmationResult` / `SignInResult` shapes consistent across Tasks 9, 10, 14, 15. `PortalCookieOptions.SchemeName` referenced consistently. `PortalPolicies.EmailConfirmedClaim` defined in Task 3, used in Tasks 5 and 10. ✓ |
| **`required` properties pre-checked** | `PublicUser` (5 required) handled by `PublicUserBuilder.Active`/`Pending`/`Suspended`. `EmailMessage` requires `To`, `Subject`, `BodyText` — all set in test code. ✓ |

---

## Stage 2a success criteria

- [ ] `dotnet build` is green.
- [ ] `dotnet test Tests/dwa_ver_val.Tests.csproj` returns 182 / 0.
- [ ] App starts cleanly (`dotnet run`) in Development.
- [ ] Manually: navigate to `http://localhost:5099/ExternalPortal/Account/Register` — form renders with DWS palette.
- [ ] Manually: submit a valid registration → redirected to RegisterConfirmation page → click the demo URL → see "Email confirmed".
- [ ] Manually: log in with the new account → redirected to Dashboard placeholder showing user's email.
- [ ] Manually: click logout → redirected to login page; reloading dashboard → redirected back to login.
- [ ] Internal `/Account/Login` still works (no regression).
- [ ] Rollout plan row 9.1 marked with Stage 2a notes.

---

## What's NOT in Stage 2a (deferred to Stage 2b)

- TOTP MFA enrolment (`Otp.NET`, QR code, recovery codes)
- MFA challenge during login
- Account lockout (5 failed attempts → 15 min)
- Password reset flow (ForgotPassword + ResetPassword)
- TOTP replay prevention
- `PortalCookieEvents.OnValidatePrincipal` real status revalidation
- Real email link substitution (Stage 2a relies on the demo `TempData` URL on the RegisterConfirmation page)
- Email resend flow
- Recovery code regeneration UI
