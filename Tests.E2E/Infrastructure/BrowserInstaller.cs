using Microsoft.Playwright;

namespace dwa_ver_val.E2E.Infrastructure;

/// <summary>
/// Installs the Chromium browser binary Playwright needs, programmatically, so the
/// suite is self-contained (no separate <c>playwright.ps1 install</c> step required).
/// Idempotent: Playwright no-ops if the browser is already present.
/// </summary>
public static class BrowserInstaller
{
    private static readonly object Gate = new();
    private static bool _installed;

    public static void EnsureChromium()
    {
        if (_installed) return;
        lock (Gate)
        {
            if (_installed) return;

            var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Playwright Chromium install failed with exit code {exitCode}.");
            }

            _installed = true;
        }
    }
}
