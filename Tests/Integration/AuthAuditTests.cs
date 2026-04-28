using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace dwa_ver_val.Tests.Integration;

/// <summary>
/// Locks down the auth-audit guarantee: every sign-in attempt must leave an immutable AuditLog
/// row. Without these tests, a refactor of AccountController could silently drop the calls
/// to IAuditService.LogAsync and the regression wouldn't show up until production review.
/// </summary>
public class AuthAuditTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _factory;

    public AuthAuditTests(IntegrationTestFixture factory) => _factory = factory;

    private async Task<DateTime> NowFromDbAsync()
    {
        // Use the DB clock so the assertion isn't sensitive to test-host skew.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        // SqlServer doesn't accept GETUTCDATE() via raw projection without a backing table;
        // fall back to the test process clock minus a small grace window.
        await db.Database.OpenConnectionAsync();
        try
        {
            using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "SELECT SYSUTCDATETIME()";
            var result = await cmd.ExecuteScalarAsync();
            return result is DateTime dt ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : DateTime.UtcNow;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    [Fact]
    public async Task SuccessfulLogin_WritesSignedInAuditRow()
    {
        var before = await NowFromDbAsync();
        using var client = IntegrationTestHelpers.CreateAuthenticatedClient(_factory);
        var login = await IntegrationTestHelpers.LoginAsDemoUser(client, "validator-3@dwa.demo");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        var row = await db.AuditLogs
            .Where(a => a.Action == "SignedIn"
                        && a.UserName == "Jane Validator"
                        && a.Timestamp >= before)
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();
        Assert.NotNull(row);
        Assert.Equal(nameof(ApplicationUser), row!.EntityType);
    }

    [Fact]
    public async Task WrongPasswordLogin_WritesSignInFailedAuditRow()
    {
        var before = await NowFromDbAsync();
        using var client = IntegrationTestHelpers.CreateAuthenticatedClient(_factory);
        var login = await IntegrationTestHelpers.LoginAsDemoUser(client, "validator-3@dwa.demo", password: "WrongPassword!1");
        // Login form re-renders on failure (200 OK), not a redirect.
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        var row = await db.AuditLogs
            .Where(a => a.Action == "SignInFailed"
                        && a.UserName == "Jane Validator"
                        && a.Timestamp >= before)
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();
        Assert.NotNull(row);
        Assert.Contains("Wrong password", row!.Description);
    }

    [Fact]
    public async Task UnknownEmailLogin_WritesSignInFailedRowWithUnknownReason()
    {
        var before = await NowFromDbAsync();
        using var client = IntegrationTestHelpers.CreateAuthenticatedClient(_factory);
        var login = await IntegrationTestHelpers.LoginAsDemoUser(client, "ghost@example.com", password: "WhatEver1!");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        var row = await db.AuditLogs
            .Where(a => a.Action == "SignInFailed"
                        && a.UserName == "ghost@example.com"
                        && a.Timestamp >= before)
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();
        Assert.NotNull(row);
        Assert.Equal("Unknown email", row!.Description);
    }

    [Fact]
    public async Task ForgotPassword_KnownEmail_WritesPasswordResetRequestedRow()
    {
        var before = await NowFromDbAsync();
        using var client = IntegrationTestHelpers.CreateAuthenticatedClient(_factory);
        var token = await IntegrationTestHelpers.GetAntiForgeryToken(client, "/Account/ForgotPassword");
        var response = await client.PostAsync("/Account/ForgotPassword", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", "validator-3@dwa.demo"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        var row = await db.AuditLogs
            .Where(a => a.Action == "PasswordResetRequested"
                        && a.UserName == "Jane Validator"
                        && a.Timestamp >= before)
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();
        Assert.NotNull(row);
    }
}
