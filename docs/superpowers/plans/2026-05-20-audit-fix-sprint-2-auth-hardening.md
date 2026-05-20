# Audit Fix Sprint 2 — Auth Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enforce account-status and lockout controls in the External Portal sign-in pipeline so that Suspended/Deactivated accounts cannot authenticate and repeated password failures trigger a timed lockout.

**Architecture:** Two surgical changes to `PublicUserSignInService.SignInAsync` (Status check + lockout) and one line added to `PortalPolicies.Configure` (policy-level Status claim requirement). No new services, no new entities, no DB migration — `FailedLoginAttempts` and `LockoutUntil` already exist on `PublicUsers` (added in migration `20260504094618_ExternalPortalShellPortalAuthAndClaims`). MFA enforcement (S1b) is deliberately deferred until the MFA enrollment UI is built.

**Tech Stack:** ASP.NET Core 10 MVC, ASP.NET Authorization, xUnit + Moq, `TestDbContextFactory.Create()`, `PublicUserBuilder`.

---

## File Map

| Action | Path | Fix |
|--------|------|-----|
| Modify | `Services/Portal/Auth/PublicUserSignInService.cs` | Tasks 1 + 2 |
| Modify | `Services/Portal/Auth/PortalPolicies.cs` | Task 3 |
| Modify | `Tests/Services/Portal/Auth/PublicUserSignInServiceTests.cs` | Tasks 1 + 2 |

---

## Task 1 — Status check: refuse Suspended and Deactivated accounts at sign-in

**Finding:** S1 (High). `PublicUserSignInService.SignInAsync` never checks `user.Status` before issuing the portal cookie. A Suspended user who knows their correct password can sign in.

**Security note:** The Status check is placed **after** password verification on purpose. This prevents leaking account-status information to an attacker who does not know the password — a wrong-password attempt always gets `GenericLoginFailed` regardless of whether the account is suspended.

**Files:**
- Modify: `Services/Portal/Auth/PublicUserSignInService.cs`
- Modify: `Tests/Services/Portal/Auth/PublicUserSignInServiceTests.cs`

- [ ] **Step 1.1 — Write the failing tests**

Add to `Tests/Services/Portal/Auth/PublicUserSignInServiceTests.cs`, inside `public class PublicUserSignInServiceTests`:

```csharp
[Fact]
public async Task SignInAsync_SuspendedUser_ReturnsGenericError_DoesNotIssueCookie()
{
    var (sut, auth, audit, _) = CreateSut(db =>
    {
        var user = PublicUserBuilder.Suspended("s@e.test");
        user.PasswordHash = new PasswordHasher<PublicUser>().HashPassword(user, "Goodpassword12!");
        db.PublicUsers.Add(user);
    });

    var result = await sut.SignInAsync("s@e.test", "Goodpassword12!", default);

    Assert.False(result.Success);
    Assert.Equal("Login failed.", result.Error);
    auth.Verify(a => a.SignInAsync(
        It.IsAny<HttpContext>(), It.IsAny<string>(),
        It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()), Times.Never);
    Assert.Contains(audit.Events, e => e.Action == "PublicUserSignInFailed" && e.Reason == "AccountSuspended");
}

[Fact]
public async Task SignInAsync_DeactivatedUser_ReturnsGenericError_DoesNotIssueCookie()
{
    var (sut, auth, audit, _) = CreateSut(db =>
    {
        var user = new PublicUser
        {
            EmailAddress = "d@e.test",
            PasswordHash = new PasswordHasher<PublicUser>().HashPassword(new PublicUser
            {
                EmailAddress = "d@e.test", PasswordHash = "", FirstName = "D",
                LastName = "U", Status = "Deactivated"
            }, "Goodpassword12!"),
            FirstName = "D",
            LastName = "U",
            Status = "Deactivated",
            EmailConfirmed = true,
            RegistrationDate = DateTime.UtcNow
        };
        db.PublicUsers.Add(user);
    });

    var result = await sut.SignInAsync("d@e.test", "Goodpassword12!", default);

    Assert.False(result.Success);
    Assert.Equal("Login failed.", result.Error);
    auth.Verify(a => a.SignInAsync(
        It.IsAny<HttpContext>(), It.IsAny<string>(),
        It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()), Times.Never);
    Assert.Contains(audit.Events, e => e.Action == "PublicUserSignInFailed" && e.Reason == "AccountSuspended");
}
```

- [ ] **Step 1.2 — Run the failing tests**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai"
dotnet test Tests/ --filter "SignInAsync_SuspendedUser|SignInAsync_DeactivatedUser" -v
```

Expected: FAIL — both tests fail because the current `SignInAsync` issues the cookie without checking Status.

- [ ] **Step 1.3 — Implement the Status check**

In `Services/Portal/Auth/PublicUserSignInService.cs`, add a new constant and insert the Status check after the `EmailConfirmed` block.

Add constant after `EmailNotConfirmed`:

```csharp
public const string GenericLoginFailed = "Login failed.";
public const string EmailNotConfirmed = "Please confirm your email before logging in.";
// (no new public constant — suspended accounts receive GenericLoginFailed to avoid info leak)
```

Add the check in `SignInAsync`, immediately after the `if (!user.EmailConfirmed)` block (currently around line 67):

```csharp
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
```

The rest of the method (identity building, `SignInAsync`, `SaveChangesAsync`) is unchanged.

- [ ] **Step 1.4 — Run the tests**

```bash
dotnet test Tests/ --filter "SignInAsync_SuspendedUser|SignInAsync_DeactivatedUser" -v
```

Expected: PASS — both tests green.

- [ ] **Step 1.5 — Run the full sign-in test class to check for regressions**

```bash
dotnet test Tests/ --filter "FullyQualifiedName~PublicUserSignInServiceTests" -v
```

Expected: All existing tests still pass (happy path, wrong password, unknown email, unconfirmed email, sign-out).

- [ ] **Step 1.6 — Commit**

```bash
git add "Services/Portal/Auth/PublicUserSignInService.cs" \
        "Tests/Services/Portal/Auth/PublicUserSignInServiceTests.cs"
git commit -m "fix(auth): refuse Suspended/Deactivated accounts at portal sign-in (S1)"
```

---

## Task 2 — Account lockout: increment counter on failure, block after 5 attempts

**Finding:** S2 (High). `PublicUser` has `FailedLoginAttempts` (int) and `LockoutUntil` (DateTime?) but `SignInAsync` never reads or writes them. An attacker can brute-force passwords indefinitely.

**Lockout rules:**
- 5 consecutive failures → `LockoutUntil = DateTime.UtcNow + 15 minutes`
- Check lockout **before** verifying the password (no wasted hashing for locked accounts)
- On wrong password: increment counter, conditionally set `LockoutUntil`, save immediately
- On successful sign-in: reset both `FailedLoginAttempts = 0` and `LockoutUntil = null` before saving

**Files:**
- Modify: `Services/Portal/Auth/PublicUserSignInService.cs`
- Modify: `Tests/Services/Portal/Auth/PublicUserSignInServiceTests.cs`

- [ ] **Step 2.1 — Write the failing tests**

Add to `Tests/Services/Portal/Auth/PublicUserSignInServiceTests.cs`:

```csharp
[Fact]
public async Task SignInAsync_LockedOutUser_BlocksWithoutCheckingPassword()
{
    var (sut, auth, audit, db) = CreateSut(db =>
    {
        var user = HashedActive("l@e.test", "Goodpassword12!");
        user.FailedLoginAttempts = 5;
        user.LockoutUntil = DateTime.UtcNow.AddMinutes(10);
        db.PublicUsers.Add(user);
    });

    var result = await sut.SignInAsync("l@e.test", "Goodpassword12!", default);

    Assert.False(result.Success);
    Assert.Equal("Login failed.", result.Error);
    auth.Verify(a => a.SignInAsync(
        It.IsAny<HttpContext>(), It.IsAny<string>(),
        It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()), Times.Never);
    Assert.Contains(audit.Events, e => e.Action == "PublicUserSignInFailed" && e.Reason == "AccountLocked");
}

[Fact]
public async Task SignInAsync_WrongPassword_IncrementsFailedAttempts()
{
    var (sut, _, _, db) = CreateSut(db =>
    {
        var user = HashedActive("f@e.test", "Goodpassword12!");
        user.FailedLoginAttempts = 2;
        db.PublicUsers.Add(user);
    });

    await sut.SignInAsync("f@e.test", "wrongpassword!", default);

    var stored = db.PublicUsers.Single(u => u.EmailAddress == "f@e.test");
    Assert.Equal(3, stored.FailedLoginAttempts);
    Assert.Null(stored.LockoutUntil);
}

[Fact]
public async Task SignInAsync_FifthWrongPassword_SetsLockoutUntil()
{
    var (sut, _, _, db) = CreateSut(db =>
    {
        var user = HashedActive("ff@e.test", "Goodpassword12!");
        user.FailedLoginAttempts = 4;
        db.PublicUsers.Add(user);
    });

    await sut.SignInAsync("ff@e.test", "wrongpassword!", default);

    var stored = db.PublicUsers.Single(u => u.EmailAddress == "ff@e.test");
    Assert.Equal(5, stored.FailedLoginAttempts);
    Assert.NotNull(stored.LockoutUntil);
    Assert.True(stored.LockoutUntil > DateTime.UtcNow.AddMinutes(1));
}

[Fact]
public async Task SignInAsync_SuccessAfterPreviousFailures_ResetsLockout()
{
    var (sut, _, _, db) = CreateSut(db =>
    {
        var user = HashedActive("r@e.test", "Goodpassword12!");
        user.FailedLoginAttempts = 3;
        db.PublicUsers.Add(user);
    });

    var result = await sut.SignInAsync("r@e.test", "Goodpassword12!", default);

    Assert.True(result.Success);
    var stored = db.PublicUsers.Single(u => u.EmailAddress == "r@e.test");
    Assert.Equal(0, stored.FailedLoginAttempts);
    Assert.Null(stored.LockoutUntil);
}
```

- [ ] **Step 2.2 — Run the failing tests**

```bash
dotnet test Tests/ --filter "SignInAsync_LockedOutUser|SignInAsync_WrongPassword_Increments|SignInAsync_FifthWrongPassword|SignInAsync_SuccessAfterPreviousFailures" -v
```

Expected: FAIL — all four tests fail because lockout is not yet implemented.

- [ ] **Step 2.3 — Implement lockout**

Replace the full `SignInAsync` method in `Services/Portal/Auth/PublicUserSignInService.cs` with:

```csharp
public const string GenericLoginFailed = "Login failed.";
public const string EmailNotConfirmed = "Please confirm your email before logging in.";
public const int MaxFailedAttempts = 5;
public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

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
```

**Note on constant placement:** The constants (`MaxFailedAttempts`, `LockoutDuration`, and the existing string constants) are public members of `PublicUserSignInService`. Place them at the top of the class body, before the constructor. The two existing string constants (`GenericLoginFailed`, `EmailNotConfirmed`) are already public; just add the two new ones alongside them.

- [ ] **Step 2.4 — Run the failing tests**

```bash
dotnet test Tests/ --filter "SignInAsync_LockedOutUser|SignInAsync_WrongPassword_Increments|SignInAsync_FifthWrongPassword|SignInAsync_SuccessAfterPreviousFailures" -v
```

Expected: PASS — all four tests green.

- [ ] **Step 2.5 — Run the full sign-in test class**

```bash
dotnet test Tests/ --filter "FullyQualifiedName~PublicUserSignInServiceTests" -v
```

Expected: All tests pass (existing happy path, wrong password, unknown email, unconfirmed email, sign-out, plus the four new lockout tests and two new Status tests from Task 1).

- [ ] **Step 2.6 — Commit**

```bash
git add "Services/Portal/Auth/PublicUserSignInService.cs" \
        "Tests/Services/Portal/Auth/PublicUserSignInServiceTests.cs"
git commit -m "fix(auth): implement account lockout in PublicUserSignInService (S2)"
```

---

## Task 3 — PortalPolicies: add Status=Active requirement as defence-in-depth

**Finding:** S1 (High). `PortalAuthenticated` policy only requires `EmailConfirmed=true`. A Suspended user who obtained a cookie before suspension could use that cookie for the remainder of its 8-hour lifetime.

**Why both the service check AND the policy check are needed:**
- Service check (Task 1): prevents NEW cookies being issued to Suspended/Deactivated users.
- Policy check (Task 3): rejects EXISTING cookies that carry `Status=Suspended` (e.g., user suspended mid-session).

The `StatusClaim` is already stamped with the user's actual Status at sign-in time (`new Claim(PortalPolicies.StatusClaim, user.Status)`), so adding `RequireClaim(StatusClaim, "Active")` to the policy will correctly block existing Suspended-user cookies immediately.

**MFA note:** `RequireClaim(MfaEnrolledClaim, "true")` is **not** added in this sprint. MFA enrollment UI is not yet built; adding the policy now would lock out all demo users.

**Files:**
- Modify: `Services/Portal/Auth/PortalPolicies.cs`

- [ ] **Step 3.1 — Update PortalPolicies.Configure**

Replace the `PortalAuthenticated` and `PortalRegistrationComplete` policy registrations in `Services/Portal/Auth/PortalPolicies.cs`:

```csharp
public static void Configure(AuthorizationOptions options)
{
    // Stage 2a: PortalAuthenticated requires cookie + EmailConfirmed=true + Status=Active.
    // (Stage 2b will add MfaEnrolled=true once the MFA enrollment UI is built.)
    options.AddPolicy(PortalAuthenticated, p => p
        .AddAuthenticationSchemes(PortalCookieOptions.SchemeName)
        .RequireAuthenticatedUser()
        .RequireClaim(EmailConfirmedClaim, "true")
        .RequireClaim(StatusClaim, "Active"));

    // PortalRegistrationComplete mirrors PortalAuthenticated for Stage 2a.
    options.AddPolicy(PortalRegistrationComplete, p => p
        .AddAuthenticationSchemes(PortalCookieOptions.SchemeName)
        .RequireAuthenticatedUser()
        .RequireClaim(EmailConfirmedClaim, "true")
        .RequireClaim(StatusClaim, "Active"));

    // Stage 2b only: short-lived cookie carrying MfaPending=true between
    // password verification and TOTP entry. Defined now so AccountController
    // can reference the constant without a compile error if Stage 2b is
    // partially landed.
    options.AddPolicy(PortalMfaPending, p => p
        .AddAuthenticationSchemes(PortalCookieOptions.SchemeName)
        .RequireAuthenticatedUser()
        .RequireClaim("MfaPending", "true"));
}
```

- [ ] **Step 3.2 — Build to verify no compile errors**

```bash
dotnet build "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" -c Release --no-restore 2>&1 | tail -20
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 3.3 — Commit**

```bash
git add "Services/Portal/Auth/PortalPolicies.cs"
git commit -m "fix(auth): require Status=Active claim in PortalAuthenticated policy (S1 defence-in-depth)"
```

---

## Task 4 — Full test run

**Goal:** Confirm all tests pass after all three auth hardening tasks.

- [ ] **Step 4.1 — Run the complete test suite**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai"
dotnet test Tests/ -v 2>&1 | tail -30
```

Expected: All tests pass with 0 failures. Count should be the Wave 3 baseline (258 tests) plus the 6 new tests added in Tasks 1 and 2 = **264 tests passing**.

- [ ] **Step 4.2 — Release build check**

```bash
dotnet build "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" -c Release 2>&1 | tail -10
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 4.3 — Commit**

```bash
git add --dry-run
# verify nothing unintended is staged, then:
git commit --allow-empty -m "chore: auth hardening sprint 2 complete — 264 tests passing"
```

---

## Self-Review

**Spec coverage:**

| Finding | Task | Covered? |
|---------|------|----------|
| S1: Suspended user can sign in | Task 1 | ✅ Status check in SignInAsync |
| S1: Suspended cookie accepted by policy | Task 3 | ✅ RequireClaim(StatusClaim, "Active") |
| S2: Lockout fields never read/written | Task 2 | ✅ Full lockout cycle implemented |
| S1b: MFA not enforced in policy | Deferred | ✅ Explicitly deferred (MFA UI not built) |

**Placeholder scan:** No TBD, TODO, or "similar to" references. All steps contain exact code.

**Type consistency:**
- `MaxFailedAttempts` and `LockoutDuration` defined in Task 2 constants block; referenced only in the same `SignInAsync` method body — no cross-task type drift.
- `AuditEvent` record constructor matches pattern used in all other `SignInAsync` calls in the same method: `(EntityType, EntityId, Action, Reason, ToValue)`.
- `PublicUserBuilder.Suspended()` (from `Tests/Helpers/PublicUserBuilder.cs`) used in Task 1 test — confirmed it sets `Status = "Suspended"` and `EmailConfirmed = true`, which is exactly what the test needs.
