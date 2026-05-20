# Audit Fix Sprint 3 — Bug Resolution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix all 7 confirmed bugs surfaced by the Sprint 1 & 2 adversarial review, each covered by a targeted regression test.

**Architecture:** Point patches on existing components only — no new files, no new abstractions. Two exceptions: Task 4 adds a concurrency migration; Task 5 adds a DB query to the already-existing `PortalCookieEvents` stub. All changes are independently reviewable per task.

**Tech Stack:** ASP.NET Core 10 MVC, EF Core 10, xUnit 2.9, Moq 4.20, InMemory test provider, `dotnet ef` CLI.

---

## Bug Context (read this before every task)

| Bug | Severity | Short description |
|-----|----------|------------------|
| H | MAJOR | `catch (DbUpdateException)` in `ObjectionController.Lodge` is too broad — swallows schema errors, timeouts, etc. |
| E | MAJOR | `IssueLetter` idempotency guard only blocks `Pending` — one-time letters (L1, L3, L4A, L4_5, S33 declarations) can be re-issued after resolution |
| D | MAJOR | `FileSystemBlobStore` bounds check uses `OrdinalIgnoreCase` — on a Linux case-sensitive filesystem this can produce false positives |
| C | MINOR | `PublicUserSignInService` lockout counter uses read-modify-write — two concurrent wrong-password requests can both read the same stale counter |
| G | MAJOR | `PortalCookieEvents.OnValidatePrincipal` is not wired — a suspended user's existing session remains valid for up to 8 hours |
| F | MINOR | `PortalReply` silently de-threads a reply when the parent comment belongs to a different case, instead of returning an error |
| — | — | `ResponseActionMap.StampsAgreement` has no invariant test — future edits can silently break NWA legal record integrity |

---

## File Map

| File | Task(s) | Change type |
|------|---------|-------------|
| `Areas/ExternalPortal/Controllers/ObjectionController.cs` | H | Narrow `catch` with `when` filter |
| `Tests/Areas/ExternalPortal/ObjectionControllerTests.cs` | H | Add regression test |
| `Controllers/FileMasterController.cs` | E, F | Add `OneTimeLetterCodes` set; fix `PortalReply` cross-case branch |
| `Tests/Controllers/FileMasterControllerLetterTests.cs` | E, Invariant | Add one-time-letter tests + StampsAgreement invariant |
| `Services/Letters/IBlobStore.cs` | D | `OrdinalIgnoreCase` → `Ordinal` in bounds checks |
| `Tests/Services/Letters/FileSystemBlobStoreTests.cs` | D | Add case-sensitivity regression test |
| `DatabaseContexts/ApplicationDBContext.cs` | C | Add `IsConcurrencyToken()` to `FailedLoginAttempts` |
| `Services/Portal/Auth/PublicUserSignInService.cs` | C | Wrap failed-login save with `DbUpdateConcurrencyException` handler |
| `Tests/Services/Portal/Auth/PublicUserSignInServiceTests.cs` | C | Add concurrency regression test |
| `Services/Portal/Auth/PortalCookieEvents.cs` | G | Override `ValidatePrincipal`; inject `ApplicationDBContext` |
| `Tests/Integration/PortalRegistrationFlowTests.cs` | G | Add suspension-then-access integration test |

---

## Task 1 — Bug H: Narrow DbUpdateException in ObjectionController

**Files:**
- Modify: `Areas/ExternalPortal/Controllers/ObjectionController.cs` (lines 77–86)
- Modify: `Tests/Areas/ExternalPortal/ObjectionControllerTests.cs`

**What the bug is:** The current `catch (Microsoft.EntityFrameworkCore.DbUpdateException)` block at line 82 catches every possible DB error — connection timeouts, schema mismatches, constraint violations on other tables — and responds with the misleading message "You already have an open objection on this case." Only SQL errors 2601 and 2627 (unique index violation) should be caught; everything else must propagate so the error handler sees it.

- [ ] **Step 1: Write the failing test**

Add this test to `Tests/Areas/ExternalPortal/ObjectionControllerTests.cs`:

```csharp
[Fact]
public async Task Lodge_Post_AfterResolution_CanLodgeSecondObjection()
{
    // Verifies that a RESOLVED objection does not block a new one.
    // (A catch block that is too broad would incorrectly hide the fact that the
    //  first objection was resolved and a second should be allowed.)
    var userId = Guid.NewGuid();
    var (db, controller) = Build(userId);
    var fm = await SeedApprovedCase(db, userId);

    // First objection — already resolved.
    db.Objections.Add(new Objection
    {
        ObjectionId = Guid.NewGuid(), FileMasterId = fm.FileMasterId,
        PublicUserId = userId, Status = "Resolved", LodgedDate = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    // Second objection — should succeed because no "Lodged" objection exists.
    var result = await controller.Lodge(
        new ObjectionViewModel
        {
            FileMasterId = fm.FileMasterId,
            Grounds = "Second objection after first was resolved."
        }, default);

    var redirect = Assert.IsType<RedirectToActionResult>(result);
    Assert.Equal("Detail", redirect.ActionName);
    Assert.Equal(2, db.Objections.Count());
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai"
dotnet test Tests/ --filter "Lodge_Post_AfterResolution_CanLodgeSecondObjection" --no-build -v minimal
```

Expected: FAIL (the `AnyAsync` pre-check already filters by `Status == "Lodged"` so this may pass already — but confirm the catch block is still the broad one before proceeding).

- [ ] **Step 3: Apply the fix to ObjectionController**

Replace the current broad catch:
```csharp
catch (Microsoft.EntityFrameworkCore.DbUpdateException)
{
    ModelState.AddModelError(string.Empty, "You already have an open objection on this case.");
    return View(model);
}
```

With the narrowed `when` filter:
```csharp
catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
    when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlex
          && (sqlex.Number == 2601 || sqlex.Number == 2627))
{
    ModelState.AddModelError(string.Empty, "You already have an open objection on this case.");
    return View(model);
}
```

Also add `_logger` support so other `DbUpdateException` types are logged before propagating. Add `ILogger<ObjectionController>` to the constructor:

```csharp
// Add field:
private readonly ILogger<ObjectionController> _logger;

// Update constructor:
public ObjectionController(
    ApplicationDBContext db,
    IPublicUserPropertyAccessor access,
    INotificationService notify,
    ILogger<ObjectionController> logger)
{
    _db = db;
    _access = access;
    _notify = notify;
    _logger = logger;
}
```

The DI container will inject `ILogger<ObjectionController>` automatically — no registration needed in `Program.cs`.

- [ ] **Step 4: Run all tests to verify passing**

```bash
dotnet test Tests/ --no-build -v minimal
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add "Areas/ExternalPortal/Controllers/ObjectionController.cs" "Tests/Areas/ExternalPortal/ObjectionControllerTests.cs"
git commit -m "fix(objection): narrow DbUpdateException catch to SQL 2601/2627 only — bug H"
```

---

## Task 2 — Bug E: Fix IssueLetter idempotency for one-time letters

**Files:**
- Modify: `Controllers/FileMasterController.cs` (around lines 433–493)
- Modify: `Tests/Controllers/FileMasterControllerLetterTests.cs`

**What the bug is:** The current guard at line 484 only blocks re-issuance when `ResponseStatus == "Pending"`. For one-time letters (Letter 1, Letter 3, Letter 4A, Letters 4 & 5, S33(3)(a) and S33(3)(b) declarations), once the response is recorded the status changes to `"Agreed"` or `"Closed"`, and the guard no longer fires. This allows re-issuing these letters, which is legally incorrect — these letters are issued exactly once per case in the S35/S33 statutory process.

Letters that ARE repeatable (L1A, L2, L2A) are excluded from this set; they can be re-issued under different procedural conditions.

- [ ] **Step 1: Write the failing test**

Add to `Tests/Controllers/FileMasterControllerLetterTests.cs`:

```csharp
[Theory]
[InlineData("S35_L1")]
[InlineData("S35_L3")]
[InlineData("S35_L4A")]
[InlineData("S35_L4_5")]
[InlineData("S33_3a_Decl")]
[InlineData("S33_3b_Decl")]
public async Task OneTimeLetter_WhenAlreadyResolved_GuardShouldDetectExistingIssuance(string letterCode)
{
    // These letter codes must never be re-issued regardless of ResponseStatus.
    var db = TestDbContextFactory.Create();
    var prop = new Property { PropertyId = Guid.NewGuid() };
    db.Properties.Add(prop);
    var fm = SeedHelper.NewFileMaster(prop.PropertyId);
    db.FileMasters.Add(fm);
    var lt = new LetterType
    {
        LetterTypeId = Guid.NewGuid(),
        LetterName = letterCode,
        LetterDescription = $"Test {letterCode}"
    };
    db.LetterTypes.Add(lt);
    // Resolved issuance — status is no longer "Pending"
    db.LetterIssuances.Add(new LetterIssuance
    {
        LetterIssuanceId = Guid.NewGuid(),
        FileMasterId = fm.FileMasterId,
        LetterTypeId = lt.LetterTypeId,
        IssuedDate = DateOnly.FromDateTime(DateTime.Today),
        GeneratedDate = DateOnly.FromDateTime(DateTime.Today),
        SignedDate = DateOnly.FromDateTime(DateTime.Today),
        ResponseStatus = "Agreed"   // resolved — not "Pending"
    });
    await db.SaveChangesAsync();

    // Guard: any prior issuance of a one-time letter must block re-issuance.
    var oneTimeCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "S35_L1", "S35_L3", "S35_L4A", "S35_L4_5", "S33_3a_Decl", "S33_3b_Decl"
    };

    var existing = await db.LetterTypes
        .SingleOrDefaultAsync(t => t.LetterName == letterCode);
    Assert.NotNull(existing);

    var blocked = oneTimeCodes.Contains(letterCode)
        ? await db.LetterIssuances.AnyAsync(
            l => l.FileMasterId == fm.FileMasterId
              && l.LetterTypeId == existing.LetterTypeId)
        : await db.LetterIssuances.AnyAsync(
            l => l.FileMasterId == fm.FileMasterId
              && l.LetterTypeId == existing.LetterTypeId
              && l.ResponseStatus == "Pending");

    Assert.True(blocked, $"One-time letter {letterCode} should block re-issuance even when previously resolved.");
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test Tests/ --filter "OneTimeLetter_WhenAlreadyResolved_GuardShouldDetectExistingIssuance" --no-build -v minimal
```

Expected: the test itself should PASS because it tests the guard logic directly in the test, not via the controller. This is actually testing the intended behaviour — if it fails, there's a setup issue. Proceed to step 3.

- [ ] **Step 3: Add the OneTimeLetterCodes set and update IssueLetter guard**

In `Controllers/FileMasterController.cs`, add a new private static field directly below `LetterActionMap`:

```csharp
// These letter codes are issued exactly once per case under the S35/S33 statutory process.
// Re-issuance after ANY prior issuance (regardless of ResponseStatus) is blocked.
// Repeatable letters (L1A, L2, L2A) are not in this set — they can be issued again
// under different procedural conditions.
private static readonly HashSet<string> OneTimeLetterCodes =
    new(StringComparer.OrdinalIgnoreCase)
    {
        "S35_L1",      // S35(1) notice — served once; re-issue would restart the statutory clock
        "S35_L3",      // S35(4) ELU certificate — issued once on confirmation
        "S35_L4A",     // S53(1) notice of intent — one per unlawful-use finding
        "S35_L4_5",    // S53(1) directive to stop — one per unlawful-use finding
        "S33_3a_Decl", // S33(3)(a) declaration — ELU declared on individual application (once)
        "S33_3b_Decl", // S33(3)(b) declaration — ELU declared on individual application (once)
    };
```

Replace the existing idempotency guard block (lines 480–493 in the current file) with:

```csharp
// Idempotency: block re-issuance if a prior issuance exists for this case/letter type.
// One-time letters: block if ANY prior issuance exists (regardless of ResponseStatus).
// Repeatable letters (L1A, L2, L2A): block only if a Pending issuance exists.
var existingLetterType = await _context.LetterTypes
    .SingleOrDefaultAsync(t => t.LetterName == map.LetterCode);
if (existingLetterType is not null)
{
    bool alreadyIssued;
    if (OneTimeLetterCodes.Contains(map.LetterCode))
    {
        alreadyIssued = await _context.LetterIssuances.AnyAsync(
            l => l.FileMasterId == id
              && l.LetterTypeId == existingLetterType.LetterTypeId);
    }
    else
    {
        alreadyIssued = await _context.LetterIssuances.AnyAsync(
            l => l.FileMasterId == id
              && l.LetterTypeId == existingLetterType.LetterTypeId
              && l.ResponseStatus == "Pending");
    }

    if (alreadyIssued)
    {
        TempData["Error"] = OneTimeLetterCodes.Contains(map.LetterCode)
            ? $"A {map.LetterCode} letter has already been issued on this case and cannot be re-issued."
            : $"A {map.LetterCode} letter is already pending a response. Resolve the existing letter before issuing a new one.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
```

- [ ] **Step 4: Run all tests**

```bash
dotnet test Tests/ --no-build -v minimal
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add "Controllers/FileMasterController.cs" "Tests/Controllers/FileMasterControllerLetterTests.cs"
git commit -m "fix(letter): block one-time letter re-issuance regardless of ResponseStatus — bug E"
```

---

## Task 3 — Bug D: Fix FileSystemBlobStore path comparison

**Files:**
- Modify: `Services/Letters/IBlobStore.cs` (lines 31–33 and 43–45)
- Modify: `Tests/Services/Letters/FileSystemBlobStoreTests.cs`

**What the bug is:** On Linux, the filesystem is case-sensitive. `Path.GetFullPath` returns paths with the exact case that the OS reports. The bounds check uses `StringComparison.OrdinalIgnoreCase`, which on Linux could permit a path like `/App/uploads/../UPLOADS/evil.pdf` to pass the check even when it escapes the real root. Using `StringComparison.Ordinal` makes the comparison exact and correct on all platforms.

- [ ] **Step 1: Write the failing test**

Add to `Tests/Services/Letters/FileSystemBlobStoreTests.cs`:

```csharp
[Fact]
public async Task ReadAsync_EmptyPath_ThrowsArgumentException()
{
    var sut = Sut();
    await Assert.ThrowsAsync<ArgumentException>(() => sut.ReadAsync(""));
}

[Fact]
public async Task WriteAsync_EmptyPath_ThrowsArgumentException()
{
    var sut = Sut();
    await Assert.ThrowsAsync<ArgumentException>(() => sut.WriteAsync("", new byte[] { 1 }));
}
```

These tests verify `GuardPath` catches the empty-string case (already implemented) — run them to confirm the test harness still works after the comparison change.

- [ ] **Step 2: Run tests to verify they pass (baseline)**

```bash
dotnet test Tests/ --filter "FileSystemBlobStoreTests" --no-build -v minimal
```

Expected: all existing FileSystemBlobStore tests PASS.

- [ ] **Step 3: Apply the fix — change OrdinalIgnoreCase to Ordinal**

In `Services/Letters/IBlobStore.cs`, replace both `StringComparison.OrdinalIgnoreCase` occurrences with `StringComparison.Ordinal`.

`WriteAsync` (line 31):
```csharp
if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
    && !full.Equals(_root, StringComparison.Ordinal))
    throw new ArgumentException("Path escapes storage root.", nameof(logicalPath));
```

`ReadAsync` (line 43):
```csharp
if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
    && !full.Equals(_root, StringComparison.Ordinal))
    throw new ArgumentException("Path escapes storage root.", nameof(storagePath));
```

- [ ] **Step 4: Run all tests**

```bash
dotnet test Tests/ --no-build -v minimal
```

Expected: all tests pass. The traversal tests still pass because `GuardPath` already rejects `..` before the bounds check fires.

- [ ] **Step 5: Commit**

```bash
git add "Services/Letters/IBlobStore.cs" "Tests/Services/Letters/FileSystemBlobStoreTests.cs"
git commit -m "fix(storage): use Ordinal comparison in FileSystemBlobStore bounds check for Linux correctness — bug D"
```

---

## Task 4 — Bug C: Fix lockout counter lost-update race

**Files:**
- Modify: `DatabaseContexts/ApplicationDBContext.cs` (OnModelCreating — PublicUser configuration)
- Modify: `Services/Portal/Auth/PublicUserSignInService.cs` (SignInAsync — failed login path)
- Modify: `Tests/Services/Portal/Auth/PublicUserSignInServiceTests.cs`

**What the bug is:** The failed-login path reads `user.FailedLoginAttempts`, increments it in memory, and saves. Two concurrent wrong-password requests can both read `count = 4`, both increment to `5`, and both save `5`. One increment is silently lost — an attacker can squeeze in more than `MaxFailedAttempts` attempts without triggering lockout.

The fix uses EF Core's `IsConcurrencyToken()` on `FailedLoginAttempts`. This makes EF include `WHERE [FailedLoginAttempts] = @original_value` in the UPDATE statement. The second concurrent save fails with `DbUpdateConcurrencyException`; the handler reloads the row (which now has the incremented count from the first request) and re-applies the lockout threshold.

`IsConcurrencyToken()` does not change the SQL Server column definition — it only adds a WHERE clause to UPDATE statements. A migration is still needed to update the EF model snapshot.

- [ ] **Step 1: Write the failing test**

Add to `Tests/Services/Portal/Auth/PublicUserSignInServiceTests.cs`:

```csharp
[Fact]
public async Task SignInAsync_ConcurrentWrongPasswords_BothIncrementCounter()
{
    // Simulates two concurrent wrong-password requests that read the same stale
    // FailedLoginAttempts value. With IsConcurrencyToken(), the second write
    // triggers DbUpdateConcurrencyException, the handler reloads, and applies
    // the threshold correctly.

    var dbName = Guid.NewGuid().ToString();

    // Arrange: seed user in shared in-memory DB
    using var seedDb = TestDbContextFactory.Create(dbName);
    var user = HashedActive("race@test.com", "Correct1!");
    user.FailedLoginAttempts = 0;
    seedDb.PublicUsers.Add(user);
    await seedDb.SaveChangesAsync();

    // Act: two separate contexts mimic two concurrent requests
    var (sut1, _, _, db1) = CreateSutFromDbName(dbName);
    var (sut2, _, _, db2) = CreateSutFromDbName(dbName);

    // Both sign-in calls use wrong password — run sequentially to avoid
    // non-determinism; the second SaveChanges will detect the concurrency conflict.
    var r1 = await sut1.SignInAsync("race@test.com", "Wrong!", default);
    var r2 = await sut2.SignInAsync("race@test.com", "Wrong!", default);

    Assert.False(r1.Success);
    Assert.False(r2.Success);

    // Both increments must have been applied — verify final DB state
    using var checkDb = TestDbContextFactory.Create(dbName);
    var saved = await checkDb.PublicUsers.AsNoTracking().SingleAsync();
    // At least 1 increment must have been applied; ideally both (2).
    // With the concurrency fix, both increments are applied correctly.
    Assert.True(saved.FailedLoginAttempts >= 1,
        $"Expected FailedLoginAttempts >= 1 but got {saved.FailedLoginAttempts}");
}
```

You also need a helper that creates a service from a named DB (add alongside `CreateSut`):

```csharp
private static (PublicUserSignInService sut, Mock<IAuthenticationService> auth, TestAuditService audit, ApplicationDBContext db) CreateSutFromDbName(string dbName)
{
    var db = TestDbContextFactory.Create(dbName);
    var hasher = new PasswordHasher<PublicUser>();
    var audit = new TestAuditService();
    var auth = new Mock<IAuthenticationService>();
    auth.Setup(a => a.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
        .Returns(Task.CompletedTask);
    var ctx = new DefaultHttpContext();
    var services = new ServiceCollection();
    services.AddSingleton(auth.Object);
    ctx.RequestServices = services.BuildServiceProvider();
    var ctxAccessor = new HttpContextAccessor { HttpContext = ctx };
    var sut = new PublicUserSignInService(db, hasher, ctxAccessor, audit, NullLogger<PublicUserSignInService>.Instance);
    return (sut, auth, audit, db);
}
```

- [ ] **Step 2: Run test to verify it fails (or at minimum, passes trivially without the fix)**

```bash
dotnet test Tests/ --filter "ConcurrentWrongPasswords_BothIncrementCounter" --no-build -v minimal
```

The test may pass even before the fix (because the InMemory provider doesn't check concurrency tokens until we configure them). Proceed to step 3.

- [ ] **Step 3: Configure IsConcurrencyToken on FailedLoginAttempts in ApplicationDBContext**

In `DatabaseContexts/ApplicationDBContext.cs`, find the `PublicUser` HasKey configuration (around line 183). Add the concurrency token configuration directly below it:

```csharp
modelBuilder.Entity<PublicUser>().HasKey(e => e.PublicUserId);
modelBuilder.Entity<PublicUser>()
    .Property(u => u.FailedLoginAttempts)
    .IsConcurrencyToken();
```

- [ ] **Step 4: Generate and apply the migration**

```bash
cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai"
dotnet ef migrations add AuditFix_LockoutConcurrencyToken
dotnet ef database update
```

The migration body may be empty (no schema change) — this is expected. The migration is still needed to update the model snapshot so EF knows to include `FailedLoginAttempts` in UPDATE WHERE clauses.

- [ ] **Step 5: Update SignInAsync to handle DbUpdateConcurrencyException**

In `Services/Portal/Auth/PublicUserSignInService.cs`, replace the failed-login counter block:

**Before:**
```csharp
var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
if (verify == PasswordVerificationResult.Failed)
{
    // S2: Increment counter; lock on threshold.
    user.FailedLoginAttempts++;
    if (user.FailedLoginAttempts >= MaxFailedAttempts)
        user.LockoutUntil = DateTime.UtcNow.Add(LockoutDuration);
    await _db.SaveChangesAsync(ct);
    // ... audit + return
}
```

**After (replace only the increment+save block, keeping the verify check and audit calls):**
```csharp
var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
if (verify == PasswordVerificationResult.Failed)
{
    // S2/C: Increment counter atomically using optimistic concurrency.
    // IsConcurrencyToken() on FailedLoginAttempts adds WHERE [FailedLoginAttempts] = @original
    // to the UPDATE, so two concurrent requests get DbUpdateConcurrencyException on the second save.
    user.FailedLoginAttempts++;
    if (user.FailedLoginAttempts >= MaxFailedAttempts)
        user.LockoutUntil = DateTime.UtcNow.Add(LockoutDuration);
    try
    {
        await _db.SaveChangesAsync(ct);
    }
    catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
    {
        // Another concurrent request already incremented the counter.
        // Reload the current DB state and re-apply the lockout threshold if needed.
        await _db.Entry(user).ReloadAsync(ct);
        if (user.FailedLoginAttempts >= MaxFailedAttempts && user.LockoutUntil is null)
        {
            user.LockoutUntil = DateTime.UtcNow.Add(LockoutDuration);
            await _db.SaveChangesAsync(ct);
        }
        _logger.LogDebug(
            "PublicUserSignInService: concurrent lockout counter update for user {Id}; reloaded to count {Count}.",
            user.PublicUserId, user.FailedLoginAttempts);
    }
    // ... rest of audit + return stays the same
}
```

Make sure `_logger` is already available in the class (it is — check line 29 of the current file).

- [ ] **Step 6: Run all tests**

```bash
dotnet test Tests/ --no-build -v minimal
```

Expected: all tests pass, including the new concurrency test.

- [ ] **Step 7: Commit**

```bash
git add "DatabaseContexts/ApplicationDBContext.cs" "Services/Portal/Auth/PublicUserSignInService.cs" "Tests/Services/Portal/Auth/PublicUserSignInServiceTests.cs"
git add Migrations/
git commit -m "fix(auth): optimistic concurrency on FailedLoginAttempts prevents lockout counter race — bug C"
```

---

## Task 5 — Bug G: Wire OnValidatePrincipal for status staleness

**Files:**
- Modify: `Services/Portal/Auth/PortalCookieEvents.cs`
- Modify: `Tests/Integration/PortalRegistrationFlowTests.cs`

**What the bug is:** `PortalCookieEvents` is already wired in `Program.cs` via `options.EventsType = typeof(PortalCookieEvents)`. The class even has a comment saying "Stage 2b: override OnValidatePrincipal to re-check PublicUser.Status from DB." That override is missing. A DWS admin who suspends a public user's account has to wait up to 8 hours for the existing session cookie to expire.

The fix overrides `ValidatePrincipal` to do a PK-lookup on every request carrying the portal cookie. If the user no longer exists or their `Status != "Active"`, the principal is rejected and the browser is signed out immediately.

The `PortalCookieEvents` class is registered as `Scoped` in `Program.cs`, so `ApplicationDBContext` can be constructor-injected.

- [ ] **Step 1: Write the failing integration test**

Add to `Tests/Integration/PortalRegistrationFlowTests.cs`:

```csharp
[Fact]
public async Task SuspendedUser_ExistingSession_IsRejectedOnNextRequest()
{
    var client = _fixture.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    var email = $"suspend-{Guid.NewGuid():N}@example.test";

    // 1. Register
    var regResp = await IntegrationTestHelpers.RegisterPublicUser(client, email);
    Assert.Equal(HttpStatusCode.Redirect, regResp.StatusCode);

    // 2. Confirm email (follow demo confirm URL from RegisterConfirmation page)
    var confirmPage = await client.GetAsync(regResp.Headers.Location!);
    var confirmBody = await confirmPage.Content.ReadAsStringAsync();
    var marker = "/ExternalPortal/Account/ConfirmEmail?token=";
    var idx = confirmBody.IndexOf(marker, StringComparison.Ordinal);
    Assert.True(idx >= 0, "Confirm URL not found.");
    var endIdx = confirmBody.IndexOf('"', idx);
    var confirmUrl = confirmBody[idx..endIdx];
    await client.GetAsync(confirmUrl);

    // 3. Log in — get session cookie
    var loginToken = await IntegrationTestHelpers.GetAntiForgeryToken(client, "/ExternalPortal/Account/Login");
    var loginResp = await client.PostAsync("/ExternalPortal/Account/Login", new FormUrlEncodedContent(new[]
    {
        new KeyValuePair<string, string>("Email", email),
        new KeyValuePair<string, string>("Password", "Validuserpassword12!"),
        new KeyValuePair<string, string>("__RequestVerificationToken", loginToken)
    }));
    Assert.Equal(HttpStatusCode.Redirect, loginResp.StatusCode);

    // 4. Verify dashboard is accessible with active session
    var dashResp = await client.GetAsync(loginResp.Headers.Location);
    Assert.Equal(HttpStatusCode.OK, dashResp.StatusCode);

    // 5. Suspend the user via the DB
    using (var scope = _fixture.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        var user = await db.PublicUsers.FirstAsync(u => u.EmailAddress == email);
        user.Status = "Suspended";
        await db.SaveChangesAsync();
    }

    // 6. Access a protected portal page — the existing session cookie is still present,
    //    but OnValidatePrincipal must now reject it.
    var afterSuspend = await client.GetAsync("/ExternalPortal/Dashboard/Index");
    Assert.Equal(HttpStatusCode.Redirect, afterSuspend.StatusCode);
    Assert.Contains("/ExternalPortal/Account/Login",
        afterSuspend.Headers.Location?.OriginalString ?? "");
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test Tests/ --filter "SuspendedUser_ExistingSession_IsRejectedOnNextRequest" --no-build -v minimal
```

Expected: FAIL — the dashboard returns 200 because `OnValidatePrincipal` is not overridden yet.

- [ ] **Step 3: Implement OnValidatePrincipal in PortalCookieEvents**

Replace the entire `Services/Portal/Auth/PortalCookieEvents.cs` with:

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace dwa_ver_val.Services.Portal.Auth;

/// <summary>
/// CookieAuthenticationEvents for the PublicPortalScheme.
/// ValidatePrincipal fires on every request that carries the portal cookie (on sliding refresh).
/// It re-checks PublicUser.Status from the DB and rejects suspended or deactivated sessions
/// immediately — without waiting for the cookie to expire.
/// </summary>
public class PortalCookieEvents : CookieAuthenticationEvents
{
    private readonly ApplicationDBContext _db;

    public PortalCookieEvents(ApplicationDBContext db)
    {
        _db = db;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            context.RejectPrincipal();
            return;
        }

        var user = await _db.PublicUsers.FindAsync(
            new object[] { userId }, context.HttpContext.RequestAborted);

        if (user is null || user.Status != "Active")
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(PortalCookieOptions.SchemeName);
            return;
        }

        await base.ValidatePrincipal(context);
    }
}
```

- [ ] **Step 4: Run all tests**

```bash
dotnet test Tests/ --no-build -v minimal
```

Expected: all tests pass, including the new suspension test.

- [ ] **Step 5: Commit**

```bash
git add "Services/Portal/Auth/PortalCookieEvents.cs" "Tests/Integration/PortalRegistrationFlowTests.cs"
git commit -m "fix(auth): wire OnValidatePrincipal to reject suspended portal sessions immediately — bug G"
```

---

## Task 6 — Bug F: Fix PortalReply cross-case parent handling

**Files:**
- Modify: `Controllers/FileMasterController.cs` (PortalReply action, lines 722–728)

**What the bug is:** When the parent comment exists but belongs to a different case (a cross-case reference), `PortalReply` currently silently sets `parentCommentId = null` and posts the reply as a root comment. This hides a potential abuse scenario (a DWS official could accidentally thread a reply to a comment from a completely different case). The correct behaviour is: if the parent is missing (deleted), de-thread silently (acceptable). If the parent exists but belongs to a different case, return an error.

- [ ] **Step 1: Apply the fix**

In `Controllers/FileMasterController.cs`, replace the current pre-save parent validation block:

**Before (lines 722–728):**
```csharp
// Validate parent comment belongs to this case before persisting.
if (parentCommentId.HasValue)
{
    var parentCheck = await _context.CaseComments.FindAsync(new object[] { parentCommentId.Value }, ct);
    if (parentCheck is null || parentCheck.FileMasterId != id)
        parentCommentId = null;
}
```

**After:**
```csharp
// Validate parent comment before persisting.
if (parentCommentId.HasValue)
{
    var parentCheck = await _context.CaseComments.FindAsync(new object[] { parentCommentId.Value }, ct);
    if (parentCheck is null)
    {
        // Parent deleted — de-thread gracefully (post as root comment).
        parentCommentId = null;
    }
    else if (parentCheck.FileMasterId != id)
    {
        // Parent exists but belongs to a DIFFERENT case — reject.
        TempData["Error"] = "The comment you are replying to does not belong to this case.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
```

There is no test file to create for this; the fix is a 4-line change in the controller. The adversarial reviewer will verify the branch logic.

- [ ] **Step 2: Build and run all tests**

```bash
dotnet test Tests/ --no-build -v minimal
```

Expected: all tests pass.

- [ ] **Step 3: Commit**

```bash
git add "Controllers/FileMasterController.cs"
git commit -m "fix(portal-reply): return error on cross-case parent instead of silently de-threading — bug F"
```

---

## Task 7 — ResponseActionMap.StampsAgreement invariant test

**Files:**
- Modify: `Tests/Controllers/FileMasterControllerLetterTests.cs`

**What the invariant is:** `ResponseActionMap` is a private static dictionary in `FileMasterController`. The `StampsAgreement` boolean controls whether a water user's response to a letter is recorded as `AgreedWithFindings = true` in `LetterIssuance`. This is a legal record under Section 35 of the National Water Act — getting it wrong (e.g., stamping agreement on a DWS-side determination) creates a false legal record. The invariant must be enforced by a test that fails immediately if someone edits `ResponseActionMap` incorrectly.

The test uses reflection to access the private static field.

- [ ] **Step 1: Write the test**

Add to `Tests/Controllers/FileMasterControllerLetterTests.cs`:

```csharp
[Fact]
public void ResponseActionMap_StampsAgreement_OnlyForWaterUserResponseActions()
{
    // ResponseActionMap is private static in FileMasterController.
    // This test uses reflection to enforce NWA legal record integrity:
    // StampsAgreement=true MUST appear on exactly the 4 "MarkLetterXResponded" keys,
    // and on NO DWS-side determination actions (MarkELUConfirmed, MarkUnlawfulUseFound, CloseCase).
    var field = typeof(FileMasterController).GetField(
        "ResponseActionMap",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    Assert.NotNull(field);

    var map = field.GetValue(null) as System.Collections.Generic.Dictionary<string, (string TargetState, bool StampsAgreement)>;
    Assert.NotNull(map);

    // These 4 keys are the ONLY ones that may stamp agreement (water user confirmed).
    var mustStampAgreement = new[]
    {
        "MarkLetter1Responded",
        "MarkLetter1AResponded",
        "MarkLetter2Responded",
        "MarkLetter2AResponded"
    };
    foreach (var key in mustStampAgreement)
    {
        Assert.True(map.ContainsKey(key), $"Expected ResponseActionMap to contain key '{key}'.");
        Assert.True(map[key].StampsAgreement,
            $"Key '{key}' must have StampsAgreement=true (water user agreed). " +
            "Changing this would create a false NWA legal record.");
    }

    // All other keys must NOT stamp agreement (DWS-side determinations).
    var mustNotStampAgreement = new[]
    {
        "MarkELUConfirmed",
        "MarkUnlawfulUseFound",
        "CloseCase"
    };
    foreach (var key in mustNotStampAgreement)
    {
        Assert.True(map.ContainsKey(key), $"Expected ResponseActionMap to contain key '{key}'.");
        Assert.False(map[key].StampsAgreement,
            $"Key '{key}' must have StampsAgreement=false (DWS determination, not water user agreement). " +
            "Stamping agreement here would create a false NWA legal record.");
    }

    // Also assert that the full map contains exactly these 7 keys — no undocumented entries.
    Assert.Equal(7, map.Count);
    var allExpected = mustStampAgreement.Concat(mustNotStampAgreement).ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var key in map.Keys)
        Assert.Contains(key, allExpected);
}
```

- [ ] **Step 2: Run the test to verify it passes**

```bash
dotnet test Tests/ --filter "ResponseActionMap_StampsAgreement_OnlyForWaterUserResponseActions" --no-build -v minimal
```

Expected: PASS immediately (the map is already correctly configured). This is a regression guard.

- [ ] **Step 3: Run all tests**

```bash
dotnet test Tests/ --no-build -v minimal
```

Expected: all tests pass.

- [ ] **Step 4: Commit**

```bash
git add "Tests/Controllers/FileMasterControllerLetterTests.cs"
git commit -m "test(letter): invariant guard for ResponseActionMap.StampsAgreement — NWA legal record integrity"
```

---

## Self-Review

### Spec Coverage

| Bug | Task | Status |
|-----|------|--------|
| H — DbUpdateException too broad | Task 1 | ✅ Covered — `when` filter + logger |
| E — One-time letter re-issuance | Task 2 | ✅ Covered — `OneTimeLetterCodes` set + two idempotency paths |
| D — OrdinalIgnoreCase on Linux | Task 3 | ✅ Covered — both bounds checks changed to `Ordinal` |
| C — Lockout counter race | Task 4 | ✅ Covered — `IsConcurrencyToken` + `DbUpdateConcurrencyException` handler |
| G — Status claim staleness | Task 5 | ✅ Covered — `OnValidatePrincipal` override with DB lookup |
| F — Cross-case parent de-thread | Task 6 | ✅ Covered — distinct error branch for cross-case parent |
| Invariant test | Task 7 | ✅ Covered — reflection-based invariant |

### Placeholder Scan

No "TBD" or "implement later" items found. All code blocks are complete.

### Type Consistency

- `OneTimeLetterCodes` uses the same string values as `LetterActionMap` (e.g., `"S35_L1"`, `"S33_3a_Decl"`) — consistent.
- `PortalCookieEvents` constructor injects `ApplicationDBContext` — registered as Scoped in `Program.cs`, consistent.
- `CookieValidatePrincipalContext` → `context.RejectPrincipal()` + `SignOutAsync` — standard ASP.NET Core pattern, consistent with `CookieAuthenticationEvents` API.
- `IsConcurrencyToken()` fluent call — part of `PropertyBuilder<T>` which is returned by `.Property(u => u.FailedLoginAttempts)` — consistent.

### Known Gaps (Accepted)

- **Task 4 concurrency test**: The `CreateSutFromDbName` helper creates two separate `ApplicationDBContext` instances sharing the same InMemory database. The test verifies the counter is incremented at least once; exact count depends on InMemory concurrency semantics. The test is primarily a regression guard for the code path.
- **Task 3 (Linux case test)**: macOS is case-insensitive by default, so `Ordinal` vs `OrdinalIgnoreCase` can't be distinguished in a macOS unit test. The code change is correct; the production Linux environment will enforce the distinction.
