using System.Net;
using Xunit;

namespace dwa_ver_val.Tests.Integration;

public class IdentityFlowTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _factory;

    public IdentityFlowTests(IntegrationTestFixture factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/AccessDenied")]
    [InlineData("/Account/ForgotPassword")]
    [InlineData("/css/dws.css")]
    public async Task PublicResource_IsReachableAnonymously(string path)
    {
        using var client = IntegrationTestHelpers.CreateAuthenticatedClient(_factory);
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/FileMaster")]
    [InlineData("/Property")]
    [InlineData("/Validation")]
    [InlineData("/Owner")]
    [InlineData("/Admin/Users/Index")]
    public async Task ProtectedPage_RedirectsAnonymousToLogin(string path)
    {
        using var client = IntegrationTestHelpers.CreateAuthenticatedClient(_factory);
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task AdminUsersPage_NonAdmin_RedirectsToAccessDenied()
    {
        using var client = IntegrationTestHelpers.CreateAuthenticatedClient(_factory);
        var login = await IntegrationTestHelpers.LoginAsDemoUser(client, "readonly@dwa.demo");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync("/Admin/Users/Index");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task AdminUsersPage_AdminUser_ReturnsTheList()
    {
        using var client = IntegrationTestHelpers.CreateAuthenticatedClient(_factory);
        var login = await IntegrationTestHelpers.LoginAsDemoUser(client, "admin@dwa.demo");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync("/Admin/Users/Index");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Manage Users", body);
        // Demo seed users land in the table — at least one of them should be present by email.
        Assert.Contains("admin@dwa.demo", body);
    }
}
