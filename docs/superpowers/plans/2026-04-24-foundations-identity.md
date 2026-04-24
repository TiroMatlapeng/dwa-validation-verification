# Foundations — Identity + User Admin Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Binding skills (apply to every dispatch):** `agents-in-concert` and `cross-boundary-contracts` — see `docs/superpowers/specs/2026-04-24-mvp-hardening-design.md` §9 for the protocol.
>
> **Shared task journal:** `docs/superpowers/tasks/2026-04-24-mvp-hardening/journal.md`. Every dispatched agent reads it first and appends one entry.

**Goal:** Stand up ASP.NET Identity with six seeded roles, org-unit-scoped claims, authorisation policies, a scope-filtering query service, and a Manage Users admin UI — enough that every subsequent plan can rely on an authenticated, authorised, scope-aware request pipeline.

**Architecture:** Switch `ApplicationUser` to inherit from `IdentityUser<Guid>`; promote `ApplicationDBContext` to `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`; add a single `FoundationsIdentity` migration. Wire cookie authentication with a claims transformation that projects role + org-unit scope claims onto every request. Authorisation policies (`CanAdminister`, `CanCreateCase`, `CanTransitionWorkflow`, `CanIssueLetter`, `CanCapture`) are defined once and referenced by controllers. An `IScopedCaseQuery` service filters `FileMaster` and `Property` queries by scope; `NationalManager` and `SystemAdmin` bypass the filter. Role + demo users are seeded idempotently on startup.

**Tech Stack:** ASP.NET Core 10.0, EF Core 10.0, ASP.NET Identity, SQL Server, xUnit, Moq, EF Core InMemory provider.

**Scope note (splits from spec §6):** The spec originally imagined a single `MvpHardening` migration. Because we've split delivery into four linked plans, each plan generates its own migration so it can be shipped independently. Plan 1 owns `FoundationsIdentity` (Identity tables + `ApplicationUser` restructure + `IEntitlement` interface rename). Plans 3 and 4 add their own migrations (`WorkflowGuardsAndAudit`, `LetterGeneration`).

**Cross-boundary contracts in scope for this plan:**
- `docs/contracts/auth-claims.md` + `contracts/fixtures/auth/claims.json` — the claims contract (Section 9.3 of spec). Created in Phase 0; producer and consumers in Phases 3–4 must assert against the fixture.

---

## Phase 0 — Setup, journal, contracts, packages

### Task 0.1: Create the shared task journal

**Files:**
- Create: `docs/superpowers/tasks/2026-04-24-mvp-hardening/journal.md`

- [ ] **Step 1: Create journal directory and file from template**

Run:
```bash
mkdir -p "docs/superpowers/tasks/2026-04-24-mvp-hardening"
cp ~/.claude/skills/agents-in-concert/journal-template.md \
   "docs/superpowers/tasks/2026-04-24-mvp-hardening/journal.md"
```

- [ ] **Step 2: Fill in the journal header**

Replace the `<REPLACE — …>` tokens in `docs/superpowers/tasks/2026-04-24-mvp-hardening/journal.md` with:

```markdown
# Task: MVP Hardening — Foundations, UI, Workflow & Letters

**Start:** 2026-04-24 (ongoing across four plans)
**Branch:** demo/azure-deploy
**Worktree:** /Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai
**Plan:** docs/superpowers/plans/2026-04-24-foundations-identity.md (Plan 1 of 4)
**Contract docs in scope:** docs/contracts/auth-claims.md, docs/contracts/audit-event.md, docs/contracts/letter-context.md

**Acceptance criteria** (what "done" looks like for the whole MVP-hardening push):
- SystemAdmin can sign in, create other staff users with role + org unit, reset passwords, deactivate.
- Validator in Limpopo WMA sees only Limpopo cases; NationalManager sees all.
- Workflow transitions blocked unless guards satisfied; UI reports the blocking reason.
- S33(2) case skips CP5–CP9 and lands at S33_2_DeclarationIssued.
- RegionalManager can issue, sign, and download a Section 35 Letter 1 PDF.
- Every screen uses the wireframe shell and dws.css tokens; no Bootstrap colour utilities.
- Field labels render as English (SG Code, WARMS Registration Number, etc.).
- All unit + integration tests pass; existing 33 tests still green.

**Out of scope** (must not be touched):
- HDI indicator, property subdivide/consolidate flow, V&V case-number generator.
- Full X.509 signatures, SignaturePad.js UI, email/SMS notifications.
- External public-user portal, MFA.
- Full LawfulnessAssessmentService (GWCA rules + riparian rights + Section 9B).
- SAPWAT / dam / SFRA calculator engines, eWULAAS integration.
- Objection string sweep in views.

---

## Journal

> Each dispatched agent appends one entry. Read ALL prior entries before editing.
```

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/tasks/2026-04-24-mvp-hardening/journal.md
git commit -m "Seed multi-agent task journal for MVP hardening"
```

---

### Task 0.2: Create the claims contract doc and fixture

**Files:**
- Create: `docs/contracts/auth-claims.md`
- Create: `contracts/fixtures/auth/claims.json`
- Create: `docs/contracts/CHANGELOG.md`

- [ ] **Step 1: Create directories**

```bash
mkdir -p docs/contracts contracts/fixtures/auth
```

- [ ] **Step 2: Write the claims contract doc**

Create `docs/contracts/auth-claims.md`:

```markdown
# Auth Claims Contract

**Owners:** security-architect (producer), dotnet-architect (consumers)

## Shape

Every authenticated request carries the following claims:

| Claim type | Value | Source |
|---|---|---|
| `ClaimTypes.NameIdentifier` | ApplicationUser.Id (Guid as string) | Identity default |
| `ClaimTypes.Name` | UserName (= email) | Identity default |
| `ClaimTypes.Email` | Email | Identity default |
| `ClaimTypes.Role` | one of: SystemAdmin, NationalManager, RegionalManager, Validator, Capturer, ReadOnly (multiple allowed; usually one per user) | Identity role store |
| `orgUnitId` | ApplicationUser.OrgUnitId (Guid) or empty string if unscoped | DwsClaimsTransformation |
| `provinceId` | OrganisationalUnit.ProvinceId (Guid) or empty string | DwsClaimsTransformation |
| `wmaId` | OrganisationalUnit.WmaId (Guid) or empty string | DwsClaimsTransformation |
| `catchmentId` | OrganisationalUnit.CatchmentAreaId (Guid) or empty string | DwsClaimsTransformation |
| `displayName` | FirstName + " " + LastName | DwsClaimsTransformation |
| `employeeNumber` | ApplicationUser.EmployeeNumber | DwsClaimsTransformation |

## Fixture

`contracts/fixtures/auth/claims.json` — canonical claims for a Validator user scoped to Limpopo WMA.

## Producer

`Services/Auth/DwsClaimsTransformation.cs` — implements `IClaimsTransformation`, called by ASP.NET Core on every request to augment the Identity-emitted ClaimsPrincipal.

## Consumers

- `Services/Auth/DwsPolicies.cs` — registers authorisation policies; policy handlers read Role + scope claims.
- `Services/Auth/IScopedCaseQuery.cs` — reads scope claims to filter `FileMaster`/`Property` queries.
- Any `[Authorize(Policy = "…")]` controller action in the codebase.

## Invariants

- `orgUnitId` must either be a valid `OrganisationalUnit.OrgUnitId` or the empty string. Never null; never a malformed GUID.
- A user with role `NationalManager` or `SystemAdmin` bypasses scope filtering regardless of `orgUnitId` value.
- A user may hold multiple roles; the highest-privilege role determines scope-bypass eligibility.
- Claims are rebuilt on every request by `DwsClaimsTransformation` — never cached in a long-lived token.

## How to change this contract

1. Update `docs/contracts/auth-claims.md` and `contracts/fixtures/auth/claims.json` in the SAME commit.
2. Run the two-agent review (security-architect + dotnet-architect) on the doc+fixture diff.
3. Update `DwsClaimsTransformation`; verify the producer unit test against the fixture.
4. Update any consumer (policy handler, scope filter); verify their tests against the fixture.
5. Append one-liner to `docs/contracts/CHANGELOG.md`.
```

- [ ] **Step 3: Write the claims fixture**

Create `contracts/fixtures/auth/claims.json`:

```json
{
  "userId": "a1111111-aaaa-1111-aaaa-111111111111",
  "userName": "validator-limpopo@dwa.demo",
  "email": "validator-limpopo@dwa.demo",
  "displayName": "Jane Validator",
  "employeeNumber": "EMP-1001",
  "roles": ["Validator"],
  "orgUnitId": "b2222222-bbbb-2222-bbbb-222222222222",
  "provinceId": "c3333333-cccc-3333-cccc-333333333333",
  "wmaId": "d4444444-dddd-4444-dddd-444444444444",
  "catchmentId": ""
}
```

- [ ] **Step 4: Initialise the contracts CHANGELOG**

Create `docs/contracts/CHANGELOG.md`:

```markdown
# Contracts Changelog

One-line entries per contract change, newest first.

- 2026-04-24: auth-claims/claims — initial contract established (SystemAdmin, NationalManager, RegionalManager, Validator, Capturer, ReadOnly roles + orgUnitId/provinceId/wmaId/catchmentId scope claims). Agent: (plan seed).
```

- [ ] **Step 5: Commit**

```bash
git add docs/contracts/ contracts/
git commit -m "Establish auth-claims cross-boundary contract with fixture"
```

---

### Task 0.3: Install Identity NuGet packages

**Files:**
- Modify: `dwa_ver_val.csproj`

- [ ] **Step 1: Add the Identity package references**

Modify `dwa_ver_val.csproj` — inside the existing `<ItemGroup>` that has `Microsoft.EntityFrameworkCore` references, add:

```xml
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.2" />
    <PackageReference Include="Microsoft.AspNetCore.Identity.UI" Version="10.0.2" />
```

The final file should look like:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <Compile Remove="Tests/**" />
    <Content Remove="Tests/**" />
    <None Remove="Tests/**" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.2" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.2" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.2" />
    <PackageReference Include="Microsoft.AspNetCore.Identity.UI" Version="10.0.2" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Restore**

Run:
```bash
dotnet restore
```

Expected: restore succeeds; both Identity packages appear in `obj/project.assets.json`.

- [ ] **Step 3: Add the test-side package for integration tests**

Modify `Tests/dwa_ver_val.Tests.csproj` — add inside the first `<ItemGroup>`:

```xml
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.2" />
```

- [ ] **Step 4: Restore test project**

```bash
dotnet restore Tests/dwa_ver_val.Tests.csproj
```

Expected: restore succeeds.

- [ ] **Step 5: Verify project still builds**

```bash
dotnet build
```

Expected: build succeeds with zero errors (warnings about `UseAuthorization` without authentication are acceptable at this stage).

- [ ] **Step 6: Commit**

```bash
git add dwa_ver_val.csproj Tests/dwa_ver_val.Tests.csproj
git commit -m "Add ASP.NET Identity and MVC.Testing packages"
```

---

## Phase 1 — Data model restructure

### Task 1.1: Rename `IEntitlement` interface

**Files:**
- Modify: `Interfaces/IEntitlement.cs`
- Verify: no consumers broken

- [ ] **Step 1: Check for any consumers referencing `Entitlement` as an interface**

Run:
```bash
grep -rn "interface Entitlement" --include="*.cs" .
grep -rn ": Entitlement" --include="*.cs" . | grep -v "// " | grep -v "Entitlement entity"
```

Expected: only `Interfaces/IEntitlement.cs` declares the interface; no class implements it. If implementers exist, note them for step 3.

- [ ] **Step 2: Rename the interface declaration**

Edit `Interfaces/IEntitlement.cs`. Replace `public interface Entitlement` with `public interface IEntitlement`. File should read:

```csharp
namespace dwa_ver_val.Interfaces;

public interface IEntitlement
{
    // repository contract — stub, signatures added when EntitlementRepository is written
}
```

- [ ] **Step 3: Update any implementers found in step 1**

For any class matching `: Entitlement` that was actually referring to the interface (not the entity), change to `: IEntitlement`. Rebuild to confirm.

- [ ] **Step 4: Build**

```bash
dotnet build
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add Interfaces/IEntitlement.cs
git commit -m "Rename Entitlement interface to IEntitlement to resolve entity collision"
```

---

### Task 1.2: Refactor `ApplicationUser` to `IdentityUser<Guid>`

**Files:**
- Modify: `Models/ApplicationUser.cs`

- [ ] **Step 1: Rewrite `ApplicationUser`**

Replace the full contents of `Models/ApplicationUser.cs` with:

```csharp
using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string EmployeeNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? OrgUnitId { get; set; }
    public OrganisationalUnit? OrgUnit { get; set; }
}
```

Changes:
- Base class switched from `IdentityUser` (string Id) to `IdentityUser<Guid>` (Guid Id).
- `ApplicationUserId` property removed — `Id` (inherited, Guid) becomes the PK.
- Added `IsActive` boolean for deactivate support without row deletion.

- [ ] **Step 2: Build**

```bash
dotnet build
```

Expected: build **FAILS** on `ApplicationDBContext.cs` line 128 because `HasKey(e => e.ApplicationUserId)` references a now-removed property. This is intended; next task fixes it.

---

### Task 1.3: Promote `ApplicationDBContext` to `IdentityDbContext`

**Files:**
- Modify: `DatabaseContexts/ApplicationDBContext.cs`

- [ ] **Step 1: Update the using directives and base class**

Replace the top of `DatabaseContexts/ApplicationDBContext.cs` (line 1 through the class declaration) with:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class ApplicationDBContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDBContext(DbContextOptions<ApplicationDBContext> dbContextOption) : base(dbContextOption)
    { }
```

- [ ] **Step 2: Remove the explicit `DbSet<ApplicationUser>`**

`IdentityDbContext<ApplicationUser, ...>` already exposes `Users`. Remove line 58:

```csharp
    public DbSet<ApplicationUser> ApplicationUsers { get; set; }
```

- [ ] **Step 3: Remove the custom `HasKey` for `ApplicationUser`**

Remove line 128 in `OnModelCreating`:

```csharp
        modelBuilder.Entity<ApplicationUser>().HasKey(e => e.ApplicationUserId);
```

- [ ] **Step 4: Call `base.OnModelCreating` at the top of the override**

In `OnModelCreating`, insert this as the very first statement (before the `// ── Primary keys ──` comment):

```csharp
        base.OnModelCreating(modelBuilder);
```

This wires up the Identity schema (AspNetRoles, AspNetUserRoles, AspNetUserClaims, AspNetUserLogins, AspNetUserTokens, AspNetRoleClaims).

- [ ] **Step 5: Verify FK targets still resolve**

`FileMaster.ValidatorId`, `FileMaster.CapturePersonId`, `LetterIssuance.SignedById`, `Document.UploadedByUserId`, `WorkflowStepRecord.CompletedById`, `Validation.AssignedToId`, `DigitalSignature.ApplicationUserId`, `SignatureRequest.ApplicationUserId`, `CaseComment.ApplicationUserId`, `Notification.ApplicationUserId`, `PublicUserProperty.ApprovedByUserId` are all `Guid?`. They previously pointed at `ApplicationUser.ApplicationUserId`; they now point at `ApplicationUser.Id` (same type). No FK column changes needed at runtime — EF will bind on the principal PK by convention.

No code change in this step; this is verification only. Open each of those model files and confirm the FK property is `Guid?` not `string?`.

Run:
```bash
grep -rn "public Guid? ValidatorId\|public Guid? CapturePersonId\|public Guid? SignedById\|public Guid? UploadedByUserId\|public Guid? CompletedById\|public Guid? AssignedToId\|public Guid? ApplicationUserId\|public Guid? ApprovedByUserId" Models/
```

Expected: each listed FK appears as `Guid?`.

- [ ] **Step 6: Build**

```bash
dotnet build
```

Expected: build succeeds.

- [ ] **Step 7: Commit**

```bash
git add Models/ApplicationUser.cs DatabaseContexts/ApplicationDBContext.cs
git commit -m "Switch ApplicationUser to IdentityUser<Guid> and promote DbContext to IdentityDbContext"
```

---

### Task 1.4: Generate and apply the `FoundationsIdentity` migration

**Files:**
- Create: `Migrations/<timestamp>_FoundationsIdentity.cs`
- Create: `Migrations/<timestamp>_FoundationsIdentity.Designer.cs`
- Modify: `Migrations/ApplicationDBContextModelSnapshot.cs`

- [ ] **Step 1: Generate the migration**

Run:
```bash
dotnet ef migrations add FoundationsIdentity
```

Expected: command succeeds; two new files in `Migrations/` with names like `20260424XXXXXX_FoundationsIdentity.cs` and `.Designer.cs`; `ApplicationDBContextModelSnapshot.cs` updated.

- [ ] **Step 2: Review the generated migration**

Read `Migrations/*_FoundationsIdentity.cs`. Confirm the `Up` method:

1. Drops the `ApplicationUserId` column from the existing `ApplicationUsers` table (or equivalent rename to `Id`).
2. Creates `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims` tables.
3. Adds an `IsActive` column (bool, default true) to the user table.

If the generated migration attempts to drop the user table or recreate all FKs, STOP and read CLAUDE.md's "Known issue" section — the `ApplicationUserId` → `Id` change may need a manual `RenameColumn` inserted. Edit the migration to:

- Rename the `ApplicationUserId` column in `AspNetUsers` (the new Identity table name) to remove it, keeping EF's auto-generated `Id` column — EF may have already done this in the generated migration. Verify.
- If EF drops the existing user table and recreates it, preserve by replacing the drop+recreate with a `RenameTable` + `RenameColumn` sequence. Likely content:

```csharp
// If EF generated a "drop ApplicationUsers then create AspNetUsers" pattern, replace with:
migrationBuilder.RenameTable(
    name: "ApplicationUsers",
    newName: "AspNetUsers");
migrationBuilder.DropColumn(
    name: "ApplicationUserId",
    table: "AspNetUsers");
```

- [ ] **Step 3: Ensure SQL Server is running**

```bash
docker ps | grep mssql || docker-compose up -d
```

Expected: the `mssql` container is up on port 1433.

- [ ] **Step 4: Apply the migration**

```bash
dotnet ef database update
```

Expected: migration applies cleanly; no data loss beyond the dropped `ApplicationUserId` column (which was never referenced by any FK).

- [ ] **Step 5: Verify the schema**

Run:
```bash
docker exec -it $(docker ps -q -f name=mssql) /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P 'YourStrong@Passw0rd' -d dwa_val_ver \
  -Q "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE 'AspNet%' ORDER BY TABLE_NAME"
```

Expected output includes:
```
AspNetRoleClaims
AspNetRoles
AspNetUserClaims
AspNetUserLogins
AspNetUserRoles
AspNetUsers
AspNetUserTokens
```

(Password value depends on `docker-compose.yml` — use the one configured there.)

- [ ] **Step 6: Commit**

```bash
git add Migrations/ DatabaseContexts/
git commit -m "Add FoundationsIdentity migration with Identity schema"
```

---

## Phase 2 — Identity wiring and account views

### Task 2.1: Wire `AddIdentity` and cookie authentication in `Program.cs`

**Files:**
- Modify: `Program.cs`

- [ ] **Step 1: Update `Program.cs` with the authentication/authorisation pipeline**

Replace the full contents of `Program.cs` with:

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// DbContext
builder.Services.AddDbContext<ApplicationDBContext>(
    options => options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Identity (cookie auth; no Identity UI scaffolding)
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddEntityFrameworkStores<ApplicationDBContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Claims transformation (populates role + scope claims on every request)
builder.Services.AddScoped<IClaimsTransformation, DwsClaimsTransformation>();

// Authorisation policies
builder.Services.AddAuthorization(DwsPolicies.Configure);

// Repository DI
builder.Services.AddScoped<IPropertyInterface, PropertyRepository>();
builder.Services.AddScoped<IAddress, AddressRepository>();
builder.Services.AddScoped<IFileMaster, FileMasterRepository>();
builder.Services.AddScoped<IForestation, ForestationRepository>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IScopedCaseQuery, ScopedCaseQuery>();

// Seeders
builder.Services.AddScoped<SeedDataService>();
builder.Services.AddScoped<IdentitySeeder>();

var app = builder.Build();

// Apply migrations and seed on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
    await db.Database.MigrateAsync();

    var refSeeder = scope.ServiceProvider.GetRequiredService<SeedDataService>();
    await refSeeder.SeedAsync();

    var identitySeeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
    await identitySeeder.SeedAsync();
}

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Expose the Program class for Microsoft.AspNetCore.Mvc.Testing
public partial class Program { }
```

- [ ] **Step 2: Build**

```bash
dotnet build
```

Expected: build **FAILS** with errors about missing types `DwsClaimsTransformation`, `DwsPolicies`, `IScopedCaseQuery`, `ScopedCaseQuery`, `IdentitySeeder`. These are added in later tasks. Leave the build red for now and proceed — Phase 3 and Phase 5 fix it.

---

### Task 2.2: Scaffold the `AccountController`

**Files:**
- Create: `Controllers/AccountController.cs`
- Create: `ViewModels/LoginViewModel.cs`

- [ ] **Step 1: Create the login view model**

Create `ViewModels/LoginViewModel.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.ViewModels;

public class LoginViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
```

- [ ] **Step 2: Create the controller**

Create `Controllers/AccountController.cs`:

```csharp
using dwa_ver_val.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace dwa_ver_val.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} signed in.", model.Email);
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);
            return RedirectToAction("Index", "Home");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Account locked. Try again later.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build
```

Expected: still fails on `DwsClaimsTransformation`, `DwsPolicies`, `IScopedCaseQuery`, `IdentitySeeder`. Controller itself compiles.

---

### Task 2.3: Create the Account views (basic — re-skin is Plan 2)

**Files:**
- Create: `Views/Account/Login.cshtml`
- Create: `Views/Account/AccessDenied.cshtml`

- [ ] **Step 1: Login view**

Create `Views/Account/Login.cshtml`:

```cshtml
@model dwa_ver_val.ViewModels.LoginViewModel
@{
    ViewData["Title"] = "Sign in";
    Layout = "_Layout";
}

<div class="dws-card dws-card--login">
    <h1>Sign in to DWA V&V</h1>
    <form asp-action="Login" method="post">
        <input type="hidden" asp-for="ReturnUrl" />
        <div asp-validation-summary="All" class="dws-validation-summary"></div>

        <div class="dws-form-row">
            <label asp-for="Email" class="dws-label"></label>
            <input asp-for="Email" class="dws-input" autocomplete="username" />
            <span asp-validation-for="Email" class="dws-field-error"></span>
        </div>

        <div class="dws-form-row">
            <label asp-for="Password" class="dws-label"></label>
            <input asp-for="Password" class="dws-input" autocomplete="current-password" />
            <span asp-validation-for="Password" class="dws-field-error"></span>
        </div>

        <div class="dws-form-row">
            <label class="dws-checkbox">
                <input asp-for="RememberMe" type="checkbox" />
                @Html.DisplayNameFor(m => m.RememberMe)
            </label>
        </div>

        <button type="submit" class="dws-btn dws-btn-primary">Sign in</button>
    </form>
</div>
```

- [ ] **Step 2: Access-denied view**

Create `Views/Account/AccessDenied.cshtml`:

```cshtml
@{
    ViewData["Title"] = "Access denied";
    Layout = "_Layout";
}

<div class="dws-card">
    <h1>Access denied</h1>
    <p>You do not have permission to access this resource. If you believe this is an error, contact your system administrator.</p>
    <a asp-controller="Home" asp-action="Index" class="dws-btn dws-btn-ghost">Back to dashboard</a>
</div>
```

- [ ] **Step 3: Commit the controller + views**

```bash
git add Controllers/AccountController.cs ViewModels/LoginViewModel.cs Views/Account/
git commit -m "Add AccountController with Login, Logout, AccessDenied"
```

---

## Phase 3 — Claims transformation and authorisation policies

### Task 3.1: Write the claims-transformation test first (TDD)

**Files:**
- Create: `Tests/Services/Auth/DwsClaimsTransformationTests.cs`

- [ ] **Step 1: Create the test file**

Create `Tests/Services/Auth/DwsClaimsTransformationTests.cs`:

```csharp
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace dwa_ver_val.Tests.Services.Auth;

public class DwsClaimsTransformationTests
{
    private static ApplicationDBContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDBContext(options);
    }

    [Fact]
    public async Task TransformAsync_ValidatorScopedToLimpopo_MatchesClaimsFixture()
    {
        // Fixture: contracts/fixtures/auth/claims.json
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "contracts", "fixtures", "auth", "claims.json");
        var fixtureJson = File.ReadAllText(Path.GetFullPath(fixturePath));
        var expected = JsonSerializer.Deserialize<ExpectedClaims>(fixtureJson,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;

        using var db = CreateDb();
        var provinceId = Guid.Parse(expected.ProvinceId);
        var wmaId = Guid.Parse(expected.WmaId);
        var orgUnitId = Guid.Parse(expected.OrgUnitId);
        var userId = Guid.Parse(expected.UserId);

        db.Provinces.Add(new Province { ProvinceId = provinceId, ProvinceName = "Limpopo" });
        db.WaterManagementAreas.Add(new WaterManagementArea { WmaId = wmaId, WmaName = "Limpopo WMA", ProvinceId = provinceId });
        db.OrganisationalUnits.Add(new OrganisationalUnit
        {
            OrgUnitId = orgUnitId,
            Name = "Limpopo Regional Office",
            Type = "Regional",
            ProvinceId = provinceId,
            WmaId = wmaId,
            CatchmentAreaId = null
        });
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = expected.UserName,
            NormalizedUserName = expected.UserName.ToUpperInvariant(),
            Email = expected.Email,
            NormalizedEmail = expected.Email.ToUpperInvariant(),
            FirstName = "Jane",
            LastName = "Validator",
            EmployeeNumber = expected.EmployeeNumber,
            IsActive = true,
            OrgUnitId = orgUnitId
        });
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, expected.UserName));
        identity.AddClaim(new Claim(ClaimTypes.Email, expected.Email));
        identity.AddClaim(new Claim(ClaimTypes.Role, "Validator"));
        var principal = new ClaimsPrincipal(identity);

        var sut = new DwsClaimsTransformation(db);
        var transformed = await sut.TransformAsync(principal);

        Assert.Equal(expected.DisplayName, transformed.FindFirst("displayName")?.Value);
        Assert.Equal(expected.EmployeeNumber, transformed.FindFirst("employeeNumber")?.Value);
        Assert.Equal(expected.OrgUnitId, transformed.FindFirst("orgUnitId")?.Value);
        Assert.Equal(expected.ProvinceId, transformed.FindFirst("provinceId")?.Value);
        Assert.Equal(expected.WmaId, transformed.FindFirst("wmaId")?.Value);
        Assert.Equal(expected.CatchmentId, transformed.FindFirst("catchmentId")?.Value);
        Assert.Contains(transformed.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Validator");
    }

    [Fact]
    public async Task TransformAsync_IsIdempotent_DoesNotDuplicateClaimsOnReCall()
    {
        using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "a@b.c",
            NormalizedUserName = "A@B.C",
            Email = "a@b.c",
            NormalizedEmail = "A@B.C",
            FirstName = "A",
            LastName = "B",
            EmployeeNumber = "X",
            IsActive = true,
            OrgUnitId = null
        });
        await db.SaveChangesAsync();

        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        var principal = new ClaimsPrincipal(identity);

        var sut = new DwsClaimsTransformation(db);
        var once = await sut.TransformAsync(principal);
        var twice = await sut.TransformAsync(once);

        Assert.Single(twice.Claims.Where(c => c.Type == "displayName"));
    }

    private record ExpectedClaims(
        string UserId,
        string UserName,
        string Email,
        string DisplayName,
        string EmployeeNumber,
        string[] Roles,
        string OrgUnitId,
        string ProvinceId,
        string WmaId,
        string CatchmentId);
}
```

- [ ] **Step 2: Run the test, confirm it fails to compile**

```bash
dotnet test Tests/dwa_ver_val.Tests.csproj --filter "FullyQualifiedName~DwsClaimsTransformationTests"
```

Expected: build error — `DwsClaimsTransformation` does not exist. That's the RED test.

---

### Task 3.2: Implement `DwsClaimsTransformation`

**Files:**
- Create: `Services/Auth/DwsClaimsTransformation.cs`

- [ ] **Step 1: Create the implementation**

Create `Services/Auth/DwsClaimsTransformation.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

public class DwsClaimsTransformation : IClaimsTransformation
{
    private const string Marker = "dws:augmented";

    private readonly ApplicationDBContext _db;

    public DwsClaimsTransformation(ApplicationDBContext db)
    {
        _db = db;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return principal;

        if (identity.HasClaim(c => c.Type == Marker)) return principal;

        var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return principal;

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.FirstName,
                u.LastName,
                u.EmployeeNumber,
                u.OrgUnitId,
                OrgUnit = u.OrgUnit == null ? null : new
                {
                    u.OrgUnit.ProvinceId,
                    u.OrgUnit.WmaId,
                    u.OrgUnit.CatchmentAreaId
                }
            })
            .FirstOrDefaultAsync();

        if (user is null) return principal;

        identity.AddClaim(new Claim("displayName", $"{user.FirstName} {user.LastName}"));
        identity.AddClaim(new Claim("employeeNumber", user.EmployeeNumber));
        identity.AddClaim(new Claim("orgUnitId", user.OrgUnitId?.ToString() ?? string.Empty));
        identity.AddClaim(new Claim("provinceId", user.OrgUnit?.ProvinceId?.ToString() ?? string.Empty));
        identity.AddClaim(new Claim("wmaId", user.OrgUnit?.WmaId?.ToString() ?? string.Empty));
        identity.AddClaim(new Claim("catchmentId", user.OrgUnit?.CatchmentAreaId?.ToString() ?? string.Empty));
        identity.AddClaim(new Claim(Marker, "1"));

        return principal;
    }
}
```

- [ ] **Step 2: Run the test**

```bash
dotnet test Tests/dwa_ver_val.Tests.csproj --filter "FullyQualifiedName~DwsClaimsTransformationTests"
```

Expected: both tests PASS.

- [ ] **Step 3: Commit**

```bash
git add Services/Auth/DwsClaimsTransformation.cs Tests/Services/Auth/DwsClaimsTransformationTests.cs
git commit -m "Add DwsClaimsTransformation with fixture-driven test"
```

---

### Task 3.3: Define authorisation policies

**Files:**
- Create: `Services/Auth/DwsPolicies.cs`
- Create: `Services/Auth/DwsRoles.cs`

- [ ] **Step 1: Create the role name constants**

Create `Services/Auth/DwsRoles.cs`:

```csharp
public static class DwsRoles
{
    public const string SystemAdmin = nameof(SystemAdmin);
    public const string NationalManager = nameof(NationalManager);
    public const string RegionalManager = nameof(RegionalManager);
    public const string Validator = nameof(Validator);
    public const string Capturer = nameof(Capturer);
    public const string ReadOnly = nameof(ReadOnly);

    public static readonly string[] All =
    {
        SystemAdmin, NationalManager, RegionalManager, Validator, Capturer, ReadOnly
    };

    // Hierarchies (used by policies — higher-privilege roles satisfy lower-privilege policies)
    public static readonly string[] AtLeastReadOnly = All;
    public static readonly string[] AtLeastCapturer = { SystemAdmin, NationalManager, RegionalManager, Validator, Capturer };
    public static readonly string[] AtLeastValidator = { SystemAdmin, NationalManager, RegionalManager, Validator };
    public static readonly string[] AtLeastRegionalManager = { SystemAdmin, NationalManager, RegionalManager };
    public static readonly string[] AtLeastNationalManager = { SystemAdmin, NationalManager };
    public static readonly string[] AdminOnly = { SystemAdmin };
}
```

- [ ] **Step 2: Create the policy registration**

Create `Services/Auth/DwsPolicies.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;

public static class DwsPolicies
{
    public const string CanAdminister = "CanAdminister";
    public const string CanCreateCase = "CanCreateCase";
    public const string CanTransitionWorkflow = "CanTransitionWorkflow";
    public const string CanIssueLetter = "CanIssueLetter";
    public const string CanCapture = "CanCapture";
    public const string CanRead = "CanRead";

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(CanAdminister,
            p => p.RequireAuthenticatedUser().RequireRole(DwsRoles.AdminOnly));

        options.AddPolicy(CanCreateCase,
            p => p.RequireAuthenticatedUser().RequireRole(DwsRoles.AtLeastValidator));

        options.AddPolicy(CanTransitionWorkflow,
            p => p.RequireAuthenticatedUser().RequireRole(DwsRoles.AtLeastValidator));

        options.AddPolicy(CanIssueLetter,
            p => p.RequireAuthenticatedUser().RequireRole(DwsRoles.AtLeastRegionalManager));

        options.AddPolicy(CanCapture,
            p => p.RequireAuthenticatedUser().RequireRole(DwsRoles.AtLeastCapturer));

        options.AddPolicy(CanRead,
            p => p.RequireAuthenticatedUser().RequireRole(DwsRoles.AtLeastReadOnly));
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build
```

Expected: still fails on `IScopedCaseQuery`, `ScopedCaseQuery`, `IdentitySeeder`. Policies themselves compile.

- [ ] **Step 4: Commit**

```bash
git add Services/Auth/DwsRoles.cs Services/Auth/DwsPolicies.cs
git commit -m "Add DwsRoles constants and DwsPolicies registration"
```

---

## Phase 4 — Scope-filtering service

### Task 4.1: Write the scope-query test first (TDD)

**Files:**
- Create: `Tests/Services/Auth/ScopedCaseQueryTests.cs`

- [ ] **Step 1: Create the test**

Create `Tests/Services/Auth/ScopedCaseQueryTests.cs`:

```csharp
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace dwa_ver_val.Tests.Services.Auth;

public class ScopedCaseQueryTests
{
    private static ApplicationDBContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDBContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDBContext(options);
    }

    private static ClaimsPrincipal UserWithRoleAndOrgUnit(string role, Guid? orgUnitId, Guid? wmaId = null)
    {
        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Role, role));
        identity.AddClaim(new Claim("orgUnitId", orgUnitId?.ToString() ?? string.Empty));
        identity.AddClaim(new Claim("wmaId", wmaId?.ToString() ?? string.Empty));
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task FilterFileMasters_Validator_SeesOnlyOwnWmaCases()
    {
        using var db = CreateDb();
        var limpopoWma = Guid.NewGuid();
        var mpumalangaWma = Guid.NewGuid();

        db.Properties.AddRange(
            new Property { PropertyId = Guid.NewGuid(), SGCode = "LIM-01", WmaId = limpopoWma },
            new Property { PropertyId = Guid.NewGuid(), SGCode = "MP-01", WmaId = mpumalangaWma });
        await db.SaveChangesAsync();

        var limpopoProperty = db.Properties.First(p => p.WmaId == limpopoWma);
        var mpumalangaProperty = db.Properties.First(p => p.WmaId == mpumalangaWma);

        db.FileMasters.AddRange(
            new FileMaster { FileMasterId = Guid.NewGuid(), PropertyId = limpopoProperty.PropertyId, FileNumber = "LIM-0001" },
            new FileMaster { FileMasterId = Guid.NewGuid(), PropertyId = mpumalangaProperty.PropertyId, FileNumber = "MP-0001" });
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var user = UserWithRoleAndOrgUnit(DwsRoles.Validator, orgUnitId: Guid.NewGuid(), wmaId: limpopoWma);

        var result = await sut.FilterFileMasters(db.FileMasters, user).ToListAsync();

        Assert.Single(result);
        Assert.Equal("LIM-0001", result[0].FileNumber);
    }

    [Fact]
    public async Task FilterFileMasters_NationalManager_SeesAllCases()
    {
        using var db = CreateDb();
        var wmaA = Guid.NewGuid();
        var wmaB = Guid.NewGuid();
        var pA = new Property { PropertyId = Guid.NewGuid(), SGCode = "A", WmaId = wmaA };
        var pB = new Property { PropertyId = Guid.NewGuid(), SGCode = "B", WmaId = wmaB };
        db.Properties.AddRange(pA, pB);
        db.FileMasters.AddRange(
            new FileMaster { FileMasterId = Guid.NewGuid(), PropertyId = pA.PropertyId, FileNumber = "A" },
            new FileMaster { FileMasterId = Guid.NewGuid(), PropertyId = pB.PropertyId, FileNumber = "B" });
        await db.SaveChangesAsync();

        var sut = new ScopedCaseQuery(db);
        var user = UserWithRoleAndOrgUnit(DwsRoles.NationalManager, orgUnitId: null, wmaId: null);

        var result = await sut.FilterFileMasters(db.FileMasters, user).ToListAsync();

        Assert.Equal(2, result.Count);
    }
}
```

- [ ] **Step 2: Run; confirm compile error**

```bash
dotnet test Tests/dwa_ver_val.Tests.csproj --filter "FullyQualifiedName~ScopedCaseQueryTests"
```

Expected: build error — `IScopedCaseQuery`, `ScopedCaseQuery` do not exist.

---

### Task 4.2: Implement `IScopedCaseQuery` and `ScopedCaseQuery`

**Files:**
- Create: `Services/Auth/IScopedCaseQuery.cs`
- Create: `Services/Auth/ScopedCaseQuery.cs`

- [ ] **Step 1: Interface**

Create `Services/Auth/IScopedCaseQuery.cs`:

```csharp
using System.Security.Claims;

public interface IScopedCaseQuery
{
    IQueryable<FileMaster> FilterFileMasters(IQueryable<FileMaster> source, ClaimsPrincipal user);
    IQueryable<Property> FilterProperties(IQueryable<Property> source, ClaimsPrincipal user);
    bool IsInScope(FileMaster fileMaster, ClaimsPrincipal user);
}
```

- [ ] **Step 2: Implementation**

Create `Services/Auth/ScopedCaseQuery.cs`:

```csharp
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

public class ScopedCaseQuery : IScopedCaseQuery
{
    private readonly ApplicationDBContext _db;

    public ScopedCaseQuery(ApplicationDBContext db)
    {
        _db = db;
    }

    private static bool BypassesScope(ClaimsPrincipal user) =>
        user.IsInRole(DwsRoles.SystemAdmin) || user.IsInRole(DwsRoles.NationalManager);

    private static Guid? GetScopeWma(ClaimsPrincipal user)
    {
        var wmaClaim = user.FindFirst("wmaId")?.Value;
        return Guid.TryParse(wmaClaim, out var wma) ? wma : null;
    }

    public IQueryable<FileMaster> FilterFileMasters(IQueryable<FileMaster> source, ClaimsPrincipal user)
    {
        if (BypassesScope(user)) return source;
        var wmaId = GetScopeWma(user);
        if (wmaId is null) return source.Where(_ => false);
        return source.Where(fm => fm.Property!.WmaId == wmaId);
    }

    public IQueryable<Property> FilterProperties(IQueryable<Property> source, ClaimsPrincipal user)
    {
        if (BypassesScope(user)) return source;
        var wmaId = GetScopeWma(user);
        if (wmaId is null) return source.Where(_ => false);
        return source.Where(p => p.WmaId == wmaId);
    }

    public bool IsInScope(FileMaster fileMaster, ClaimsPrincipal user)
    {
        if (BypassesScope(user)) return true;
        var wmaId = GetScopeWma(user);
        if (wmaId is null) return false;
        return _db.Entry(fileMaster).Reference(fm => fm.Property).CurrentValue?.WmaId == wmaId;
    }
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet test Tests/dwa_ver_val.Tests.csproj --filter "FullyQualifiedName~ScopedCaseQueryTests"
```

Expected: both tests PASS.

- [ ] **Step 4: Commit**

```bash
git add Services/Auth/IScopedCaseQuery.cs Services/Auth/ScopedCaseQuery.cs Tests/Services/Auth/ScopedCaseQueryTests.cs
git commit -m "Add IScopedCaseQuery and ScopedCaseQuery with role-aware WMA filtering"
```

---

### Task 4.3: Apply scope filtering to `FileMasterController.Index` and authorize other actions

**Files:**
- Modify: `Controllers/FileMasterController.cs`

The controller already injects `ApplicationDBContext` as `_context`, so we use `_context.FileMasters` as the `IQueryable` source.

- [ ] **Step 1: Add `IScopedCaseQuery` to constructor**

At the top of `Controllers/FileMasterController.cs`, add the field and constructor parameter. Replace the existing fields + constructor with:

```csharp
using dwa_ver_val; // for DwsPolicies, IScopedCaseQuery
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

[Authorize(Policy = DwsPolicies.CanRead)]
public class FileMasterController : Controller
{
    private readonly IFileMaster _fileMasterRepository;
    private readonly ApplicationDBContext _context;
    private readonly IWorkflowService _workflow;
    private readonly IScopedCaseQuery _scope;

    public FileMasterController(
        IFileMaster fileMasterRepository,
        ApplicationDBContext context,
        IWorkflowService workflow,
        IScopedCaseQuery scope)
    {
        _fileMasterRepository = fileMasterRepository;
        _context = context;
        _workflow = workflow;
        _scope = scope;
    }
```

- [ ] **Step 2: Replace the `Index` action with a scoped query**

Replace:

```csharp
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var fileMasters = await _fileMasterRepository.ListAllAsync();
        return View(fileMasters);
    }
```

With:

```csharp
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var query = _scope.FilterFileMasters(_context.FileMasters.AsQueryable(), User);
        var fileMasters = await query
            .Include(fm => fm.Property)
            .OrderBy(fm => fm.FileNumber)
            .ToListAsync();
        return View(fileMasters);
    }
```

- [ ] **Step 3: Authorise mutating actions**

Above the existing `Create`, `Edit`, `Delete` actions (both GET and POST variants), add:

```csharp
    [Authorize(Policy = DwsPolicies.CanCreateCase)]
```

Above the POST action that advances workflow (e.g. `AdvanceWorkflow` or similar), add:

```csharp
    [Authorize(Policy = DwsPolicies.CanTransitionWorkflow)]
```

- [ ] **Step 4: Build**

```bash
dotnet build
```

Expected: build succeeds (all Auth types now exist after Tasks 3.2–4.2 and 5.1).

- [ ] **Step 5: Run full test suite**

```bash
dotnet test
```

Expected: all tests pass (33 existing + new Auth unit tests).

- [ ] **Step 6: Commit**

```bash
git add Controllers/FileMasterController.cs
git commit -m "Authorise FileMasterController and apply scope filter to list query"
```

---

## Phase 5 — Role and demo-user seeding

### Task 5.1: Create `IdentitySeeder`

**Files:**
- Create: `Services/IdentitySeeder.cs`

- [ ] **Step 1: Implementation**

Create `Services/IdentitySeeder.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public class IdentitySeeder
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole<Guid>> _roles;
    private readonly ApplicationDBContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles,
        ApplicationDBContext db,
        IConfiguration config,
        ILogger<IdentitySeeder> logger)
    {
        _users = users;
        _roles = roles;
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await SeedRolesAsync();
        await SeedDemoUsersAsync();
    }

    private async Task SeedRolesAsync()
    {
        foreach (var roleName in DwsRoles.All)
        {
            if (!await _roles.RoleExistsAsync(roleName))
            {
                await _roles.CreateAsync(new IdentityRole<Guid>(roleName) { Id = Guid.NewGuid() });
                _logger.LogInformation("Seeded role {Role}", roleName);
            }
        }
    }

    private async Task SeedDemoUsersAsync()
    {
        // Only seed demo users when the initial password is configured — production must not fall through.
        var initialPassword = _config["Identity:InitialDemoPassword"];
        if (string.IsNullOrWhiteSpace(initialPassword))
        {
            _logger.LogInformation("Identity:InitialDemoPassword not set; skipping demo-user seed.");
            return;
        }

        // Pick the first seeded Regional OrgUnit + its WMA (fallback for dev — if none, log and bail).
        // OrganisationalUnit.Type is a string column with values "National", "Provincial", "Regional", "CMA", "Catchment".
        var orgUnit = await _db.OrganisationalUnits
            .Include(ou => ou.WaterManagementArea)
            .FirstOrDefaultAsync(ou => ou.Type == "Regional" && ou.WaterManagementArea != null);
        if (orgUnit is null)
        {
            _logger.LogWarning("No Regional OrganisationalUnit with a WMA found; skipping demo-user seed.");
            return;
        }

        var demos = new (string Email, string Role, string First, string Last, string EmpNo, Guid? OrgUnitId)[]
        {
            ("admin@dwa.demo",                DwsRoles.SystemAdmin,      "System",   "Admin",     "EMP-0001", null),
            ("national@dwa.demo",             DwsRoles.NationalManager,  "Nate",     "National",  "EMP-0002", null),
            ($"regional-{orgUnit.WaterManagementArea!.WmaCode.ToLowerInvariant()}@dwa.demo",
                                              DwsRoles.RegionalManager,  "Rita",     "Regional",  "EMP-0003", orgUnit.OrgUnitId),
            ($"validator-{orgUnit.WaterManagementArea!.WmaCode.ToLowerInvariant()}@dwa.demo",
                                              DwsRoles.Validator,        "Jane",     "Validator", "EMP-1001", orgUnit.OrgUnitId),
            ($"capturer-{orgUnit.WaterManagementArea!.WmaCode.ToLowerInvariant()}@dwa.demo",
                                              DwsRoles.Capturer,         "Cody",     "Capturer",  "EMP-2001", orgUnit.OrgUnitId),
            ("readonly@dwa.demo",             DwsRoles.ReadOnly,         "Rosa",     "Reader",    "EMP-3001", orgUnit.OrgUnitId),
        };

        foreach (var demo in demos)
        {
            var existing = await _users.FindByEmailAsync(demo.Email);
            if (existing is not null) continue;

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = demo.Email,
                Email = demo.Email,
                EmailConfirmed = true,
                FirstName = demo.First,
                LastName = demo.Last,
                EmployeeNumber = demo.EmpNo,
                IsActive = true,
                OrgUnitId = demo.OrgUnitId
            };
            var create = await _users.CreateAsync(user, initialPassword);
            if (!create.Succeeded)
            {
                _logger.LogError("Failed to create demo user {Email}: {Errors}",
                    demo.Email, string.Join(", ", create.Errors.Select(e => e.Description)));
                continue;
            }
            await _users.AddToRoleAsync(user, demo.Role);
            _logger.LogInformation("Seeded demo user {Email} with role {Role}", demo.Email, demo.Role);
        }
    }
}
```

- [ ] **Step 2: Add the dev-only initial password**

Modify `appsettings.Development.json` to include:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Identity": {
    "InitialDemoPassword": "Demo@Pass2026"
  }
}
```

Do NOT add this to `appsettings.json` or `appsettings.Production.json`. Production passwords are set via App Service configuration only.

- [ ] **Step 3: Build**

```bash
dotnet build
```

Expected: build succeeds (Phase 2's `Program.cs` now compiles because `IdentitySeeder` exists).

- [ ] **Step 4: Run tests**

```bash
dotnet test
```

Expected: all tests pass (existing 33 + the Auth tests from Phase 3–4).

- [ ] **Step 5: Commit**

```bash
git add Services/IdentitySeeder.cs appsettings.Development.json
git commit -m "Add IdentitySeeder for roles and demo users"
```

---

### Task 5.2: Smoke-test the login flow end-to-end

**Files:**
- No new files.

- [ ] **Step 1: Clean and migrate**

If the dev DB has stale data, wipe it:

```bash
dotnet ef database drop --force
dotnet ef database update
```

- [ ] **Step 2: Start the app**

```bash
dotnet run
```

Expected log output includes:
- "Seeded role SystemAdmin" … through ReadOnly.
- "Seeded demo user admin@dwa.demo with role SystemAdmin" and similar for the five others.
- No exceptions.

- [ ] **Step 3: Sign in manually**

Open the app URL printed by Kestrel (e.g. `https://localhost:7xxx/`). Expect to be redirected to `/Account/Login`. Log in as `validator-{wma}@dwa.demo` with password `Demo@Pass2026`. Confirm redirect to `/`.

- [ ] **Step 4: Verify scope filter in the browser**

Navigate to `/FileMaster`. Confirm only cases with properties in the seeded Regional OrgUnit's WMA appear. Sign out, sign in as `national@dwa.demo`, return to `/FileMaster`, confirm all cases appear.

- [ ] **Step 5: Commit any minor fixups required**

If smoke testing revealed nits (e.g. unhandled null reference in a view that now receives an authenticated user's ClaimsPrincipal), fix them and commit:

```bash
git add <affected files>
git commit -m "Fix <specific issue> discovered during smoke test"
```

---

## Phase 6 — Manage Users admin UI

### Task 6.1: `UsersController` scaffolding

**Files:**
- Create: `Controllers/Admin/UsersController.cs`
- Create: `ViewModels/Admin/CreateUserViewModel.cs`
- Create: `ViewModels/Admin/EditUserViewModel.cs`
- Create: `ViewModels/Admin/ResetPasswordViewModel.cs`
- Create: `ViewModels/Admin/UserListItemViewModel.cs`

- [ ] **Step 1: Create the view models**

Create `ViewModels/Admin/UserListItemViewModel.cs`:

```csharp
namespace dwa_ver_val.ViewModels.Admin;

public class UserListItemViewModel
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string FullName { get; set; }
    public required string EmployeeNumber { get; set; }
    public required string Role { get; set; }
    public string? OrgUnitName { get; set; }
    public bool IsActive { get; set; }
}
```

Create `ViewModels/Admin/CreateUserViewModel.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.ViewModels.Admin;

public class CreateUserViewModel
{
    [Required, EmailAddress, Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required, Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required, Display(Name = "Employee Number")]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, Display(Name = "Role")]
    public string Role { get; set; } = string.Empty;

    [Display(Name = "Organisational Unit")]
    public Guid? OrgUnitId { get; set; }

    [Required, DataType(DataType.Password), MinLength(8), Display(Name = "Initial Password")]
    public string InitialPassword { get; set; } = string.Empty;

    public IEnumerable<string> AvailableRoles { get; set; } = Array.Empty<string>();
    public IEnumerable<(Guid Id, string Name)> AvailableOrgUnits { get; set; } = Array.Empty<(Guid, string)>();
}
```

Create `ViewModels/Admin/EditUserViewModel.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.ViewModels.Admin;

public class EditUserViewModel
{
    public Guid Id { get; set; }

    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required, Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required, Display(Name = "Employee Number")]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required, Display(Name = "Role")]
    public string Role { get; set; } = string.Empty;

    [Display(Name = "Organisational Unit")]
    public Guid? OrgUnitId { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; }

    public IEnumerable<string> AvailableRoles { get; set; } = Array.Empty<string>();
    public IEnumerable<(Guid Id, string Name)> AvailableOrgUnits { get; set; } = Array.Empty<(Guid, string)>();
}
```

Create `ViewModels/Admin/ResetPasswordViewModel.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace dwa_ver_val.ViewModels.Admin;

public class ResetPasswordViewModel
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(8), Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword)), Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Create the controller**

Create `Controllers/Admin/UsersController.cs`:

```csharp
using dwa_ver_val.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dwa_ver_val.Controllers.Admin;

[Authorize(Policy = DwsPolicies.CanAdminister)]
[Route("Admin/[controller]/[action]")]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole<Guid>> _roles;
    private readonly ApplicationDBContext _db;

    public UsersController(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles,
        ApplicationDBContext db)
    {
        _users = users;
        _roles = roles;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await _db.Users
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.EmployeeNumber,
                u.IsActive,
                OrgUnitName = u.OrgUnit != null ? u.OrgUnit.Name : null
            })
            .ToListAsync();

        var items = new List<UserListItemViewModel>();
        foreach (var u in users)
        {
            var roles = await _users.GetRolesAsync(new ApplicationUser { Id = u.Id });
            items.Add(new UserListItemViewModel
            {
                Id = u.Id,
                Email = u.Email ?? string.Empty,
                FullName = $"{u.FirstName} {u.LastName}",
                EmployeeNumber = u.EmployeeNumber,
                Role = roles.FirstOrDefault() ?? "(none)",
                OrgUnitName = u.OrgUnitName,
                IsActive = u.IsActive
            });
        }

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CreateUserViewModel
        {
            AvailableRoles = DwsRoles.All,
            AvailableOrgUnits = await LoadOrgUnits()
        };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableRoles = DwsRoles.All;
            model.AvailableOrgUnits = await LoadOrgUnits();
            return View(model);
        }

        if (!DwsRoles.All.Contains(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Unknown role.");
            model.AvailableRoles = DwsRoles.All;
            model.AvailableOrgUnits = await LoadOrgUnits();
            return View(model);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            FirstName = model.FirstName,
            LastName = model.LastName,
            EmployeeNumber = model.EmployeeNumber,
            OrgUnitId = model.OrgUnitId,
            IsActive = true
        };

        var create = await _users.CreateAsync(user, model.InitialPassword);
        if (!create.Succeeded)
        {
            foreach (var e in create.Errors) ModelState.AddModelError(string.Empty, e.Description);
            model.AvailableRoles = DwsRoles.All;
            model.AvailableOrgUnits = await LoadOrgUnits();
            return View(model);
        }

        await _users.AddToRoleAsync(user, model.Role);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        var roles = await _users.GetRolesAsync(user);

        return View(new EditUserViewModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            EmployeeNumber = user.EmployeeNumber,
            Role = roles.FirstOrDefault() ?? string.Empty,
            OrgUnitId = user.OrgUnitId,
            IsActive = user.IsActive,
            AvailableRoles = DwsRoles.All,
            AvailableOrgUnits = await LoadOrgUnits()
        });
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, EditUserViewModel model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            model.AvailableRoles = DwsRoles.All;
            model.AvailableOrgUnits = await LoadOrgUnits();
            return View(model);
        }

        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.EmployeeNumber = model.EmployeeNumber;
        user.OrgUnitId = model.OrgUnitId;
        user.IsActive = model.IsActive;

        var update = await _users.UpdateAsync(user);
        if (!update.Succeeded)
        {
            foreach (var e in update.Errors) ModelState.AddModelError(string.Empty, e.Description);
            model.AvailableRoles = DwsRoles.All;
            model.AvailableOrgUnits = await LoadOrgUnits();
            return View(model);
        }

        var currentRoles = await _users.GetRolesAsync(user);
        if (!currentRoles.Contains(model.Role))
        {
            await _users.RemoveFromRolesAsync(user, currentRoles);
            await _users.AddToRoleAsync(user, model.Role);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ResetPassword(Guid id)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        return View(new ResetPasswordViewModel { UserId = user.Id, Email = user.Email ?? string.Empty });
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordViewModel model)
    {
        if (id != model.UserId) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        user.IsActive = false;
        await _users.UpdateAsync(user);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        user.IsActive = true;
        await _users.UpdateAsync(user);
        return RedirectToAction(nameof(Index));
    }

    private async Task<IEnumerable<(Guid Id, string Name)>> LoadOrgUnits() =>
        await _db.OrganisationalUnits
            .OrderBy(ou => ou.Name)
            .Select(ou => new ValueTuple<Guid, string>(ou.OrgUnitId, ou.Name))
            .ToListAsync();
}
```

- [ ] **Step 3: Build**

```bash
dotnet build
```

Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add Controllers/Admin/UsersController.cs ViewModels/Admin/
git commit -m "Add Admin/UsersController with Index, Create, Edit, ResetPassword, Deactivate"
```

---

### Task 6.2: Admin views

**Files:**
- Create: `Views/Users/Index.cshtml`
- Create: `Views/Users/Create.cshtml`
- Create: `Views/Users/Edit.cshtml`
- Create: `Views/Users/ResetPassword.cshtml`

Razor conventionally resolves views from `Views/{Controller}/`; our controller namespace has an `Admin/` prefix but the conventional view lookup uses the controller name (`Users`). Keep the view folder at `Views/Users/` and configure the route prefix via `[Route("Admin/[controller]/[action]")]` as already applied.

- [ ] **Step 1: Index view**

Create `Views/Users/Index.cshtml`:

```cshtml
@model IEnumerable<dwa_ver_val.ViewModels.Admin.UserListItemViewModel>
@{
    ViewData["Title"] = "Manage Users";
}

<div class="dws-page-header">
    <h1>Manage Users</h1>
    <a asp-action="Create" class="dws-btn dws-btn-primary">Add User</a>
</div>

<table class="dws-table">
    <thead>
        <tr>
            <th>Name</th>
            <th>Email</th>
            <th>Employee Number</th>
            <th>Role</th>
            <th>Organisational Unit</th>
            <th>Status</th>
            <th></th>
        </tr>
    </thead>
    <tbody>
    @foreach (var u in Model)
    {
        <tr>
            <td>@u.FullName</td>
            <td>@u.Email</td>
            <td>@u.EmployeeNumber</td>
            <td>@u.Role</td>
            <td>@(u.OrgUnitName ?? "(national)")</td>
            <td>
                @if (u.IsActive)
                {
                    <span class="dws-status-pill dws-status-pill--ok">Active</span>
                }
                else
                {
                    <span class="dws-status-pill dws-status-pill--warning">Inactive</span>
                }
            </td>
            <td>
                <a asp-action="Edit" asp-route-id="@u.Id" class="dws-btn dws-btn-ghost">Edit</a>
                <a asp-action="ResetPassword" asp-route-id="@u.Id" class="dws-btn dws-btn-ghost">Reset password</a>
                @if (u.IsActive)
                {
                    <form asp-action="Deactivate" asp-route-id="@u.Id" method="post" class="dws-inline-form">
                        @Html.AntiForgeryToken()
                        <button type="submit" class="dws-btn dws-btn-danger">Deactivate</button>
                    </form>
                }
                else
                {
                    <form asp-action="Reactivate" asp-route-id="@u.Id" method="post" class="dws-inline-form">
                        @Html.AntiForgeryToken()
                        <button type="submit" class="dws-btn dws-btn-ghost">Reactivate</button>
                    </form>
                }
            </td>
        </tr>
    }
    </tbody>
</table>
```

- [ ] **Step 2: Create view**

Create `Views/Users/Create.cshtml`:

```cshtml
@model dwa_ver_val.ViewModels.Admin.CreateUserViewModel
@{
    ViewData["Title"] = "Add User";
}

<div class="dws-page-header">
    <h1>Add User</h1>
</div>

<form asp-action="Create" method="post" class="dws-form">
    <div asp-validation-summary="All" class="dws-validation-summary"></div>

    <div class="dws-form-row">
        <label asp-for="FirstName" class="dws-label"></label>
        <input asp-for="FirstName" class="dws-input" />
        <span asp-validation-for="FirstName" class="dws-field-error"></span>
    </div>
    <div class="dws-form-row">
        <label asp-for="LastName" class="dws-label"></label>
        <input asp-for="LastName" class="dws-input" />
        <span asp-validation-for="LastName" class="dws-field-error"></span>
    </div>
    <div class="dws-form-row">
        <label asp-for="Email" class="dws-label"></label>
        <input asp-for="Email" class="dws-input" />
        <span asp-validation-for="Email" class="dws-field-error"></span>
    </div>
    <div class="dws-form-row">
        <label asp-for="EmployeeNumber" class="dws-label"></label>
        <input asp-for="EmployeeNumber" class="dws-input" />
        <span asp-validation-for="EmployeeNumber" class="dws-field-error"></span>
    </div>
    <div class="dws-form-row">
        <label asp-for="Role" class="dws-label"></label>
        <select asp-for="Role" class="dws-input" asp-items="@(new SelectList(Model.AvailableRoles))">
            <option value="">-- select role --</option>
        </select>
        <span asp-validation-for="Role" class="dws-field-error"></span>
    </div>
    <div class="dws-form-row">
        <label asp-for="OrgUnitId" class="dws-label"></label>
        <select asp-for="OrgUnitId" class="dws-input"
                asp-items="@(new SelectList(Model.AvailableOrgUnits, "Id", "Name"))">
            <option value="">-- none (national scope) --</option>
        </select>
    </div>
    <div class="dws-form-row">
        <label asp-for="InitialPassword" class="dws-label"></label>
        <input asp-for="InitialPassword" class="dws-input" type="password" />
        <span asp-validation-for="InitialPassword" class="dws-field-error"></span>
    </div>

    <button type="submit" class="dws-btn dws-btn-primary">Create</button>
    <a asp-action="Index" class="dws-btn dws-btn-ghost">Cancel</a>
</form>
```

- [ ] **Step 3: Edit view**

Create `Views/Users/Edit.cshtml`:

```cshtml
@model dwa_ver_val.ViewModels.Admin.EditUserViewModel
@{
    ViewData["Title"] = "Edit User";
}

<div class="dws-page-header">
    <h1>Edit User — @Model.Email</h1>
</div>

<form asp-action="Edit" asp-route-id="@Model.Id" method="post" class="dws-form">
    <input type="hidden" asp-for="Id" />
    <div asp-validation-summary="All" class="dws-validation-summary"></div>

    <div class="dws-form-row">
        <label asp-for="FirstName" class="dws-label"></label>
        <input asp-for="FirstName" class="dws-input" />
        <span asp-validation-for="FirstName" class="dws-field-error"></span>
    </div>
    <div class="dws-form-row">
        <label asp-for="LastName" class="dws-label"></label>
        <input asp-for="LastName" class="dws-input" />
        <span asp-validation-for="LastName" class="dws-field-error"></span>
    </div>
    <div class="dws-form-row">
        <label asp-for="EmployeeNumber" class="dws-label"></label>
        <input asp-for="EmployeeNumber" class="dws-input" />
        <span asp-validation-for="EmployeeNumber" class="dws-field-error"></span>
    </div>
    <div class="dws-form-row">
        <label asp-for="Role" class="dws-label"></label>
        <select asp-for="Role" class="dws-input" asp-items="@(new SelectList(Model.AvailableRoles))">
            <option value="">-- select role --</option>
        </select>
        <span asp-validation-for="Role" class="dws-field-error"></span>
    </div>
    <div class="dws-form-row">
        <label asp-for="OrgUnitId" class="dws-label"></label>
        <select asp-for="OrgUnitId" class="dws-input"
                asp-items="@(new SelectList(Model.AvailableOrgUnits, "Id", "Name"))">
            <option value="">-- none (national scope) --</option>
        </select>
    </div>
    <div class="dws-form-row">
        <label class="dws-checkbox">
            <input asp-for="IsActive" type="checkbox" />
            @Html.DisplayNameFor(m => m.IsActive)
        </label>
    </div>

    <button type="submit" class="dws-btn dws-btn-primary">Save</button>
    <a asp-action="Index" class="dws-btn dws-btn-ghost">Cancel</a>
</form>
```

- [ ] **Step 4: Reset password view**

Create `Views/Users/ResetPassword.cshtml`:

```cshtml
@model dwa_ver_val.ViewModels.Admin.ResetPasswordViewModel
@{
    ViewData["Title"] = "Reset Password";
}

<div class="dws-page-header">
    <h1>Reset Password — @Model.Email</h1>
</div>

<form asp-action="ResetPassword" asp-route-id="@Model.UserId" method="post" class="dws-form">
    <input type="hidden" asp-for="UserId" />
    <div asp-validation-summary="All" class="dws-validation-summary"></div>

    <div class="dws-form-row">
        <label asp-for="NewPassword" class="dws-label"></label>
        <input asp-for="NewPassword" class="dws-input" type="password" />
        <span asp-validation-for="NewPassword" class="dws-field-error"></span>
    </div>
    <div class="dws-form-row">
        <label asp-for="ConfirmPassword" class="dws-label"></label>
        <input asp-for="ConfirmPassword" class="dws-input" type="password" />
        <span asp-validation-for="ConfirmPassword" class="dws-field-error"></span>
    </div>

    <button type="submit" class="dws-btn dws-btn-primary">Reset</button>
    <a asp-action="Index" class="dws-btn dws-btn-ghost">Cancel</a>
</form>
```

- [ ] **Step 5: Build and smoke**

```bash
dotnet build
dotnet run
```

Sign in as `admin@dwa.demo`, navigate to `/Admin/Users/Index`. Confirm the list renders, Create/Edit/Reset-password flows work end-to-end. Sign in as `validator-...@dwa.demo`; navigate to `/Admin/Users/Index`; confirm a 403/Access Denied.

- [ ] **Step 6: Commit**

```bash
git add Views/Users/
git commit -m "Add admin views for user management (Index, Create, Edit, ResetPassword)"
```

---

## Phase 7 — Verification

### Task 7.1: Integration test — login per role redirects to dashboard

**Files:**
- Create: `Tests/Integration/IdentityFlowTests.cs`

- [ ] **Step 1: Test harness**

Create `Tests/Integration/IdentityFlowTests.cs`:

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http;
using Xunit;

namespace dwa_ver_val.Tests.Integration;

public class IdentityFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IdentityFlowTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
        });
    }

    [Theory]
    [InlineData("admin@dwa.demo")]
    [InlineData("national@dwa.demo")]
    [InlineData("readonly@dwa.demo")]
    public async Task LoginPage_IsReachableAnonymously(string _)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedPage_RedirectsAnonymousToLogin()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync("/FileMaster");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task AdminUsersPage_NonAdmin_RedirectsToAccessDenied()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var token = await GetAntiForgeryToken(client, "/Account/Login");
        var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", "readonly@dwa.demo"),
            new KeyValuePair<string, string>("Password", "Demo@Pass2026"),
            new KeyValuePair<string, string>("RememberMe", "false"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync("/Admin/Users/Index");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.OriginalString);
    }

    private static async Task<string> GetAntiForgeryToken(HttpClient client, string path)
    {
        var body = await client.GetStringAsync(path);
        var marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        var start = body.IndexOf(marker) + marker.Length;
        var end = body.IndexOf('"', start);
        return body[start..end];
    }
}
```

- [ ] **Step 2: Run the tests**

```bash
dotnet test Tests/dwa_ver_val.Tests.csproj --filter "FullyQualifiedName~IdentityFlowTests"
```

Expected: three tests PASS. If the login test fails, check that:
- SQL Server is running (the WebApplicationFactory hits the real DbContext).
- `appsettings.Development.json` has `Identity:InitialDemoPassword = Demo@Pass2026`.
- Seeding has run (first test execution will trigger `await db.Database.MigrateAsync()` and seeders).

For CI or environments without Docker SQL, override the `ApplicationDBContext` registration in `WebApplicationFactory.WithWebHostBuilder` to use the `Microsoft.EntityFrameworkCore.InMemory` provider (already referenced in `Tests/dwa_ver_val.Tests.csproj`). The override pattern:

```csharp
_factory = factory.WithWebHostBuilder(b =>
{
    b.UseEnvironment("Development");
    b.ConfigureServices(services =>
    {
        var dbDescriptor = services.SingleOrDefault(s => s.ServiceType == typeof(DbContextOptions<ApplicationDBContext>));
        if (dbDescriptor is not null) services.Remove(dbDescriptor);
        services.AddDbContext<ApplicationDBContext>(o => o.UseInMemoryDatabase("integration-tests"));
    });
});
```

Use this only if the real SQL Server path is blocked; otherwise prefer the real DbContext so Identity's relational stores are exercised.

- [ ] **Step 3: Commit**

```bash
git add Tests/Integration/IdentityFlowTests.cs
git commit -m "Add integration tests for login redirect, anonymous protection, and admin policy denial"
```

---

### Task 7.2: Final verification — all tests pass

- [ ] **Step 1: Run the full suite**

```bash
dotnet test
```

Expected: all tests pass — the original 33 from commit `a3d879e`, the new `DwsClaimsTransformationTests` (2), `ScopedCaseQueryTests` (2), `IdentityFlowTests` (3). Total ≥ 40 passing.

- [ ] **Step 2: Manual smoke pass**

```bash
dotnet run
```

Confirm:
- Anonymous user hitting `/` is redirected to `/Account/Login`.
- Sign in as `validator-{wma}@dwa.demo`; `/FileMaster` shows only in-WMA cases.
- Sign in as `national@dwa.demo`; `/FileMaster` shows all cases.
- Sign in as `admin@dwa.demo`; `/Admin/Users/Index` renders; Create a new user; Edit; Reset password; Deactivate; Reactivate.
- Sign in as `readonly@dwa.demo`; `/Admin/Users/Index` → redirects to `/Account/AccessDenied`.

- [ ] **Step 3: Update the journal**

Append an entry to `docs/superpowers/tasks/2026-04-24-mvp-hardening/journal.md` describing the work done in this plan. Keep it terse:

```markdown
### 2026-04-24 HH:MM — (plan-1-controller) — Plan 1 complete

- **Read:** spec §1 §9, existing Program.cs, ApplicationDBContext.cs, ApplicationUser.cs
- **Changed:**
  - Models/ApplicationUser.cs → IdentityUser<Guid>
  - DatabaseContexts/ApplicationDBContext.cs → IdentityDbContext<..., Guid>
  - Program.cs → AddIdentity, policies, cookie auth, scope service DI
  - Services/Auth/{DwsClaimsTransformation,DwsPolicies,DwsRoles,IScopedCaseQuery,ScopedCaseQuery}.cs — new
  - Services/IdentitySeeder.cs — new
  - Controllers/AccountController.cs, Controllers/Admin/UsersController.cs — new
  - Views/Account/*, Views/Users/* — basic (re-skin in Plan 2)
  - Migrations/<ts>_FoundationsIdentity.cs
  - docs/contracts/auth-claims.md, contracts/fixtures/auth/claims.json — contract established
- **Learned:**
  - ApplicationUser previously had ApplicationUserId (Guid) colliding with IdentityUser.Id (string); switched to IdentityUser<Guid> so Id is the only PK — FK columns all remained Guid, no data loss.
  - DwsClaimsTransformation is called on EVERY request (scoped service); uses a marker claim to stay idempotent.
  - ScopedCaseQuery filters by WMA (not OrgUnit) because FileMaster→Property→WmaId is the canonical scope path; OrgUnit is only the user's assignment, not the data's.
- **Status:** DONE
```

- [ ] **Step 4: Commit the journal update**

```bash
git add docs/superpowers/tasks/2026-04-24-mvp-hardening/journal.md
git commit -m "Plan 1 journal entry — Foundations complete"
```

---

## Summary of files touched

**New:**
- `docs/superpowers/tasks/2026-04-24-mvp-hardening/journal.md`
- `docs/contracts/auth-claims.md`, `docs/contracts/CHANGELOG.md`
- `contracts/fixtures/auth/claims.json`
- `Services/Auth/DwsClaimsTransformation.cs`, `DwsPolicies.cs`, `DwsRoles.cs`, `IScopedCaseQuery.cs`, `ScopedCaseQuery.cs`
- `Services/IdentitySeeder.cs`
- `Controllers/AccountController.cs`, `Controllers/Admin/UsersController.cs`
- `ViewModels/LoginViewModel.cs`
- `ViewModels/Admin/CreateUserViewModel.cs`, `EditUserViewModel.cs`, `ResetPasswordViewModel.cs`, `UserListItemViewModel.cs`
- `Views/Account/Login.cshtml`, `AccessDenied.cshtml`
- `Views/Users/Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `ResetPassword.cshtml`
- `Migrations/<timestamp>_FoundationsIdentity.cs` (+ `.Designer.cs`)
- `Tests/Services/Auth/DwsClaimsTransformationTests.cs`, `ScopedCaseQueryTests.cs`
- `Tests/Integration/IdentityFlowTests.cs`

**Modified:**
- `Models/ApplicationUser.cs`
- `DatabaseContexts/ApplicationDBContext.cs`
- `Program.cs`
- `dwa_ver_val.csproj`, `Tests/dwa_ver_val.Tests.csproj`
- `appsettings.Development.json`
- `Interfaces/IEntitlement.cs`
- `Controllers/FileMasterController.cs` (inject `IScopedCaseQuery`, authorize, scope the list)
- `Migrations/ApplicationDBContextModelSnapshot.cs`

## Acceptance

When Plan 1 is complete, the following are true:

1. The app builds with `dotnet build` (zero errors).
2. `dotnet test` passes ≥ 40 tests.
3. `dotnet run` starts; anonymous requests to anything except `/Account/Login` redirect to the login page.
4. A `SystemAdmin` can log in, navigate to `/Admin/Users/Index`, create new users, edit them, reset passwords, deactivate/reactivate.
5. A `Validator` logging in with an `OrgUnitId` in Limpopo WMA sees only FileMaster cases whose `Property.WmaId` is Limpopo.
6. A `NationalManager` sees all FileMaster cases regardless of WMA.
7. `docs/contracts/auth-claims.md` + `contracts/fixtures/auth/claims.json` are committed; `DwsClaimsTransformationTests` asserts the producer matches the fixture.
8. The journal at `docs/superpowers/tasks/2026-04-24-mvp-hardening/journal.md` has a terminal entry describing Plan 1.

Plan 2 (UI re-skin) can begin once Plan 1 is on `demo/azure-deploy` and deployed.
