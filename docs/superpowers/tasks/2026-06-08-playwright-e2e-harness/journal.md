# Task: Playwright E2E harness + regression net (Slice 1)

**Start:** 2026-06-08
**Branch:** fix/remediation-wave4
**Worktree:** main working tree — /Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai
**Plan:** docs/2026-06-08-build-status-report.html → "Next steps", Tier 4 step 14 (Playwright E2E regression)

**Goal:** Establish a .NET Playwright (Microsoft.Playwright + xUnit) E2E harness that boots the real app over HTTP against an isolated E2E database, then lock in regression coverage of existing working flows. Pure-additive — NO production code changes in this slice.

**Stack decision (user-confirmed 2026-06-08):** .NET Playwright + xUnit (single `dotnet test` toolchain), NOT Node.

**Acceptance criteria:**
- New E2E test project compiles and is added to the solution; does not alter the existing `Tests` project's behaviour.
- A Kestrel-hosted fixture serves the app on a real HTTP port that a real Playwright browser can reach.
- E2E runs against a dedicated isolated database (NOT the dev `dwa_val_ver`), migrations applied + demo users seeded on startup.
- `dotnet test` runs the E2E project green (proof pasted in journal).
- Phase 2 regression tests (auth-gate/RBAC, dashboard+reports smoke, DOC-01 letter-exposure) each pass.

**Off-limits:** production code (Controllers/Services/Models/Program.cs behaviour), the existing `Tests/` project, `.worktrees/`, `.claude/worktrees/`.

## Journal

### 2026-06-08 — controller (orchestrator) — task setup
- Read: build status report, rollout WBS, repo tooling (no Playwright present; xUnit+Moq+InMemory+Mvc.Testing+NetArchTest; node/npx + dotnet available; app on http://localhost:4000).
- Decision: foundation is serial (tests depend on fixture). Phase 1 = single agent stands up harness + 1 proving test, fully verified. Phase 2 = parallel agents add independent regression test files on the verified fixture.
- Status: DONE (setup)

### 2026-06-08 — foundation agent (Phase 1) — harness + 1 proving test

**Read first:** controller setup entry (above), CLAUDE.md, `Program.cs` (confirmed `public partial class Program {}` at L340 — no shim needed; MigrateAsync+SeedDataService+IdentitySeeder at L242-253; login path `/Account/Login` L58), `Services/IdentitySeeder.cs` (demo users keyed off `Identity:InitialDemoPassword`; wmaCode derived from Regional OrgUnit's WMA), `Views/Account/Login.cshtml` + `ViewModels/LoginViewModel.cs` (form fields render as `name="Email"`/`name="Password"`/`name="RememberMe"`, heading "Sign in to DWA V&V", `[ValidateAntiForgeryToken]` on POST), existing `Tests/Integration/IntegrationTestBase.cs` (config-override conventions). Verified SQL up (`docker ps` → sqlserver healthy 6d) and creds (`dwa_val_ver` existed, `dwa_val_ver_e2e` did not).

**Hosting approach: (A) in-process real Kestrel** via `WebApplicationFactory<Program>` subclass. Chosen over out-of-process child `dotnet run` to keep a single `dotnet test` lifecycle with no readiness-polling/child-process fragility. Config overrides injected in-memory (no committed-config changes): `ConnectionStrings:Default` → E2E DB, `Identity:InitialDemoPassword=DwaDemo2026!`, environment `Development` (keeps the Production-only POPIA guard dormant + relaxes HTTPS redirect). Cookie `SecurePolicy=SameAsRequest` so the auth cookie survives over plain HTTP.

**Files created:**
- `Tests.E2E/dwa_ver_val.E2E.csproj` — net10.0; refs main project + `Microsoft.Playwright` 1.49.0 + xUnit + Mvc.Testing 10.0.7. Added to `dwa_ver_val.sln`.
- `Tests.E2E/Infrastructure/KestrelAppFixture.cs` — the WAF subclass that boots a REAL Kestrel listener on `http://127.0.0.1:0` and exposes `BaseUrl`.
- `Tests.E2E/Infrastructure/E2ECollection.cs` — xUnit `[CollectionDefinition("E2E app collection")]` + `E2EAppFixture` (owns ONE app + ONE Chromium browser shared across all test classes; `NewPageAsync()` = isolated context per test; `LoginAsync(page,email,password)`).
- `Tests.E2E/Infrastructure/BrowserInstaller.cs` — programmatic `Microsoft.Playwright.Program.Main(["install","chromium"])`, idempotent.
- `Tests.E2E/Infrastructure/DemoUsers.cs` — email helpers (`Validator(wmaCode)` etc.).
- `Tests.E2E/AnonymousRootRedirectsToLoginTests.cs` — the ONE proving test.

**File changed (build-config only, no behaviour change):** `dwa_ver_val.csproj` — added `Compile/Content/None Remove="Tests.E2E/**"` mirroring the existing `Tests/**` exclusion so the web app's default globs don't try to compile the E2E folder.

**Bug hit + fix (controller-diagnosed, applied verbatim):** first run threw `InvalidOperationException: Sequence contains no elements` at `addresses.Addresses.First()`. Root cause: `base.CreateHost(builder)` built AND started the in-memory TestServer host; the rebuilt `_kestrelHost` still resolved TestServer as `IServer` → empty `IServerAddressesFeature.Addresses`. Fix = canonical dotnet/aspnetcore#33846 ordering in `CreateHost`: (1) `builder.Build()` the test host without starting; (2) `ConfigureWebHost` → `UseKestrel()` + `UseUrls("http://127.0.0.1:0")` (applied after the factory's UseTestServer so Kestrel wins); (3) build + Start the Kestrel host FIRST, read `BaseUrl = addresses.Addresses.Last()`; (4) Start the TestServer host and return it. The double `builder.Build()` is supported inside WAF.CreateHost — no "Build can only be called once" thrown.

**Discovered wmaCode = `3`** (seeded Regional OrgUnit's WMA). Demo emails in E2E DB: `admin@dwa.demo`, `national@dwa.demo`, `readonly@dwa.demo`, `regional-3@dwa.demo`, `validator-3@dwa.demo`, `capturer-3@dwa.demo`.

**Isolation verified:** `sys.databases` shows both `dwa_val_ver` (dev, untouched) and `dwa_val_ver_e2e` (new); `AspNetUsers` in the E2E DB holds the 6 seeded demo users → migrations + seeding ran against the isolated DB.

**Passing test output:**
```
Test run for .../Tests.E2E/bin/Debug/net10.0/dwa_ver_val.E2E.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.
Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 1 s - dwa_ver_val.E2E.dll (net10.0)
```

**Phase-2 reuse notes:**
- Decorate each test class with `[Collection(E2ECollection.Name)]` and take `E2EAppFixture` in the ctor — this guarantees ONE app boot + ONE browser for the whole run (no per-class server). DO NOT instantiate `KestrelAppFixture` directly.
- Per test: `var page = await fixture.NewPageAsync();` gives a fresh isolated cookie jar (BaseURL preset, so use relative paths like `page.GotoAsync("/")`). Clean up with `await E2EAppFixture.DisposePageAsync(page);` so auth state never leaks between tests on the shared browser.
- RBAC login: `await fixture.LoginAsync(page, DemoUsers.Validator("3"));` (password defaults to `KestrelAppFixture.DemoPassword`). `LoginAsync` fills the real form so the anti-forgery token is handled by the browser; it waits for the redirect away from `/Account/Login`.
- `BaseUrl` form: `http://127.0.0.1:<dynamic-port>` — never hardcode 4000. Discover the wmaCode at runtime if you prefer not to depend on `"3"`.
- Status: DONE (Phase 1 — harness verified GREEN)

### 2026-06-08 — controller — Phase-1 verified-handoff (Rule 4)
- Re-ran `dotnet build dwa_ver_val.sln` (0 errors) + `dotnet test Tests.E2E` myself → `Passed: 1`. Confirmed green independently before authorising Phase 2. (First controller run had caught the #33846 bug — agent's initial "looks done" was a green-that-wasn't.)
- Status: DONE

### 2026-06-08 — Phase-2 author A — AuthGateAndRbacTests.cs
- Read: E2ECollection.cs (fixture API), DemoUsers.cs, proving-test template, Views/Account/AccessDenied.cshtml, Controllers/ReportsController.cs.
- Created Tests.E2E/AuthGateAndRbacTests.cs: 4 facts — (1) anon→/Account/Login gate; (2) ReadOnly reads /Reports/CatchmentProgress; (3) ReadOnly denied /Reports/UserActivity (→/Account/AccessDenied or "Access denied" h1); (4) National reads /Reports/UserActivity. Used DemoUsers.ReadOnly / DemoUsers.National. Structural assertions only.
- Status: DONE (authored + self-reviewed; not run per contract)

### 2026-06-08 — Phase-2 author B — DashboardAndReportsSmokeTests.cs
- Read: E2ECollection.cs, DemoUsers.cs, proving-test template, Views/Home/Index.cshtml, Views/Reports/Report.cshtml.
- Created Tests.E2E/DashboardAndReportsSmokeTests.cs: (1) dashboard #phaseChart/#statusChart + script[src*=charts.js] + no console errors; (2) [Theory] over the 3 CanRead reports → table.table + format=csv/xlsx/pdf export anchors. Login as ReadOnly. Structural only.
- Status: DONE (authored + self-reviewed; not run per contract)

### 2026-06-08 — Phase-2 author C — LetterExposureRegressionTests.cs
- Read: E2ECollection.cs, proving-test template, Program.cs /_uploads block, FileMasterController.LetterPdf, existing Tests/Integration/StaticFileExposureTests.cs (did not duplicate its HttpClient mechanism).
- Created Tests.E2E/LetterExposureRegressionTests.cs: (1) [Theory] anonymous browser GET /_uploads/letters/*.pdf → 404 + not application/pdf; (2) anonymous GET /FileMaster/LetterPdf?id=..&letterIssuanceId=.. → redirected to /Account/Login, content text/html not PDF. Anonymous (no login).
- Status: DONE (authored + self-reviewed; not run per contract)

### 2026-06-08 — controller — Phase-2 consolidated run + ROOT CAUSE (Rule 4 + systematic-debugging)
- First consolidated `dotnet test Tests.E2E` → **7 failed / 5 passed**. All 7 failures were login-dependent; the 5 passes were all anonymous tests.
- Symptom: after `LoginAsync`, every authenticated nav bounced to `/Account/Login?ReturnUrl=...`, even `/`. `LoginAsync` did NOT time out → server-side "login" left the login page but no Identity cookie was honoured.
- Disproved the cookie-Secure theory: app already sets `SameAsRequest`+`SameSite=Lax` (Program.cs:63,69); fixture mirrors it. Added a temporary `_DiagLoginTests` that dumped login POST status, post-login URL, and the cookie jar.
- **ROOT CAUSE:** E2E app runs in Development → loads **AzureAd user-secrets** present on this machine → enables the Entra (Microsoft) OIDC scheme → login page renders a "Sign in with Microsoft" submit button. `LoginAsync`'s generic `button[type='submit']` matched the **Entra** button → redirect to `login.microsoftonline.com`, no local sign-in, no `.AspNetCore.Identity.Application` cookie. Also made the suite **machine-dependent** (would pass on CI without secrets, fail on a dev box with them).
- **FIX (shared fixture, controller-owned):** blank `AzureAd:TenantId/ClientId/ClientSecret` in `KestrelAppFixture` in-memory config so Entra stays disabled and the login page exposes only the local form. (In-memory config wins over user-secrets — same mechanism already redirects the DB to `dwa_val_ver_e2e`.)
- Diagnostic re-run confirmed: POST 302, after-login URL `/`, protected URL stays on the report, `.AspNetCore.Identity.Application` cookie present (secure=False, sameSite=Lax). Deleted `_DiagLoginTests`.
- **Consolidated re-run: `dotnet test Tests.E2E` → Passed: 12, Failed: 0.** Regression: `dotnet test Tests` → Passed: 495, Failed: 0. Total 507 green.
- Status: DONE (Phase 2 verified GREEN)

## Retro (on completion)
**Converged:** The serial-foundation-then-parallel-authors structure worked exactly as intended. The three Phase-2 files were genuinely independent (distinct files, no shared edits, journal entries returned to controller instead of self-appended → no write race), and all three were correct against the real markup on first authoring — the failures were NOT the agents' fault. **Drifted:** twice the work *looked* done but wasn't — (1) the foundation agent's truncated "build succeeds" before the proving test actually ran (caught by controller running it), and (2) all Phase-2 login tests failed on a cause none of us could see from the code alone (Entra enabled by ambient user-secrets). Both were caught only because the controller ran ground-truth verification rather than trusting "DONE". **Failed prompt pattern → durable lesson:** instructing authors to use a generic `button[type='submit']` login selector is fragile on a dual-auth (local + Entra) login page; the deterministic fix is to neutralise ambient IdP config in the test host, not to trust the selector. Worth a memory entry: *E2E hosts must blank AzureAd config or they pick up dev user-secrets and route login through Entra.*
