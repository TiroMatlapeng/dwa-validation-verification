using System.Net;
using Xunit;

namespace dwa_ver_val.Tests.Integration;

public class ForgotPasswordFlowTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _factory;

    public ForgotPasswordFlowTests(IntegrationTestFixture factory) => _factory = factory;

    [Fact]
    public async Task GetForgotPassword_ReturnsForm()
    {
        using var client = IntegrationTestHelpers.CreateAuthenticatedClient(_factory);
        var response = await client.GetAsync("/Account/ForgotPassword");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Reset your password", body);
        Assert.Contains("name=\"Email\"", body);
    }

    [Fact]
    public async Task PostForgotPassword_KnownEmail_ShowsResetLink()
    {
        using var client = IntegrationTestHelpers.CreateAuthenticatedClient(_factory);
        var token = await IntegrationTestHelpers.GetAntiForgeryToken(client, "/Account/ForgotPassword");

        var response = await client.PostAsync("/Account/ForgotPassword", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", "admin@dwa.demo"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("admin@dwa.demo", body);
        // Demo mode renders the reset link directly into the page; confirm both the link path
        // and that the warning banner explaining "no email service" is present so a future
        // change that wires a real email provider doesn't accidentally keep leaking the link.
        Assert.Contains("/Account/ResetPassword", body);
        Assert.Contains("Demo mode", body);
    }

    [Fact]
    public async Task PostForgotPassword_UnknownEmail_DoesNotRevealAccountExistence()
    {
        using var client = IntegrationTestHelpers.CreateAuthenticatedClient(_factory);
        var token = await IntegrationTestHelpers.GetAntiForgeryToken(client, "/Account/ForgotPassword");

        var response = await client.PostAsync("/Account/ForgotPassword", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Email", "nobody@example.com"),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // Same shell, same email echoed, but no actual reset link should appear.
        Assert.Contains("nobody@example.com", body);
        Assert.DoesNotContain("/Account/ResetPassword", body);
    }
}
