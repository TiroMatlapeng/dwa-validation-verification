using Microsoft.Playwright;

namespace dwa_ver_val.E2E.Infrastructure;

/// <summary>
/// One live app + one Chromium browser, shared across EVERY E2E test class via a
/// single xUnit collection fixture. Phase-2 test classes opt in by decorating the
/// class with <c>[Collection(E2ECollection.Name)]</c> and taking
/// <see cref="E2EAppFixture"/> in their constructor. This guarantees the (slow) app
/// boot + migration/seed + browser download happen exactly once for the whole run.
/// </summary>
[CollectionDefinition(Name)]
public sealed class E2ECollection : ICollectionFixture<E2EAppFixture>
{
    public const string Name = "E2E app collection";
}

/// <summary>
/// Owns the Kestrel-hosted app, the Playwright driver, and a single shared browser.
/// Exposes <see cref="NewPageAsync"/> (a fresh isolated browser context per test) and
/// <see cref="LoginAsync"/> for internal demo-user cookie auth.
/// </summary>
public sealed class E2EAppFixture : IAsyncLifetime
{
    private readonly KestrelAppFixture _app = new();
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>Base URL of the live app, e.g. http://127.0.0.1:53123.</summary>
    public string BaseUrl => _app.BaseUrl;

    public async Task InitializeAsync()
    {
        await _app.InitializeAsync();           // boots Kestrel + migrates/seeds E2E DB + installs Chromium
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    /// <summary>
    /// Opens a fresh, isolated browser context (its own cookie jar) and a blank page.
    /// Dispose the returned page's context via <see cref="DisposePageAsync"/> when done,
    /// or just let the browser dispose at end-of-run. Each test should call this so
    /// auth state never leaks between tests sharing the one browser.
    /// </summary>
    public async Task<IPage> NewPageAsync()
    {
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
        });
        return await context.NewPageAsync();
    }

    /// <summary>Closes the page's owning context (clears its cookies). Safe to call once per test.</summary>
    public static async Task DisposePageAsync(IPage page) => await page.Context.CloseAsync();

    /// <summary>
    /// Logs an internal DWS staff demo user in via the cookie-based ASP.NET Identity
    /// login form. Navigates to /Account/Login, fills Email + Password, submits, and
    /// waits for the post-login redirect away from the login page. The anti-forgery
    /// token is carried automatically because we submit the real rendered form.
    ///
    /// Phase-2 RBAC tests: pass <see cref="DemoUsers"/> emails (e.g.
    /// <c>DemoUsers.Validator(wmaCode)</c>) with <see cref="KestrelAppFixture.DemoPassword"/>.
    /// </summary>
    public async Task LoginAsync(IPage page, string email, string password = KestrelAppFixture.DemoPassword)
    {
        await page.GotoAsync("/Account/Login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.FillAsync("input[name='Email']", email);
        await page.FillAsync("input[name='Password']", password);
        await page.ClickAsync("button[type='submit']");
        // After a successful login the app redirects to Home; the URL leaves /Account/Login.
        await page.WaitForURLAsync(url => !url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase));
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
        await ((IAsyncLifetime)_app).DisposeAsync();
    }
}
