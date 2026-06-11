using dwa_ver_val.E2E.Infrastructure;
using Microsoft.Playwright;
using OtpNet;

namespace dwa_ver_val.E2E;

/// <summary>
/// Internal-staff TOTP MFA, end to end: a dedicated user (created via the admin console so the
/// shared demo accounts stay 2FA-free) enrols an authenticator from the Security page, then must
/// pass the 6-digit code at sign-in; a wrong code is rejected; a single-use recovery code works
/// as the fallback. TOTP codes are computed in-test with OtpNet from the on-screen shared key —
/// the same RFC 6238 parameters (SHA1/6 digits/30s) Identity's authenticator provider verifies.
/// </summary>
[Collection(E2ECollection.Name)]
public sealed class InternalMfaTests
{
    private readonly E2EAppFixture _fixture;
    public InternalMfaTests(E2EAppFixture fixture) => _fixture = fixture;

    private static readonly PageGotoOptions Idle = new() { WaitUntil = WaitUntilState.NetworkIdle };
    private const string Password = "MfaTest@2026!x";

    private static string ComputeCode(string sharedKeyDisplay)
    {
        // The Security page shows the base32 key lowercased and space-grouped for manual entry.
        var secret = sharedKeyDisplay.Replace(" ", string.Empty).ToUpperInvariant();
        return new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();
    }

    [Fact]
    public async Task InternalUser_EnrolsTotp_ThenLoginRequiresCode_AndRecoveryCodeWorks()
    {
        var email = $"mfa-{DateTime.UtcNow:HHmmssfff}@dwa.test";

        // ── Admin creates a dedicated user (Validator role, any org unit) ──
        var adminPage = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(adminPage, DemoUsers.Admin);
            await adminPage.GotoAsync("/Admin/Users/Create", Idle);
            await adminPage.FillAsync("input[name='FirstName']", "Mfa");
            await adminPage.FillAsync("input[name='LastName']", "Tester");
            await adminPage.FillAsync("input[name='Email']", email);
            await adminPage.FillAsync("input[name='EmployeeNumber']", "EMP-MFA-1");
            await adminPage.SelectOptionAsync("select[name='Role']", "Validator");
            await adminPage.SelectOptionAsync("select[name='OrgUnitId']", new SelectOptionValue { Index = 1 });
            await adminPage.FillAsync("input[name='InitialPassword']", Password);
            await adminPage.ClickAsync("form.dws-form button[type='submit']");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
        finally { await E2EAppFixture.DisposePageAsync(adminPage); }

        // ── The user enrols an authenticator from the Security page ──
        string sharedKey;
        string recoveryCode;
        var page = await _fixture.NewPageAsync();
        try
        {
            await _fixture.LoginAsync(page, email, Password);

            await page.GotoAsync("/Security/Index", Idle);
            Assert.Contains("not enabled", await page.InnerTextAsync("body"), StringComparison.OrdinalIgnoreCase);

            await page.GotoAsync("/Security/EnableAuthenticator", Idle);
            sharedKey = (await page.InnerTextAsync("#shared-key")).Trim();
            Assert.False(string.IsNullOrWhiteSpace(sharedKey), "No shared key rendered on enrolment page.");

            await page.FillAsync("input[name='Code']", ComputeCode(sharedKey));
            await page.ClickAsync("form.dws-form button[type='submit']");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Recovery codes are shown exactly once — capture one for the fallback test.
            var codes = await page.Locator("#recovery-codes li").AllInnerTextsAsync();
            Assert.True(codes.Count >= 8, $"Expected 8 recovery codes, saw {codes.Count}.");
            recoveryCode = codes[0].Trim();

            await page.GotoAsync("/Security/Index", Idle);
            Assert.Contains("is enabled", await page.InnerTextAsync("body"), StringComparison.OrdinalIgnoreCase);
        }
        finally { await E2EAppFixture.DisposePageAsync(page); }

        // ── Fresh session: password alone is no longer enough ──
        var page2 = await _fixture.NewPageAsync();
        try
        {
            await page2.GotoAsync("/Account/Login", Idle);
            await page2.FillAsync("input[name='Email']", email);
            await page2.FillAsync("input[name='Password']", Password);
            await page2.ClickAsync("button[type='submit']");
            await page2.WaitForURLAsync(u => u.Contains("/Account/LoginWith2fa", StringComparison.OrdinalIgnoreCase));

            // Wrong code is rejected, session stays on the 2FA page.
            await page2.FillAsync("input[name='TwoFactorCode']", "000000");
            await page2.ClickAsync("button[type='submit']");
            await page2.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.Contains("/Account/LoginWith2fa", page2.Url, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Invalid authenticator code", await page2.InnerTextAsync("body"));

            // Correct code signs in.
            await page2.FillAsync("input[name='TwoFactorCode']", ComputeCode(sharedKey));
            await page2.ClickAsync("button[type='submit']");
            await page2.WaitForURLAsync(u => !u.Contains("/Account/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain("/Account/Login", page2.Url, StringComparison.OrdinalIgnoreCase);
        }
        finally { await E2EAppFixture.DisposePageAsync(page2); }

        // ── Fallback: single-use recovery code ──
        var page3 = await _fixture.NewPageAsync();
        try
        {
            await page3.GotoAsync("/Account/Login", Idle);
            await page3.FillAsync("input[name='Email']", email);
            await page3.FillAsync("input[name='Password']", Password);
            await page3.ClickAsync("button[type='submit']");
            await page3.WaitForURLAsync(u => u.Contains("/Account/LoginWith2fa", StringComparison.OrdinalIgnoreCase));

            await page3.ClickAsync("a[href*='RecoveryCodeLogin']");
            await page3.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page3.FillAsync("input[name='RecoveryCode']", recoveryCode);
            await page3.ClickAsync("button[type='submit']");
            await page3.WaitForURLAsync(u => !u.Contains("/Account/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain("/Account/Login", page3.Url, StringComparison.OrdinalIgnoreCase);
        }
        finally { await E2EAppFixture.DisposePageAsync(page3); }
    }
}
